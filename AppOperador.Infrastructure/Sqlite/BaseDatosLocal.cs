using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Sqlite.Esquema;
using Microsoft.Maui.Storage;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

// Singleton: varias conexiones al mismo archivo invitan a bloqueos. Las pestañas la inicializan a la vez.
public sealed class BaseDatosLocal : ILocalDatabase, IAsyncDisposable
{
	public const int VersionEsquemaActual = EsquemaLocal.Version;

	private const string NombreArchivo = "appoperador.db3";

	// SharedCache + FullMutex: las pestañas leen y escriben desde hilos distintos.
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

	public BaseDatosLocal(string? rutaArchivo = null, IDatabaseKeyProvider? claves = null)
	{
		RutaArchivo = rutaArchivo ?? Path.Combine(FileSystem.AppDataDirectory, NombreArchivo);
		_claves = claves;
	}

	public string RutaArchivo { get; }

	public async Task InicializarAsync(CancellationToken cancelacion = default)
	{
		if (_inicializada)
		{
			return;
		}

		await _cerrojoInicializacion.WaitAsync(cancelacion);
		try
		{
			// Otra pestaña pudo inicializar mientras esperábamos.
			if (_inicializada)
			{
				return;
			}

			// La clave va antes de abrir, y antes hay que cifrar una base que venga en claro.
			_clave = _claves is null ? null : await _claves.ObtenerAsync(cancelacion);
			CifrarBaseEnClaroSiHace();

			// Conexión propia y en otro hilo: reconstruir tablas apaga y enciende las llaves foráneas.
			var migrador = new MigradorEsquema(RutaArchivo, Banderas, _clave);
			await Task.Run(migrador.Aplicar, cancelacion);

			// SQLite no verifica las llaves por omisión; es por conexión.
			await AbrirConexion().ExecuteAsync("PRAGMA foreign_keys = ON;");

			_inicializada = true;
		}
		finally
		{
			_cerrojoInicializacion.Release();
		}
	}

	public async Task<int> ObtenerVersionEsquemaAsync(CancellationToken cancelacion = default)
	{
		await InicializarAsync(cancelacion);
		return await AbrirConexion().ExecuteScalarAsync<int>("PRAGMA user_version;");
	}

	internal async Task<SQLiteAsyncConnection> ObtenerConexionListaAsync(CancellationToken cancelacion)
	{
		await InicializarAsync(cancelacion);
		return AbrirConexion();
	}

	// Descartar la base en claro perdería los pendientes. El original se sustituye solo si la copia terminó.
	private void CifrarBaseEnClaroSiHace()
	{
		if (_clave is null)
		{
			return;
		}

		var temporal = RutaArchivo + ".cifrando";

		if (!File.Exists(RutaArchivo))
		{
			// Temporal sin base: la migración anterior se cortó y el temporal es la copia completa.
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
				// sqlcipher_export no copia los pragmas: la versión se lleva a mano.
				var version = enClaro.ExecuteScalar<int>("PRAGMA user_version;");

				// La clave es Base64 y no trae comillas, pero se escapan igual.
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

	// SQLCipher cifra también la cabecera: la única prueba es intentar leerla sin clave.
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

	// storeDateTimeAsTicks explícito: el valor por omisión cambió entre versiones.
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
