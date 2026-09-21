using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Sqlite.Esquema;
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
	/// Versión de esquema que este código espera. Se guarda en el <c>PRAGMA user_version</c>
	/// del archivo; las tablas están descritas en <see cref="EsquemaLocal"/>.
	/// </summary>
	public const int VersionEsquemaActual = EsquemaLocal.Version;

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

			// El esquema se prepara con una conexión propia y síncrona, aparte de la que la app
			// va a usar: reconstruir tablas apaga y enciende las llaves foráneas, y eso no debe
			// mezclarse con la conexión compartida. En un hilo aparte porque las pestañas lo
			// piden desde la interfaz y una migración con datos tarda lo que tarda.
			var migrador = new MigradorEsquema(RutaArchivo, Banderas, _clave);
			await Task.Run(migrador.Aplicar, cancelacion);

			// Sin esto las llaves foráneas son decorativas: SQLite no las verifica por omisión y
			// sqlite-net no las enciende. Es por conexión, y esta es la única que usa la app.
			await AbrirConexion().ExecuteAsync("PRAGMA foreign_keys = ON;");

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
