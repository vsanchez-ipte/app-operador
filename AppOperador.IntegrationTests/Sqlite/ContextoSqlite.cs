using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Base de datos SQLite real sobre un archivo temporal, con dobles para el resto.
/// </summary>
/// <remarks>
/// Cada prueba trabaja contra su propio archivo, así que pueden correr en paralelo sin
/// pisarse. El archivo se borra al terminar.
///
/// La ruta se pasa explícitamente al constructor de <see cref="BaseDatosLocal"/>: en el
/// destino <c>net10.0</c> no hay dispositivo y <c>FileSystem.AppDataDirectory</c>
/// lanzaría. Ese parámetro existe justamente para poder probar esto fuera del emulador.
/// </remarks>
public sealed class ContextoSqlite : IAsyncDisposable
{
	private readonly string _ruta;

	public ContextoSqlite()
	{
		_ruta = Path.Combine(Path.GetTempPath(), $"appoperador-pruebas-{Guid.NewGuid():N}.db3");

		Reloj = new RelojFijo();
		Conectividad = new ConectividadControlada();
		Sesion = new SesionFija();
		BaseDatos = new BaseDatosLocal(_ruta);
		Bitacora = new BitacoraAuditoriaSqlite(BaseDatos, Reloj);
	}

	public string Ruta => _ruta;

	public RelojFijo Reloj { get; }

	public ConectividadControlada Conectividad { get; }

	public SesionFija Sesion { get; }

	public BaseDatosLocal BaseDatos { get; }

	public BitacoraAuditoriaSqlite Bitacora { get; }

	public RepositorioIncidenciasSqlite CrearRepositorio() => new(BaseDatos, Reloj, Sesion);

	public ColaSincronizacionSqlite CrearCola() => new(BaseDatos, Reloj, Conectividad, Bitacora);

	/// <summary>
	/// Abre una instancia nueva sobre el mismo archivo, como si la app se hubiera reiniciado.
	/// </summary>
	public BaseDatosLocal ReabrirBaseDatos() => new(_ruta);

	public async ValueTask DisposeAsync()
	{
		await BaseDatos.DisposeAsync();

		// SQLite puede tardar en soltar el archivo; si no se puede borrar no vale la pena
		// tumbar la prueba por un temporal huérfano.
		try
		{
			if (File.Exists(_ruta))
			{
				File.Delete(_ruta);
			}
		}
		catch (IOException)
		{
		}
	}
}

/// <summary>Reloj controlable: las pruebas deciden qué hora es.</summary>
public sealed class RelojFijo : IClock
{
	public DateTime UtcAhora { get; set; } = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

	public void Avanzar(TimeSpan cuanto) => UtcAhora = UtcAhora.Add(cuanto);
}

/// <summary>Conectividad que la prueba enciende y apaga a voluntad.</summary>
public sealed class ConectividadControlada : IConnectivityService
{
	private bool _hayEnlace = true;

	public bool HayEnlace
	{
		get => _hayEnlace;
		set
		{
			_hayEnlace = value;
			EnlaceCambio?.Invoke(this, value);
		}
	}

	public event EventHandler<bool>? EnlaceCambio;
}

/// <summary>Sesión abierta fija, para que las incidencias tengan operador y unidad.</summary>
public sealed class SesionFija : ISessionStore
{
	public SesionOperador? Actual { get; private set; } = new(
		"admin",
		"Operador",
		"VEH-01",
		VigenciaOffline.Validada(new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc)),
		["CAPTURA", "SYNC"],
		"1.2.0",
		new DateOnly(2026, 7, 23));

	public void Guardar(SesionOperador sesion) => Actual = sesion;

	public void Limpiar() => Actual = null;
}
