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
	public const int VersionEsquemaActual = 1;

	private const string NombreArchivo = "appoperador.db3";

	// ReadWrite|Create: la app crea el archivo la primera vez.
	// SharedCache + FullMutex: varias pestañas leen y escriben desde hilos distintos.
	private const SQLiteOpenFlags Banderas =
		SQLiteOpenFlags.ReadWrite |
		SQLiteOpenFlags.Create |
		SQLiteOpenFlags.SharedCache |
		SQLiteOpenFlags.FullMutex;

	private readonly SemaphoreSlim _cerrojoInicializacion = new(1, 1);
	private SQLiteAsyncConnection? _conexion;
	private bool _inicializada;

	public BaseDatosLocal(string? rutaArchivo = null)
	{
		RutaArchivo = rutaArchivo ?? Path.Combine(FileSystem.AppDataDirectory, NombreArchivo);
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

			var conexion = AbrirConexion();

			await conexion.CreateTableAsync<IncidenciaLocal>();
			await conexion.CreateTableAsync<EvidenciaLocal>();
			await conexion.CreateTableAsync<IntentoSincronizacion>();
			await conexion.CreateTableAsync<EventoAuditoriaLocal>();
			await conexion.CreateTableAsync<TipoIncidenciaLocal>();

			await MigrarAsync(conexion);
			await SembrarCatalogoAsync(conexion);

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

	private SQLiteAsyncConnection AbrirConexion() =>
		_conexion ??= new SQLiteAsyncConnection(RutaArchivo, Banderas);

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

		// De 0 a 1: primera versión publicada. Las tablas ya quedaron creadas arriba.
		await conexion.ExecuteAsync($"PRAGMA user_version = {VersionEsquemaActual};");
	}

	/// <summary>
	/// Siembra el catálogo de tipos si la tabla está vacía.
	/// </summary>
	/// <remarks>
	/// Provisional: el catálogo autorizado lo entregará Jacob (JTT-1347). Mientras tanto
	/// se usan los tipos de la maqueta para que la captura funcione sin conexión.
	/// Solo siembra cuando la tabla está vacía, así una sincronización futura de
	/// catálogos no se pisa con estos valores.
	/// </remarks>
	private static async Task SembrarCatalogoAsync(SQLiteAsyncConnection conexion)
	{
		if (await conexion.Table<TipoIncidenciaLocal>().CountAsync() > 0)
		{
			return;
		}

		await conexion.InsertAllAsync(new[]
		{
			new TipoIncidenciaLocal { Clave = "OBJETO", Nombre = "Objeto en camino", Orden = 1 },
			new TipoIncidenciaLocal { Clave = "VEHICULO", Nombre = "Vehiculo detenido", Orden = 2 },
			new TipoIncidenciaLocal { Clave = "ACCIDENTE", Nombre = "Accidente", Orden = 3 },
			new TipoIncidenciaLocal { Clave = "ANIMAL", Nombre = "Animal en camino", Orden = 4 },
			new TipoIncidenciaLocal { Clave = "SENALAMIENTO", Nombre = "Senalamiento danado", Orden = 5 },
			new TipoIncidenciaLocal { Clave = "OTRO", Nombre = "Otro", ExigeDescripcion = true, Orden = 6 },
		});
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
