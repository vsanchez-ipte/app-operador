using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Orquestación del envío de incidencias (JTT-1401).
/// </summary>
/// <remarks>
/// Lo que se prueba aquí son <b>las decisiones</b>: cuándo se intenta, qué se reintenta y qué se
/// queda esperando. Que la fila quede escrita en la base se prueba contra SQLite, en
/// <c>ColaSincronizacionSqliteTests</c>.
/// </remarks>
public sealed class SincronizarIncidenciasTests
{
	private static readonly DateTime Ahora = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

	private readonly RelojControlado _reloj = new();
	private readonly ColaFalsa _cola = new();
	private readonly JacobFalso _jacob = new();
	private readonly ConectividadFalsa _conectividad = new();
	private readonly SesionFalsa _sesion = new();

	private SincronizarIncidencias Crear() => new(
		_cola,
		_jacob,
		_conectividad,
		new TokenFalso(),
		new CapacidadesDeLaSesion(_sesion),
		_reloj,
		new BitacoraNula(),
		new CatalogoFalso());

	// ── CA 7: la espera creciente ─────────────────────────────────────────────────────

	[Fact]
	public async Task UnFalloTecnicoNoSeReintentaAntesDeQueVenzaLaEspera()
	{
		_cola.Encolar(Fallida(intentos: 1, ultimoIntento: Ahora, codigo: "appincidencias.error.tecnico"));

		// Han pasado 30 segundos; la espera del primer fallo es de un minuto.
		_reloj.UtcAhora = Ahora.AddSeconds(30);

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(0, resultado.Intentados);
		Assert.Empty(_jacob.Recibidos);
	}

	[Fact]
	public async Task AlVencerLaEsperaElFalloTecnicoSeReintenta()
	{
		_cola.Encolar(Fallida(intentos: 1, ultimoIntento: Ahora, codigo: "appincidencias.error.tecnico"));
		_reloj.UtcAhora = Ahora.AddMinutes(1);

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(1, resultado.Intentados);
		Assert.Single(_jacob.Recibidos);
	}

	[Fact]
	public async Task LaEsperaCreceConLosIntentos()
	{
		// Cuatro fallos previos exigen ocho minutos; a los cinco todavía no toca.
		_cola.Encolar(Fallida(intentos: 4, ultimoIntento: Ahora, codigo: "appincidencias.error.tecnico"));
		_reloj.UtcAhora = Ahora.AddMinutes(5);

		Assert.Equal(0, (await Crear().EjecutarAsync()).Intentados);

		_reloj.UtcAhora = Ahora.AddMinutes(8);

		Assert.Equal(1, (await Crear().EjecutarAsync()).Intentados);
	}

	[Fact]
	public async Task LaEsperaNoCreceMasAllaDelTope()
	{
		// Una racha larguísima no puede dejar la siguiente espera en más de treinta minutos, o
		// al recuperar la señal lo capturado no saldría hasta la jornada siguiente.
		_cola.Encolar(Fallida(intentos: 50, ultimoIntento: Ahora, codigo: "appincidencias.error.tecnico"));
		_reloj.UtcAhora = Ahora.Add(ReglaEsperaReintento.EsperaMaxima);

		Assert.Equal(1, (await Crear().EjecutarAsync()).Intentados);
	}

	// ── CA 8: lo funcional no se reintenta ────────────────────────────────────────────

	[Fact]
	public async Task UnRechazoFuncionalNoSeReintentaPorMuchoQuePaseElTiempo()
	{
		_cola.Encolar(Fallida(intentos: 1, ultimoIntento: Ahora, codigo: "appincidencias.nota.requerida"));

		// Una semana después sigue sin tocarse: reenviarlo igual daría el mismo rechazo.
		_reloj.UtcAhora = Ahora.AddDays(7);

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(0, resultado.Intentados);
		Assert.Empty(_jacob.Recibidos);
	}

	[Fact]
	public async Task UnRechazoFuncionalConservaElRegistro()
	{
		// JTT-1404 CA 4: «funcional» significa que deja de reintentarse, NO que se descarte.
		_cola.Encolar(Pendiente());
		_jacob.Responde(ResultadoEnvio.Rechazada(
			FamiliaErrorSincronizacion.Funcional, "appincidencias.permiso.revocado", "Sin permiso."));

		await Crear().EjecutarAsync();

		Assert.Contains(_cola.Actualizaciones, a => a.Estado == EstadoSincronizacion.Fallido);
		Assert.DoesNotContain(_cola.Actualizaciones, a => a.Estado == EstadoSincronizacion.Sincronizado);
		// El código queda guardado: es lo que hace que no se reintente tras reabrir la app.
		Assert.Contains(_cola.Actualizaciones, a => a.UltimoErrorCodigo == "appincidencias.permiso.revocado");
	}

	[Fact]
	public async Task UnCodigoDesconocidoSeReintentaComoTecnico()
	{
		_cola.Encolar(Fallida(intentos: 1, ultimoIntento: Ahora, codigo: "appincidencias.algo.nuevo"));
		_reloj.UtcAhora = Ahora.AddMinutes(1);

		// Prudente: mejor gastar un reintento acotado que dejar el registro parado para siempre
		// esperando una corrección que nadie sabe que hace falta.
		Assert.Equal(1, (await Crear().EjecutarAsync()).Intentados);
	}

	// ── CA 13: una falla no arrastra a las demás ──────────────────────────────────────

	[Fact]
	public async Task UnRechazoNoDetieneAlResto()
	{
		_cola.Encolar(Pendiente("uuid-1"), Pendiente("uuid-2"), Pendiente("uuid-3"));

		_jacob
			.Responde(ResultadoEnvio.Rechazada(
				FamiliaErrorSincronizacion.Funcional, "appincidencias.nota.requerida", "Falta nota."))
			.Responde(Aceptada("INC-APK-2026-0002"))
			.Responde(Aceptada("INC-APK-2026-0003"));

		var resultado = await Crear().EjecutarAsync();

		// Las tres se intentaron y dos salieron: el primer rechazo no detuvo el recorrido.
		Assert.Equal(3, resultado.Intentados);
		Assert.Equal(2, resultado.Confirmados);
		Assert.Equal(3, _jacob.Recibidos.Count);
	}

	[Fact]
	public async Task CadaRegistroSeResuelveIndividualmente()
	{
		// CA 12: cada uno pasa por Enviando y termina en su estado antes del siguiente.
		_cola.Encolar(Pendiente("uuid-1"), Pendiente("uuid-2"));

		await Crear().EjecutarAsync();

		Assert.Equal(2, _cola.Actualizaciones.Count(a => a.Estado == EstadoSincronizacion.Enviando));
		Assert.Equal(2, _cola.Actualizaciones.Count(a => a.Estado == EstadoSincronizacion.Sincronizado));
	}

	// ── CA 1 y 2: las compuertas ──────────────────────────────────────────────────────

	[Fact]
	public async Task SinEnlaceConJacobNiSiquieraSeIntenta()
	{
		_cola.Encolar(Pendiente());
		_conectividad.HayEnlace = false;

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(MotivoNoSincroniza.SinEnlaceConJacob, resultado.MotivoBloqueo);
		Assert.Empty(_jacob.Recibidos);
	}

	[Fact]
	public async Task SinPermisoDeSincronizarNoSeIntenta()
	{
		_cola.Encolar(Pendiente());
		_sesion.Limpiar();

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(MotivoNoSincroniza.SinPermiso, resultado.MotivoBloqueo);
		Assert.Empty(_jacob.Recibidos);
	}

	// ── El folio ──────────────────────────────────────────────────────────────────────

	[Fact]
	public async Task ElFolioAceptadoSeGuardaEnElRegistro()
	{
		_cola.Encolar(Pendiente());
		_jacob.Responde(Aceptada("INC-APK-2026-0034"));

		await Crear().EjecutarAsync();

		Assert.Contains(_cola.Actualizaciones, a =>
			a.Estado == EstadoSincronizacion.Sincronizado && a.FolioCentral == "INC-APK-2026-0034");
	}

	[Fact]
	public async Task UnReenvioQueYaExistiaCuentaComoConfirmado()
	{
		// yaExistia significa que la respuesta anterior se perdió en la carretera y la app hizo
		// lo correcto al reenviar. Tratarlo como fallo dejaría reintentando algo ya registrado.
		_cola.Encolar(Pendiente());
		_jacob.Responde(ResultadoEnvio.Aceptada(
			new IncidenciaRegistrada("INC-APK-2026-0034", Ahora, YaExistia: true)));

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(1, resultado.Confirmados);
	}

	// ── Envío inmediato al capturar ───────────────────────────────────────────────────

	[Fact]
	public async Task EnviarUna_soloMandaEsaYNoElRestoDeLaCola()
	{
		// El operador está parado en el incidente: hacerle esperar a que suban los registros
		// viejos alarga la captura, y si uno de ellos falla el aviso sobre el suyo se enturbia.
		_cola.Encolar(Pendiente("uuid-viejo"), Pendiente("uuid-nuevo"));

		var resultado = await Crear().EnviarUnaAsync("LOC-000001");

		Assert.Equal(1, resultado.Intentados);
		Assert.Single(_jacob.Recibidos);
	}

	[Fact]
	public async Task EnviarUna_sinEnlaceNoEsUnErrorYLaDejaEnLaCola()
	{
		_cola.Encolar(Pendiente());
		_conectividad.HayEnlace = false;

		var resultado = await Crear().EnviarUnaAsync("LOC-000001");

		// Sin enlace no se intenta, y sobre todo no se toca el registro: sigue pendiente y
		// saldrá por el camino normal de reintentos.
		Assert.Equal(MotivoNoSincroniza.SinEnlaceConJacob, resultado.MotivoBloqueo);
		Assert.Empty(_jacob.Recibidos);
		Assert.Empty(_cola.Actualizaciones);
	}

	[Fact]
	public async Task EnviarUna_aceptadaQuedaSincronizadaConSuFolio()
	{
		_cola.Encolar(Pendiente());
		_jacob.Responde(Aceptada("INC-APK-2026-0034"));

		var resultado = await Crear().EnviarUnaAsync("LOC-000001");

		Assert.Equal(1, resultado.Confirmados);
		Assert.Contains(_cola.Actualizaciones, a =>
			a.Estado == EstadoSincronizacion.Sincronizado && a.FolioCentral == "INC-APK-2026-0034");
	}

	[Fact]
	public async Task EnviarUna_deUnaClaveQueNoExisteNoRompeNada()
	{
		// Pudo eliminarse, ser de otro operador o haber salido ya. Nada que hacer, y no es
		// un error: lo guardado sigue su camino.
		var resultado = await Crear().EnviarUnaAsync("LOC-999999");

		Assert.Equal(0, resultado.Intentados);
		Assert.Null(resultado.MotivoBloqueo);
		Assert.Empty(_jacob.Recibidos);
	}

	// ── El motivo del rechazo llega hasta la pantalla · CA 10 ─────────────────────────

	[Fact]
	public async Task UnFalloTecnicoSeDistingueDeUnRechazoDeJacob()
	{
		// Es el defecto que Victor encontro en el emulador el 21-ago: con el API apagado, la
		// pantalla decia «el CCO no la acepto todavia». Jacob nunca la recibio, y ese texto
		// manda al operador a revisar una captura que esta bien.
		_cola.Encolar(Pendiente());
		_jacob.Responde(ResultadoEnvio.Rechazada(
			FamiliaErrorSincronizacion.Tecnico,
			"app.envio.error.tecnico",
			"No se pudo contactar al CCO. Se reintentará."));

		var resultado = await Crear().EnviarUnaAsync("LOC-000001");

		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.FamiliaUltimoError);
	}

	[Fact]
	public async Task ElMensajeDeJacobLlegaTalCualEnUnRechazoFuncional()
	{
		// Jacob sabe que esta mal mejor que la pantalla: reescribirlo como «no se acepto»
		// obliga al operador a adivinar que corregir.
		_cola.Encolar(Pendiente());
		_jacob.Responde(ResultadoEnvio.Rechazada(
			FamiliaErrorSincronizacion.Funcional,
			"appincidencias.km.fueradecorredor",
			"El kilómetro 119.999 está fuera del corredor (120.000 a 148.000)."));

		var resultado = await Crear().EnviarUnaAsync("LOC-000001");

		Assert.Equal(FamiliaErrorSincronizacion.Funcional, resultado.FamiliaUltimoError);
		Assert.Contains("fuera del corredor", resultado.MensajeUltimoError);
	}

	[Fact]
	public async Task UnEnvioAceptadoNoDejaMotivoDeRechazo()
	{
		_cola.Encolar(Pendiente());

		var resultado = await Crear().EnviarUnaAsync("LOC-000001");

		Assert.Null(resultado.FamiliaUltimoError);
		Assert.Null(resultado.MensajeUltimoError);
	}

	// ── Saltarse un registro se cuenta, y se dice por que ─────────────────────────────

	[Fact]
	public async Task LoQueEsperaSuReintentoSeCuentaAparteDeLoQueNecesitaCorreccion()
	{
		// Es el defecto que Victor vio el 21-ago: la cola decia "3 incidencias pendientes"
		// arriba y "No hay incidencias pendientes de enviar" abajo. Las dos no pueden ser
		// ciertas, y el operador se queda sin saber si el boton funciono.
		_cola.Encolar(
			Fallida(intentos: 1, ultimoIntento: Ahora, codigo: "appincidencias.error.tecnico") with { Uuid = "u1", ClaveLocal = "LOC-1" },
			Fallida(intentos: 1, ultimoIntento: Ahora, codigo: "appincidencias.nota.requerida") with { Uuid = "u2", ClaveLocal = "LOC-2" });

		_reloj.UtcAhora = Ahora.AddSeconds(10);

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(0, resultado.Intentados);
		Assert.Equal(1, resultado.OmitidosEnEspera);
		Assert.Equal(1, resultado.OmitidosPorCorregir);
	}

	[Fact]
	public async Task UnaColaVaciaNoReportaOmitidos()
	{
		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(0, resultado.Intentados);
		Assert.Equal(0, resultado.OmitidosEnEspera);
		Assert.Equal(0, resultado.OmitidosPorCorregir);
	}

	// ── Dobles ────────────────────────────────────────────────────────────────────────

	private static ResultadoEnvio Aceptada(string folio) =>
		ResultadoEnvio.Aceptada(new IncidenciaRegistrada(folio, Ahora, false));

	private static IncidenciaEnviable Pendiente(string uuid = "uuid-1") => new(
		uuid, "LOC-000001", 11, Guid.NewGuid(), "130+200", KilometerSource.Manual,
		"nota de prueba", Ahora, "sesion-1", EstadoSincronizacion.Pendiente, 0, Ahora, null);

	private static IncidenciaEnviable Fallida(int intentos, DateTime ultimoIntento, string codigo) => new(
		"uuid-1", "LOC-000001", 11, Guid.NewGuid(), "130+200", KilometerSource.Manual,
		"nota de prueba", Ahora, "sesion-1", EstadoSincronizacion.Fallido, intentos, ultimoIntento, codigo);

	private sealed class ColaFalsa : ISyncQueueService
	{
		private readonly List<IncidenciaEnviable> _enviables = [];

		public List<ActualizacionEnvio> Actualizaciones { get; } = [];

		public void Encolar(params IncidenciaEnviable[] incidencias) => _enviables.AddRange(incidencias);

		public Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<IncidenciaEnviable>>(_enviables);

		public Task<IncidenciaEnviable?> ObtenerEnviablePorClaveAsync(
			string claveLocal, CancellationToken c = default) =>
			Task.FromResult(_enviables.FirstOrDefault(i => i.ClaveLocal == claveLocal));

		public Task ActualizarEnvioAsync(ActualizacionEnvio actualizacion, CancellationToken c = default)
		{
			Actualizaciones.Add(actualizacion);
			return Task.CompletedTask;
		}

		public Task RegistrarIntentoAsync(
			string uuid, bool exito, string? codigo, string? mensaje, CancellationToken c = default) =>
			Task.CompletedTask;

		public Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<RegistroCola>>([]);

		public Task<int> ContarPendientesAsync(CancellationToken c = default) => Task.FromResult(0);
	}

	private sealed class JacobFalso : IIncidenciasJacobClient
	{
		private readonly Queue<ResultadoEnvio> _programados = new();

		public List<EnvioIncidencia> Recibidos { get; } = [];

		public JacobFalso Responde(ResultadoEnvio resultado)
		{
			_programados.Enqueue(resultado);
			return this;
		}

		public Task<ResultadoEnvio> RegistrarAsync(
			EnvioIncidencia incidencia, string accessToken, CancellationToken c = default)
		{
			Recibidos.Add(incidencia);
			return Task.FromResult(_programados.Count > 0
				? _programados.Dequeue()
				: Aceptada("INC-APK-2026-0001"));
		}
	}

	private sealed class RelojControlado : IClock
	{
		public DateTime UtcAhora { get; set; } = Ahora;
	}

	private sealed class ConectividadFalsa : IConnectivityService
	{
		public bool HayEnlace { get; set; } = true;

		public event EventHandler<bool>? EnlaceCambio;

		public Task<ResultadoSondeo> ComprobarAsync(CancellationToken c = default)
		{
			EnlaceCambio?.Invoke(this, HayEnlace);
			return Task.FromResult(HayEnlace
				? ResultadoSondeo.Alcanzado()
				: ResultadoSondeo.SinTransporte("Apagado por la prueba."));
		}
	}

	private sealed class SesionFalsa : ISessionStore
	{
		public SesionFalsa()
		{
			Actual = new SesionOperador(
				"admin",
				"Operador",
				"VEH-01",
				VigenciaOffline.Validada(Ahora),
				PermisosOperador.DelServidor([ReglaCapacidades.PermisoAppOperadorMovil]),
				"1.2.0",
				new DateOnly(2026, 8, 20));
		}

		public SesionOperador? Actual { get; private set; }

		public void Guardar(SesionOperador sesion) => Actual = sesion;

		public void Limpiar() => Actual = null;
	}

	private sealed class TokenFalso : ITokenProvider
	{
		public Task GuardarAsync(string accessToken, CancellationToken c = default) => Task.CompletedTask;

		public Task<string?> ObtenerAsync(CancellationToken c = default) =>
			Task.FromResult<string?>("token-de-prueba");

		public Task LimpiarAsync(CancellationToken c = default) => Task.CompletedTask;
	}

	private sealed class BitacoraNula : IAuditLog
	{
		public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken c = default) =>
			Task.CompletedTask;

		public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EventoAuditoria>>([]);
	}

	private sealed class CatalogoFalso : ICatalogoRepository
	{
		public Task<CatalogosOperacion> ObtenerAsync(CancellationToken c = default) =>
			Task.FromResult(new CatalogosOperacion(
				new DateOnly(2026, 8, 20),
				[new TipoIncidencia(11, "Objeto en camino")],
				[new SeveridadIncidencia(Guid.NewGuid(), "Crítico", 1, "#EB1409")],
				[new AfectacionIncidencia(3, "Parcial")],
				[new CuerpoVia("A", "Cuerpo A")]));

		public Task ReemplazarAsync(CatalogosOperacion catalogos, CancellationToken c = default) =>
			Task.CompletedTask;
	}
}
