using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Sqlite.Entidades;
using Microsoft.Maui.Storage;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Base de datos local sobre SQLite. Punto único de apertura del archivo.
/// </summary>
/// <remarks>
/// Se registra como <b>singleton</b>: sqlite-net mantiene una conexión por instancia y
/// abrir el mismo archivo desde varias conexiones invita a bloqueos de escritura. Todos
/// los repositorios comparten esta instancia.
///
/// La inicialización está protegida por un semáforo porque las cuatro pestañas cargan a
/// la vez al arrancar y todas la invocan sin coordinarse.
/// </remarks>
public sealed class BaseDatosLocal : ILocalDatabase, IAsyncDisposable
{
	/// <summary>
	/// Versión de esquema que este código espera.
	/// </summary>
	/// <remarks>
	/// Se guarda en el <c>PRAGMA user_version</c> del archivo. Al cambiar el esquema hay
	/// que subir este número y agregar su paso en <see cref="MigrarAsync"/>.
	/// </remarks>
	public const int VersionEsquemaActual = 4;

	private const string NombreArchivo = "appoperador.db3";

	// ReadWrite|Create: la app crea el archivo la primera vez.
	// SharedCache + FullMutex: varias pestañas leen y escriben desde hilos distintos.
	private const SQLiteOpenFlags Banderas =
		SQLiteOpenFlags.ReadWrite |
		SQLiteOpenFlags.Create |
		SQLiteOpenFlags.SharedCache |
		SQLiteOpenFlags.FullMutex;

	private readonly SemaphoreSlim _cerrojoInicializacion = new(1, 1);
	private readonly IDatabaseKeyProvider? _claves;
	private SQLiteAsyncConnection? _conexion;
	private string? _clave;
	private bool _inicializada;

	/// <param name="rutaArchivo">
	/// Archivo de la base. Las pruebas pasan uno temporal para no tocar el del dispositivo.
	/// </param>
	/// <param name="claves">
	/// De dónde sale la clave de cifrado (JTT-1388 CA 2). Sin ella la base se abre en claro,
	/// que es como corren las pruebas que no verifican el cifrado y el destino de escritorio,
	/// donde <c>SecureStorage</c> no existe.
	/// </param>
	public BaseDatosLocal(string? rutaArchivo = null, IDatabaseKeyProvider? claves = null)
	{
		RutaArchivo = rutaArchivo ?? Path.Combine(FileSystem.AppDataDirectory, NombreArchivo);
		_claves = claves;
	}

	/// <inheritdoc />
	public string RutaArchivo { get; }

	/// <inheritdoc />
	public async Task InicializarAsync(CancellationToken cancelacion = default)
	{
		if (_inicializada)
		{
			return;
		}

		await _cerrojoInicializacion.WaitAsync(cancelacion);
		try
		{
			// Segunda comprobación: otra pestaña pudo inicializar mientras esperábamos.
			if (_inicializada)
			{
				return;
			}

			// La clave se resuelve antes de abrir nada: el cifrado se aplica al abrir, no
			// después. Y antes de eso hay que llevarse los datos de una base en claro, si la hay.
			_clave = _claves is null ? null : await _claves.ObtenerAsync(cancelacion);
			CifrarBaseEnClaroSiHace();

			var conexion = AbrirConexion();

			// Va ANTES de crear las tablas, no después: hay cambios que CreateTableAsync no
			// sabe hacer y que dejarían la tabla mal formada si se aplicaran encima.
			await PrepararEsquemaAsync(conexion);

			await conexion.CreateTableAsync<IncidenciaLocal>();
			await conexion.CreateTableAsync<EvidenciaLocal>();
			await conexion.CreateTableAsync<IntentoSincronizacion>();
			await conexion.CreateTableAsync<EventoAuditoriaLocal>();
			await conexion.CreateTableAsync<TipoIncidenciaLocal>();
			await conexion.CreateTableAsync<SeveridadLocal>();
			await conexion.CreateTableAsync<AfectacionLocal>();
			await conexion.CreateTableAsync<CuerpoLocal>();
			await conexion.CreateTableAsync<CatalogoMetaLocal>();
			await conexion.CreateTableAsync<SesionLocal>();

			await MigrarAsync(conexion);

			_inicializada = true;
		}
		finally
		{
			_cerrojoInicializacion.Release();
		}
	}

	/// <inheritdoc />
	public async Task<int> ObtenerVersionEsquemaAsync(CancellationToken cancelacion = default)
	{
		await InicializarAsync(cancelacion);
		return await AbrirConexion().ExecuteScalarAsync<int>("PRAGMA user_version;");
	}

	/// <summary>
	/// Conexión compartida, ya abierta. Uso interno de los repositorios.
	/// </summary>
	internal SQLiteAsyncConnection Conexion => AbrirConexion();

	/// <summary>
	/// Garantiza que la base está lista antes de cualquier consulta de un repositorio.
	/// </summary>
	internal async Task<SQLiteAsyncConnection> ObtenerConexionListaAsync(CancellationToken cancelacion)
	{
		await InicializarAsync(cancelacion);
		return AbrirConexion();
	}

	/// <summary>
	/// Cifra en el sitio una base que quedó en claro de una versión anterior (JTT-1388).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Una base sin cifrar no se abre con clave</b>, así que al actualizar la app habría dos
	/// salidas: migrarla o descartarla. Descartarla se llevaría por delante las incidencias
	/// pendientes de quien tuviera la app instalada, y conservarlas es justo lo que exigen los
	/// criterios 8 y 11 de esta misma historia. Por eso se migra.
	/// </para>
	/// <para>
	/// El traslado lo hace <c>sqlcipher_export</c>, que copia esquema y datos a una base
	/// adjunta con su propia clave. Es la vía que documenta SQLCipher para esto; recorrer las
	/// tablas a mano habría que actualizarlo cada vez que se agregue una.
	/// </para>
	/// <para>
	/// El archivo original se sustituye solo cuando la copia terminó bien. Si algo falla a
	/// medias, queda la base en claro intacta y el temporal se borra: es preferible arrancar
	/// otra vez sin cifrar que quedarse sin los pendientes.
	/// </para>
	/// <para>
	/// Queda una rendija que el <c>try</c> no cubre: entre borrar el original y mover el cifrado
	/// a su sitio son dos llamadas, y si la app muere justo ahí no hay archivo en la ruta de la
	/// base. El arranque siguiente lo repara adoptando el temporal, que para entonces es la base
	/// buena. Sin eso se crearía una base nueva y vacía y los pendientes quedarían en un archivo
	/// huérfano que nadie vuelve a mirar.
	/// </para>
	/// </remarks>
	private void CifrarBaseEnClaroSiHace()
	{
		if (_clave is null)
		{
			return;
		}

		var temporal = RutaArchivo + ".cifrando";

		if (!File.Exists(RutaArchivo))
		{
			// Solo puede haber temporal sin base si la migración anterior se cortó después de
			// exportar; es la copia cifrada completa, así que ocupa el lugar del original.
			if (File.Exists(temporal))
			{
				File.Move(temporal, RutaArchivo);
			}

			return;
		}

		if (EstaCifrada())
		{
			return;
		}

		File.Delete(temporal);

		try
		{
			using (var enClaro = new SQLiteConnection(RutaArchivo, Banderas))
			{
				// sqlcipher_export traslada esquema y datos, pero no los pragmas del archivo:
				// la base cifrada nacería en la versión 0 y MigrarAsync la trataría como si
				// viniera de antes de la primera versión publicada.
				var version = enClaro.ExecuteScalar<int>("PRAGMA user_version;");

				// El literal va entre comillas simples y con las internas duplicadas: la clave
				// es Base64 y no las lleva, pero no se deja abierta la puerta.
				var claveSql = _clave.Replace("'", "''");
				enClaro.Execute($"ATTACH DATABASE '{temporal.Replace("'", "''")}' AS cifrada KEY '{claveSql}';");
				enClaro.ExecuteScalar<string>("SELECT sqlcipher_export('cifrada');");
				enClaro.Execute($"PRAGMA cifrada.user_version = {version};");
				enClaro.Execute("DETACH DATABASE cifrada;");
			}

			File.Delete(RutaArchivo);
			File.Move(temporal, RutaArchivo);
		}
		catch
		{
			// Sin cifrar y con los datos es mejor que cifrada y a medias.
			File.Delete(temporal);
			throw;
		}
	}

	/// <summary>
	/// Indica si el archivo ya está cifrado.
	/// </summary>
	/// <remarks>
	/// Se comprueba abriéndolo en claro y pidiéndole algo: una base cifrada no se deja leer sin
	/// clave y responde «file is not a database». No hay una forma más directa, porque SQLCipher
	/// cifra también la cabecera del archivo, que es lo que permitiría reconocerlo de un vistazo.
	/// </remarks>
	private bool EstaCifrada()
	{
		try
		{
			using var enClaro = new SQLiteConnection(RutaArchivo, SQLiteOpenFlags.ReadOnly);
			enClaro.ExecuteScalar<int>("PRAGMA user_version;");
			return false;
		}
		catch (SQLiteException)
		{
			return true;
		}
	}

	/// <summary>
	/// Abre la conexión, cifrada si hay clave.
	/// </summary>
	/// <remarks>
	/// La clave viaja en la cadena de conexión y SQLCipher la aplica como <c>PRAGMA key</c> al
	/// abrir. <c>storeDateTimeAsTicks</c> se declara explícito para no depender del valor por
	/// omisión del paquete, que cambió entre versiones.
	/// </remarks>
	private SQLiteAsyncConnection AbrirConexion() =>
		_conexion ??= new SQLiteAsyncConnection(
			new SQLiteConnectionString(RutaArchivo, Banderas, storeDateTimeAsTicks: true, key: _clave));

	/// <summary>
	/// Lleva el archivo desde la versión que tenga hasta <see cref="VersionEsquemaActual"/>.
	/// </summary>
	/// <remarks>
	/// <c>CreateTableAsync</c> ya agrega columnas nuevas a tablas existentes, así que los
	/// cambios aditivos no necesitan paso propio. Este método existe para los que sí lo
	/// necesitan: renombrar, borrar o rellenar datos. Hoy solo sella la versión inicial.
	/// </remarks>
	private static async Task MigrarAsync(SQLiteAsyncConnection conexion)
	{
		var version = await conexion.ExecuteScalarAsync<int>("PRAGMA user_version;");

		if (version >= VersionEsquemaActual)
		{
			return;
		}

		// De 0 a 1: primera versión publicada.
		// De 1 a 2: sesión persistida y sello de origen de las incidencias (JTT-1383).
		// De 2 a 3: permiso con el que se autorizó la captura (JTT-1385 CA 7).
		// De 3 a 4: catálogo real de Jacob (JTT-1394). Su parte destructiva la hace
		//           PrepararEsquemaAsync antes de crear las tablas; lo que queda —las columnas
		//           de severidad y la versión de catálogo en incidencia_local, y las tablas de
		//           severidades, afectaciones, cuerpos y meta— es aditivo y CreateTableAsync ya
		//           lo aplicó arriba.
		//
		// Las incidencias capturadas antes se conservan (JTT-1388 CA 8). Quedan con la
		// severidad vacía y sin versión de catálogo: no se puede reconstruir con qué se
		// capturaron, igual que pasó con el permiso al pasar de 2 a 3.
		await conexion.ExecuteAsync($"PRAGMA user_version = {VersionEsquemaActual};");
	}

	/// <summary>
	/// Aplica los cambios de esquema que <c>CreateTableAsync</c> no sabe hacer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>CreateTableAsync</c> solo <b>agrega</b> columnas que falten. No cambia una llave
	/// primaria ni quita columnas, así que aplicarlo sobre una tabla cuya forma cambió deja un
	/// híbrido de las dos versiones. Este método corre <b>antes</b> para dejar el archivo en un
	/// estado sobre el que crear sea seguro.
	/// </para>
	/// <para>
	/// Solo toca tablas que son <b>caché reconstruible</b>. Nada de lo que el operador capturó
	/// se borra aquí: eso lo prohíbe JTT-1388 CA 8.
	/// </para>
	/// </remarks>
	private static async Task PrepararEsquemaAsync(SQLiteAsyncConnection conexion)
	{
		var version = await conexion.ExecuteScalarAsync<int>("PRAGMA user_version;");

		if (version >= VersionEsquemaActual)
		{
			return;
		}

		// De 3 a 4 (JTT-1394): el catálogo de tipos cambió de llave, de una clave de texto
		// inventada en la maqueta al entero de Jacob. No se puede migrar fila por fila porque
		// no hay correspondencia: los seis tipos sembrados —OBJETO, VEHICULO, ACCIDENTE,
		// ANIMAL, SENALAMIENTO, OTRO— no existen en ningún servidor. Se tira la tabla y se
		// vuelve a crear vacía; la llena la primera descarga del catálogo real.
		//
		// Que quede vacía hasta esa descarga es a propósito: un formulario sin tipos avisa de
		// que falta bajar el catálogo, mientras que uno con seis tipos falsos deja capturar
		// incidencias que Jacob va a rechazar.
		await conexion.ExecuteAsync("DROP TABLE IF EXISTS catalogo_tipo_incidencia;");
	}

	public async ValueTask DisposeAsync()
	{
		if (_conexion is not null)
		{
			await _conexion.CloseAsync();
			_conexion = null;
		}

		_cerrojoInicializacion.Dispose();
	}
}
