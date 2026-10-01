using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

// Un archivo por prueba, para correr en paralelo; la ruta explícita evita FileSystem, que en net10.0 lanza.
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
		Monotonico = new MonotonicoFijo();
		Bitacora = CrearBitacora(BaseDatos);
	}

	public BitacoraAuditoriaSqlite CrearBitacora(BaseDatosLocal baseDatos) =>
		new(baseDatos, Reloj, Monotonico, Sesion, Conectividad);

	public BitacoraAuditoriaSqlite CrearBitacoraDe(ISessionStore sesion) =>
		new(BaseDatos, Reloj, Monotonico, sesion, Conectividad);

	public MonotonicoFijo Monotonico { get; }

	public string Ruta => _ruta;

	public RelojFijo Reloj { get; }

	public ConectividadControlada Conectividad { get; }

	public SesionFija Sesion { get; }

	public BaseDatosLocal BaseDatos { get; }

	public BitacoraAuditoriaSqlite Bitacora { get; }

	public RepositorioIncidenciasSqlite CrearRepositorio() => new(BaseDatos, Reloj, Sesion);

	public ColaSincronizacionSqlite CrearCola() => new(BaseDatos, Reloj, Sesion);

	public RepositorioCatalogoSqlite CrearCatalogo() => new(BaseDatos);

	public RepositorioEvidenciasSqlite CrearRepositorioEvidencias() => new(BaseDatos);

	// Por el caso de uso, para comprobar que estados y folio quedan escritos y no solo decididos.
	public SincronizarIncidencias CrearSincronizador(
		JacobControlado? jacob = null,
		ISessionStore? sesion = null,
		CapacidadesDeLaSesion? capacidades = null,
		EvidenciasControladas? jacobEvidencias = null)
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
			CrearCatalogo(),
			CrearRepositorioEvidencias(),
			jacobEvidencias ?? new EvidenciasControladas(),
			deQuien);
	}

	public ColaSincronizacionSqlite CrearColaDe(ISessionStore sesion) =>
		new(BaseDatos, Reloj, sesion);

	public RepositorioIncidenciasSqlite CrearRepositorioDe(ISessionStore sesion) =>
		new(BaseDatos, Reloj, sesion);

	public BaseDatosLocal ReabrirBaseDatos() => new(_ruta);

	// Desde el esquema 11 no puede haber evidencia sin su incidencia.
	public Task SembrarIncidenciaAsync(string uuid) =>
		EjecutarSqlAsync(
			"INSERT INTO incidencia_local (uuid, clave_local, estado, creado_utc_ticks, actualizado_utc_ticks) " +
			"VALUES (?, ?, ?, ?, ?)",
			uuid, "LOC-" + uuid[..6], (int)EstadoSincronizacion.Pendiente, Reloj.UtcAhora.Ticks, Reloj.UtcAhora.Ticks);

	// Para preparar filas que la app ya no escribe, como las de un esquema anterior.
	public async Task EjecutarSqlAsync(string sql, params object[] args)
	{
		var conexion = new SQLite.SQLiteAsyncConnection(
			new SQLite.SQLiteConnectionString(_ruta, storeDateTimeAsTicks: true));
		try
		{
			await conexion.ExecuteAsync(sql, args);
		}
		finally
		{
			await conexion.CloseAsync();
		}
	}

	public async Task<T> EscalarAsync<T>(string sql, params object[] args)
	{
		var conexion = new SQLite.SQLiteAsyncConnection(
			new SQLite.SQLiteConnectionString(_ruta, storeDateTimeAsTicks: true));
		try
		{
			return await conexion.ExecuteScalarAsync<T>(sql, args);
		}
		finally
		{
			await conexion.CloseAsync();
		}
	}

	public async Task<List<T>> ConsultarAsync<T>(string sql, params object[] args) where T : new()
	{
		var conexion = new SQLite.SQLiteAsyncConnection(
			new SQLite.SQLiteConnectionString(_ruta, storeDateTimeAsTicks: true));
		try
		{
			return await conexion.QueryAsync<T>(sql, args);
		}
		finally
		{
			await conexion.CloseAsync();
		}
	}

	public async ValueTask DisposeAsync()
	{
		await BaseDatos.DisposeAsync();

		// SQLite puede tardar en soltar el archivo; un temporal huérfano no tumba la prueba.
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

public sealed class RelojFijo : IClock
{
	public DateTime UtcAhora { get; set; } = new(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc);

	public void Avanzar(TimeSpan cuanto) => UtcAhora = UtcAhora.Add(cuanto);
}

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

	public Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(HayEnlace
			? ResultadoSondeo.Alcanzado()
			: ResultadoSondeo.SinTransporte("Enlace apagado por la prueba."));

	public void AnotarIntercambio(bool jacobRespondio)
	{
	}
}

public sealed class SesionFija : ISessionStore
{
	public SesionFija(string operador = "admin", string sessionId = "")
	{
		Actual = De(operador, sessionId);
	}

	public SesionOperador? Actual { get; private set; }

	public void Guardar(SesionOperador sesion) => Actual = sesion;

	public void Limpiar() => Actual = null;

	private static SesionOperador De(string operador, string sessionId) => new(
		operador,
		"Operador",
		"VEH-01",
		VigenciaOffline.Validada(new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc)),
		// Los códigos reales de Jacob: con otros, la sesión no autorizaría nada.
		PermisosOperador.DelServidor([
			ReglaCapacidades.PermisoAppOperadorMovil,
			ReglaCapacidades.PermisoCapturaIncidencias,
		]),
		"1.2.0",
		new DateOnly(2026, 7, 23),
		sessionId);
}

public sealed class JacobControlado : IIncidenciasJacobClient
{
	private readonly Queue<ResultadoEnvio> _programados = new();

	public ResultadoEnvio PorOmision { get; set; } =
		ResultadoEnvio.Aceptada(new IncidenciaRegistrada("INC-APK-2026-0001", new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc), false));

	public List<EnvioIncidencia> Recibidos { get; } = [];

	public JacobControlado Responde(ResultadoEnvio resultado)
	{
		_programados.Enqueue(resultado);
		return this;
	}

	public JacobControlado RechazaFuncional(string codigo = "appincidencias.nota.requerida") =>
		Responde(ResultadoEnvio.Rechazada(FamiliaErrorSincronizacion.Funcional, codigo, "Rechazo de prueba."));

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

// La ausencia de token se prueba aparte.
public sealed class TokenFijo : ITokenProvider
{
	public Task<string?> ObtenerAsync(CancellationToken cancelacion = default) =>
		Task.FromResult<string?>("token-de-prueba");

	public Task GuardarAsync(string accessToken, CancellationToken cancelacion = default) =>
		Task.CompletedTask;

	public Task LimpiarAsync(CancellationToken cancelacion = default) => Task.CompletedTask;
}

// Acepta todo por omisión: las pruebas de incidencias no deben fallar por las evidencias.
public sealed class EvidenciasControladas : IEvidenciasJacobClient
{
	private readonly Queue<ResultadoEnvioEvidencia> _programados = new();

	public List<(string Incidencia, string Ruta)> Recibidas { get; } = [];

	public EvidenciasControladas Responde(ResultadoEnvioEvidencia resultado)
	{
		_programados.Enqueue(resultado);
		return this;
	}

	public Task<ResultadoEnvioEvidencia> SubirAsync(
		string incidenciaUuid,
		string rutaArchivo,
		string nombreOriginal,
		string accessToken,
		CancellationToken cancelacion = default)
	{
		Recibidas.Add((incidenciaUuid, rutaArchivo));

		return Task.FromResult(_programados.Count > 0
			? _programados.Dequeue()
			: ResultadoEnvioEvidencia.Aceptada(
				new EvidenciaRegistrada(Guid.NewGuid().ToString(), "image/jpeg", "hash", false)));
	}
}

public sealed class MonotonicoFijo : IMonotonicClock
{
	public TimeSpan Transcurrido { get; set; } = TimeSpan.FromHours(1);
}
