using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
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

	public ColaSincronizacionSqlite CrearCola() => new(BaseDatos, Reloj, Sesion);

	/// <summary>Copia local del catálogo, de donde salen los campos que se rellenan al enviar.</summary>
	public RepositorioCatalogoSqlite CrearCatalogo() => new(BaseDatos);

	/// <summary>
	/// El caso de uso del envío, armado sobre la base real y un Jacob controlable.
	/// </summary>
	/// <remarks>
	/// Desde JTT-1401 la orquestación no vive en la cola, así que las pruebas de comportamiento
	/// del envío entran por aquí. Siguen tocando SQLite de verdad: lo que se comprueba es que
	/// los estados y el folio queden <b>escritos</b>, no solo decididos.
	/// </remarks>
	public SincronizarIncidencias CrearSincronizador(
		JacobControlado? jacob = null,
		ISessionStore? sesion = null,
		CapacidadesDeLaSesion? capacidades = null)
	{
		var deQuien = sesion ?? Sesion;

		return new SincronizarIncidencias(
			new ColaSincronizacionSqlite(BaseDatos, Reloj, deQuien),
			jacob ?? new JacobControlado(),
			Conectividad,
			new TokenFijo(),
			capacidades ?? new CapacidadesDeLaSesion(deQuien),
			Reloj,
			Bitacora,
			CrearCatalogo());
	}

	/// <summary>
	/// Cola vista por otra sesión, para comprobar que no se ve la cola ajena (JTT-1390 CA 7).
	/// </summary>
	public ColaSincronizacionSqlite CrearColaDe(ISessionStore sesion) =>
		new(BaseDatos, Reloj, sesion);

	/// <summary>
	/// Repositorio visto por otra sesión, para los borradores ajenos (JTT-1388 CA 9).
	/// </summary>
	public RepositorioIncidenciasSqlite CrearRepositorioDe(ISessionStore sesion) =>
		new(BaseDatos, Reloj, sesion);

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

	/// <summary>La prueba fija el estado a mano: comprobar solo devuelve lo que hay.</summary>
	public Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(HayEnlace
			? ResultadoSondeo.Alcanzado()
			: ResultadoSondeo.SinTransporte("Enlace apagado por la prueba."));
}

/// <summary>Sesión abierta fija, para que las incidencias tengan operador y unidad.</summary>
public sealed class SesionFija : ISessionStore
{
	public SesionFija(string operador = "admin")
	{
		Actual = De(operador);
	}

	public SesionOperador? Actual { get; private set; }

	public void Guardar(SesionOperador sesion) => Actual = sesion;

	public void Limpiar() => Actual = null;

	private static SesionOperador De(string operador) => new(
		operador,
		"Operador",
		"VEH-01",
		VigenciaOffline.Validada(new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc)),
		// Los códigos reales que emite Jacob, no los del simulador retirado en JTT-1385:
		// "CAPTURA" y "SYNC" no existen en el servidor, y desde que el envío consulta
		// capacidades una sesión con esos códigos no autorizaría nada.
		PermisosOperador.DelServidor([
			ReglaCapacidades.PermisoAppOperadorMovil,
			ReglaCapacidades.PermisoCapturaIncidencias,
		]),
		"1.2.0",
		new DateOnly(2026, 7, 23));
}

/// <summary>
/// Jacob controlable: la prueba decide si acepta, y con qué error rechaza.
/// </summary>
/// <remarks>
/// Sustituye al simulador que vivía dentro de la cola y devolvía siempre un folio inventado.
/// Aquél no permitía probar ningún rechazo, que es la mitad de JTT-1401.
/// </remarks>
public sealed class JacobControlado : IIncidenciasJacobClient
{
	private readonly Queue<ResultadoEnvio> _programados = new();

	/// <summary>Respuesta por omisión cuando no queda ninguna programada.</summary>
	public ResultadoEnvio PorOmision { get; set; } =
		ResultadoEnvio.Aceptada(new IncidenciaRegistrada("INC-APK-2026-0001", new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc), false));

	/// <summary>Envíos recibidos, en orden, para poder afirmar qué se mandó.</summary>
	public List<EnvioIncidencia> Recibidos { get; } = [];

	/// <summary>Programa la siguiente respuesta. Se consumen en orden.</summary>
	public JacobControlado Responde(ResultadoEnvio resultado)
	{
		_programados.Enqueue(resultado);
		return this;
	}

	/// <summary>Programa un rechazo funcional con el código indicado.</summary>
	public JacobControlado RechazaFuncional(string codigo = "appincidencias.nota.requerida") =>
		Responde(ResultadoEnvio.Rechazada(FamiliaErrorSincronizacion.Funcional, codigo, "Rechazo de prueba."));

	/// <summary>Programa un rechazo técnico.</summary>
	public JacobControlado RechazaTecnico(string codigo = "appincidencias.error.tecnico") =>
		Responde(ResultadoEnvio.Rechazada(FamiliaErrorSincronizacion.Tecnico, codigo, "Fallo de prueba."));

	public Task<ResultadoEnvio> RegistrarAsync(
		EnvioIncidencia incidencia,
		string accessToken,
		CancellationToken cancelacion = default)
	{
		Recibidos.Add(incidencia);

		var resultado = _programados.Count > 0 ? _programados.Dequeue() : PorOmision;
		return Task.FromResult(resultado);
	}
}

/// <summary>Token siempre presente: la ausencia de token se prueba aparte.</summary>
public sealed class TokenFijo : ITokenProvider
{
	public Task<string?> ObtenerAsync(CancellationToken cancelacion = default) =>
		Task.FromResult<string?>("token-de-prueba");

	public Task GuardarAsync(string accessToken, CancellationToken cancelacion = default) =>
		Task.CompletedTask;

	public Task LimpiarAsync(CancellationToken cancelacion = default) => Task.CompletedTask;
}
