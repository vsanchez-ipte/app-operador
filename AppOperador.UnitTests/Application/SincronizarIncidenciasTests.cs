using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

public sealed class SincronizarIncidenciasTests
{
	private static readonly DateTime Ahora = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

	private readonly RelojControlado _reloj = new();
	private readonly ColaFalsa _cola = new();
	private readonly JacobFalso _jacob = new();
	private readonly ConectividadFalsa _conectividad = new();
	private readonly SesionFalsa _sesion = new();
	private readonly EvidenciasFalsas _evidencias = new();
	private readonly EvidenciasJacobFalso _jacobEvidencias = new();
	private readonly BitacoraNula _bitacora = new();

	private SincronizarIncidencias Crear() => new(
		_cola,
		_jacob,
		_conectividad,
		new TokenFalso(),
		new CapacidadesDeLaSesion(_sesion),
		_reloj,
		_bitacora,
		new CatalogoFalso(),
		_evidencias,
		_jacobEvidencias,
		_sesion);

	// ── El envío que nunca terminó ────────────────────────────────────────────────────

	[Fact]
	public async Task AntesDeLeerLaCola_seRecuperanLosEnviosInterrumpidos()
	{
		// Si el proceso muere en Enviando, nadie escribe el estado final y el registro queda fuera de todo.
		_cola.EnviosInterrumpidos = 2;

		await Crear().EjecutarAsync();

		Assert.Equal(1, _cola.VecesQueSeRecupero);
	}

	[Fact]
	public async Task ElEnvioSeRecuperaAunqueNoHayaNadaQueMandar()
	{
		// Lo que se recupera es justo lo que la cola no ve.
		await Crear().EjecutarAsync();

		Assert.Equal(1, _cola.VecesQueSeRecupero);
	}

	[Fact]
	public async Task SiElEnvioRevienta_elRegistroQuedaFallidoYNoEnviando()
	{
		_cola.Encolar(Pendiente());
		_jacob.LanzaExcepcion = true;

		var resultado = await Crear().EjecutarAsync();

		var ultima = _cola.Actualizaciones[^1];
		Assert.Equal(EstadoSincronizacion.Fallido, ultima.Estado);
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.FamiliaUltimoError);
	}

	[Fact]
	public async Task UnEnvioQueRevienta_seClasificaComoTecnicoYNoComoFuncional()
	{
		// Una excepción del cliente no dice que el registro esté mal.
		_cola.Encolar(Pendiente());
		_jacob.LanzaExcepcion = true;

		await Crear().EjecutarAsync();

		var ultima = _cola.Actualizaciones[^1];
		Assert.False(CodigosErrorJacob.EsFuncional(ultima.UltimoErrorCodigo));
	}

	// ── La espera creciente ───────────────────────────────────────────────────────────

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
		// Sin tope, al recuperar la señal lo capturado no saldría hasta la jornada siguiente.
		_cola.Encolar(Fallida(intentos: 50, ultimoIntento: Ahora, codigo: "appincidencias.error.tecnico"));
		_reloj.UtcAhora = Ahora.Add(ReglaEsperaReintento.EsperaMaxima);

		Assert.Equal(1, (await Crear().EjecutarAsync()).Intentados);
	}

	// ── Lo funcional no se reintenta ──────────────────────────────────────────────────

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
		// Funcional significa que deja de reintentarse, no que se descarte.
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

		// Prudente: mejor un reintento acotado que un registro parado esperando una corrección.
		Assert.Equal(1, (await Crear().EjecutarAsync()).Intentados);
	}

	// ── Una falla no arrastra a las demás ─────────────────────────────────────────────

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
		// Cada uno pasa por Enviando y termina en su estado antes del siguiente.
		_cola.Encolar(Pendiente("uuid-1"), Pendiente("uuid-2"));

		await Crear().EjecutarAsync();

		Assert.Equal(2, _cola.Actualizaciones.Count(a => a.Estado == EstadoSincronizacion.Enviando));
		Assert.Equal(2, _cola.Actualizaciones.Count(a => a.Estado == EstadoSincronizacion.Sincronizado));
	}

	// ── Las compuertas ────────────────────────────────────────────────────────────────

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
		// yaExistia: la respuesta anterior se perdió y reenviar fue lo correcto.
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
		// El operador está parado en el incidente: los registros viejos no deben hacerle esperar.
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

		// Sin enlace no se toca el registro: sigue pendiente.
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
		// Pudo eliminarse, ser de otro operador o haber salido ya.
		var resultado = await Crear().EnviarUnaAsync("LOC-999999");

		Assert.Equal(0, resultado.Intentados);
		Assert.Null(resultado.MotivoBloqueo);
		Assert.Empty(_jacob.Recibidos);
	}

	// ── El motivo del rechazo llega hasta la pantalla ─────────────────────────────────

	[Fact]
	public async Task UnFalloTecnicoSeDistingueDeUnRechazoDeJacob()
	{
		// Con el API apagado Jacob nunca la recibió: decir que el CCO no la aceptó manda a revisar una captura correcta.
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
		// Jacob sabe mejor que la pantalla qué está mal.
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

	// ── Saltarse un registro se cuenta, y se dice por qué ─────────────────────────────

	[Fact]
	public async Task LoQueEsperaSuReintentoSeCuentaAparteDeLoQueNecesitaCorreccion()
	{
		// El encabezado y el aviso de la cola no pueden contradecirse.
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

	// ── Una sola tanda a la vez ───────────────────────────────────────────────────────

	[Fact]
	public async Task MientrasCorreUnaTanda_laSegundaNoEntra()
	{
		// Recuperar la señal justo al pulsar «Sincronizar» es lo normal para quien está esperando.
		_cola.Encolar(Pendiente());
		_jacob.Pausa = new TaskCompletionSource();

		var sincronizador = Crear();
		var primera = sincronizador.EjecutarAsync();
		await _jacob.LlegoLaPrimera.Task;

		var segunda = await sincronizador.EjecutarAsync();

		Assert.Equal(MotivoNoSincroniza.YaEnCurso, segunda.MotivoBloqueo);
		Assert.Equal(0, segunda.Intentados);

		// Y lo que importa de verdad: la segunda no volvió a mandar el mismo registro.
		Assert.Single(_jacob.Recibidos);

		_jacob.Pausa.SetResult();
		var resultadoPrimera = await primera;
		Assert.Equal(1, resultadoPrimera.Confirmados);
	}

	[Fact]
	public async Task CuandoLaTandaTermina_laSiguienteSiEntra()
	{
		// El cerrojo se suelta pase lo que pase, o la app dejaría de sincronizar hasta reiniciarla.
		_cola.Encolar(Pendiente());
		var sincronizador = Crear();

		await sincronizador.EjecutarAsync();
		var segunda = await sincronizador.EjecutarAsync();

		Assert.Null(segunda.MotivoBloqueo);
	}

	[Fact]
	public async Task SiLaTandaRevienta_elCerrojoIgualSeSuelta()
	{
		_cola.Encolar(Pendiente());
		_jacob.LanzaExcepcion = true;
		var sincronizador = Crear();

		await sincronizador.EjecutarAsync();
		_jacob.LanzaExcepcion = false;
		var segunda = await sincronizador.EjecutarAsync();

		Assert.NotEqual(MotivoNoSincroniza.YaEnCurso, segunda.MotivoBloqueo);
	}

	[Fact]
	public async Task ElEnvioInmediatoNoSeCuelaMientrasCorreLaTanda()
	{
		// Comparten cerrojo: el registro recién guardado puede ser uno de los que la tanda recorre.
		_cola.Encolar(Pendiente());
		_jacob.Pausa = new TaskCompletionSource();

		var sincronizador = Crear();
		var tanda = sincronizador.EjecutarAsync();
		await _jacob.LlegoLaPrimera.Task;

		var inmediato = await sincronizador.EnviarUnaAsync("LOC-000001");

		Assert.Equal(MotivoNoSincroniza.YaEnCurso, inmediato.MotivoBloqueo);
		Assert.Single(_jacob.Recibidos);

		_jacob.Pausa.SetResult();
		await tanda;
	}

	[Fact]
	public async Task LoQueLlegaAMediaTanda_saleEnLaMismaSincronizacion()
	{
		// La tanda relee la cola si un envío inmediato la encontró ocupada; con señal estable nada más lo enviaría.
		_cola.Encolar(Pendiente("uuid-1"));
		_jacob.Pausa = new TaskCompletionSource();

		var sincronizador = Crear();
		var tanda = sincronizador.EjecutarAsync();
		await _jacob.LlegoLaPrimera.Task;

		// Llega una captura nueva mientras la primera está en el aire.
		_cola.Encolar(Pendiente("uuid-2"));
		var inmediato = await sincronizador.EnviarUnaAsync("LOC-000001");
		Assert.Equal(MotivoNoSincroniza.YaEnCurso, inmediato.MotivoBloqueo);

		_jacob.Pausa.SetResult();
		var resultado = await tanda;

		Assert.Equal(2, resultado.Confirmados);
		Assert.Equal(["uuid-1", "uuid-2"], _jacob.Recibidos.Select(r => r.Uuid).ToArray());
	}

	[Fact]
	public async Task LoQueLlegaMientrasOtroEnvioInmediatoTieneElCerrojo_tambienSale()
	{
		// Si el cerrojo lo tenía otro envío inmediato, al soltarlo corre una tanda.
		_cola.Encolar(Pendiente("uuid-1"));
		_jacob.Pausa = new TaskCompletionSource();

		var sincronizador = Crear();
		var primero = sincronizador.EnviarUnaAsync("LOC-000001");
		await _jacob.LlegoLaPrimera.Task;

		_cola.Encolar(Pendiente("uuid-2") with { ClaveLocal = "LOC-000002" });
		var segundo = await sincronizador.EnviarUnaAsync("LOC-000002");
		Assert.Equal(MotivoNoSincroniza.YaEnCurso, segundo.MotivoBloqueo);

		_jacob.Pausa.SetResult();
		await primero;

		Assert.Equal(["uuid-1", "uuid-2"], _jacob.Recibidos.Select(r => r.Uuid).ToArray());
	}

	[Fact]
	public async Task SinNadaNuevoAMediaTanda_noHaySegundaPasada()
	{
		_cola.Encolar(Pendiente("uuid-1"));

		await Crear().EjecutarAsync();

		Assert.Equal(1, _cola.LecturasDeEnviables);
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

		public int LecturasDeEnviables { get; private set; }

		// Copia sin lo Sincronizado: una segunda pasada no debe reenviar lo ya confirmado.
		public Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(CancellationToken c = default)
		{
			LecturasDeEnviables++;
			var sincronizados = Actualizaciones
				.Where(a => a.Estado == EstadoSincronizacion.Sincronizado)
				.Select(a => a.Uuid)
				.ToHashSet();
			return Task.FromResult<IReadOnlyList<IncidenciaEnviable>>(
				_enviables.Where(i => !sincronizados.Contains(i.Uuid)).ToList());
		}

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

		public int EnviosInterrumpidos { get; set; }

		public int VecesQueSeRecupero { get; private set; }

		public Task<int> RecuperarEnviosInterrumpidosAsync(CancellationToken c = default)
		{
			VecesQueSeRecupero++;
			return Task.FromResult(EnviosInterrumpidos);
		}
	}

	private sealed class JacobFalso : IIncidenciasJacobClient
	{
		private readonly Queue<ResultadoEnvio> _programados = new();

		public List<EnvioIncidencia> Recibidos { get; } = [];

		public bool LanzaExcepcion { get; set; }

		// Deja una tanda a medio correr para poder solapar otra.
		public TaskCompletionSource? Pausa { get; set; }

		public TaskCompletionSource LlegoLaPrimera { get; } = new();

		public JacobFalso Responde(ResultadoEnvio resultado)
		{
			_programados.Enqueue(resultado);
			return this;
		}

		public async Task<ResultadoEnvio> RegistrarAsync(
			EnvioIncidencia incidencia, string accessToken, CancellationToken c = default)
		{
			Recibidos.Add(incidencia);
			LlegoLaPrimera.TrySetResult();

			if (Pausa is not null)
			{
				await Pausa.Task;
			}

			if (LanzaExcepcion)
			{
				throw new HttpRequestException("La conexión se cortó a media petición.");
			}

			return _programados.Count > 0
				? _programados.Dequeue()
				: Aceptada("INC-APK-2026-0001");
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

		public void AnotarIntercambio(bool jacobRespondio)
		{
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
		public List<(NivelAuditoria Nivel, string Mensaje)> Escrito { get; } = [];

		public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken c = default)
		{
			Escrito.Add((nivel, mensaje));
			return Task.CompletedTask;
		}

		public Task AtribuirAsync(string alias, string operador, CancellationToken c = default) =>
			Task.CompletedTask;

		public Task RegistrarAsync(
			OperacionAuditada operacion, ResultadoAuditoria resultado, string mensaje,
			string? motivoCodigo = null, string? operador = null, CancellationToken c = default) =>
			RegistrarAsync(
				resultado == ResultadoAuditoria.Rechazo ? NivelAuditoria.Advertencia : NivelAuditoria.Info,
				mensaje, c);

		public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EventoAuditoria>>([]);

		public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(int omitir, int cantidad, CancellationToken c = default) =>
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
				[new CuerpoVia("A", "Cuerpo A")],
				LimitesEvidencia.Desconocidos));

		public Task ReemplazarAsync(CatalogosOperacion catalogos, CancellationToken c = default) =>
			Task.CompletedTask;
	}

	// ── La evidencia va encadenada a su incidencia ────────────────────────────────────

	private static EvidenciaAdjunta EvidenciaDe(
		string incidenciaUuid = "uuid-1",
		string uuid = "ev-1",
		string? ultimoError = null) =>
		new(uuid, incidenciaUuid, $"{uuid}.jpg", "image/jpeg", 1024,
			$"/privado/{uuid}.jpg", EstadoSincronizacion.Pendiente, ultimoError);

	[Fact]
	public async Task LaEvidenciaSaleDespuesDeQueLaIncidenciaConfirma()
	{
		_cola.Encolar(Pendiente());
		_evidencias.Pendientes.Add(EvidenciaDe());

		await Crear().EjecutarAsync();

		Assert.Single(_jacobEvidencias.Subidas);
		Assert.Contains(_evidencias.Actualizadas,
			a => a.Estado == EstadoSincronizacion.Sincronizado);
	}

	[Fact]
	public async Task SiLaIncidenciaNoConfirma_laEvidenciaNiSeIntenta()
	{
		// Antes que su incidencia, el servidor la rechazaría como funcional y quedaría parada para siempre.
		_cola.Encolar(Pendiente());
		_jacob.Responde(ResultadoEnvio.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, "appincidencias.error.tecnico", "Sin red."));
		_evidencias.Pendientes.Add(EvidenciaDe());

		await Crear().EjecutarAsync();

		Assert.Empty(_jacobEvidencias.Subidas);
	}

	[Fact]
	public async Task UnaEvidenciaQueFalla_noRevierteLaIncidenciaYaConfirmada()
	{
		_cola.Encolar(Pendiente());
		_evidencias.Pendientes.Add(EvidenciaDe());
		_jacobEvidencias.Responde(ResultadoEnvioEvidencia.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, "appincidencias.error.tecnico", "Sin red."));

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(1, resultado.Confirmados);
		Assert.Contains(_cola.Actualizaciones, a => a.Estado == EstadoSincronizacion.Sincronizado);
		Assert.Contains(_evidencias.Actualizadas, a => a.Estado == EstadoSincronizacion.Fallido);
	}

	[Fact]
	public async Task UnaEvidenciaQueFalla_noDetieneALasSiguientes()
	{
		_cola.Encolar(Pendiente());
		_evidencias.Pendientes.Add(EvidenciaDe(uuid: "ev-1"));
		_evidencias.Pendientes.Add(EvidenciaDe(uuid: "ev-2"));

		_jacobEvidencias.Responde(ResultadoEnvioEvidencia.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, "appincidencias.error.tecnico", "Sin red."));

		await Crear().EjecutarAsync();

		Assert.Equal(2, _jacobEvidencias.Subidas.Count);
	}

	[Fact]
	public async Task YaExistia_esExitoYNoSeReintenta()
	{
		// La subida es idempotente: el servidor ya la tenía.
		_cola.Encolar(Pendiente());
        _evidencias.Pendientes.Add(EvidenciaDe());
		_jacobEvidencias.Responde(ResultadoEnvioEvidencia.Aceptada(
			new EvidenciaRegistrada("id-remoto", "image/jpeg", "hash", YaExistia: true)));

		await Crear().EjecutarAsync();

		Assert.Contains(_evidencias.Actualizadas,
			a => a.Estado == EstadoSincronizacion.Sincronizado);
	}

	[Fact]
	public async Task UnaEvidenciaRechazadaPorFormato_noSeVuelveAIntentar()
	{
		_cola.Encolar(Pendiente());
		_evidencias.Pendientes.Add(
			EvidenciaDe(ultimoError: "appevidencias.formato.nopermitido"));

		await Crear().EjecutarAsync();

		Assert.Empty(_jacobEvidencias.Subidas);
	}

	private static EvidenciaRezagada Rezagada(
		int intentos = 1,
		DateTime? ultimoIntento = null,
		string? ultimoError = "appincidencias.error.tecnico") =>
		new(EvidenciaDe(ultimoError: ultimoError) with { Estado = EstadoSincronizacion.Fallido },
			intentos, ultimoIntento);

	[Fact]
	public async Task UnaEvidenciaQueFalloConSuIncidenciaYaConfirmada_seReintentaEnLaSiguienteTanda()
	{
		_evidencias.Rezagadas.Add(Rezagada(intentos: 1, ultimoIntento: Ahora.AddMinutes(-5)));

		await Crear().EjecutarAsync();

		Assert.Single(_jacobEvidencias.Subidas);
		Assert.Contains(_evidencias.Actualizadas,
			a => a.Estado == EstadoSincronizacion.Sincronizado);
	}

	[Fact]
	public async Task UnaEvidenciaRezagada_esperaComoUnaIncidenciaAntesDeReintentarse()
	{
		_evidencias.Rezagadas.Add(Rezagada(intentos: 1, ultimoIntento: Ahora.AddSeconds(-30)));

		await Crear().EjecutarAsync();

		Assert.Empty(_jacobEvidencias.Subidas);
	}

	[Fact]
	public async Task UnaEvidenciaRezagadaQueNuncaSeIntento_saleSinEsperar()
	{
		_evidencias.Rezagadas.Add(Rezagada(intentos: 0, ultimoIntento: null, ultimoError: null));

		await Crear().EjecutarAsync();

		Assert.Single(_jacobEvidencias.Subidas);
	}

	[Fact]
	public async Task UnaEvidenciaRezagadaRechazadaPorElCco_noSeReintenta()
	{
		_evidencias.Rezagadas.Add(Rezagada(
			intentos: 1,
			ultimoIntento: Ahora.AddHours(-1),
			ultimoError: "appevidencias.formato.nopermitido"));

		await Crear().EjecutarAsync();

		Assert.Empty(_jacobEvidencias.Subidas);
	}

	[Fact]
	public async Task LasRezagadasSePidenParaElOperadorDeLaSesion()
	{
		await Crear().EjecutarAsync();

		Assert.Equal(["admin"], _evidencias.RezagadasPedidasPara);
	}

	[Fact]
	public async Task SinEnlace_lasRezagadasTampocoSeIntentan()
	{
		_conectividad.HayEnlace = false;
		_evidencias.Rezagadas.Add(Rezagada(intentos: 1, ultimoIntento: Ahora.AddHours(-1)));

		await Crear().EjecutarAsync();

		Assert.Empty(_jacobEvidencias.Subidas);
	}

	[Fact]
	public async Task UnaRezagadaQueVuelveAFallar_quedaFallidaParaElSiguienteIntento()
	{
		_evidencias.Rezagadas.Add(Rezagada(intentos: 1, ultimoIntento: Ahora.AddMinutes(-5)));
		_jacobEvidencias.Responde(ResultadoEnvioEvidencia.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, "appincidencias.error.tecnico", "Sin red."));

		await Crear().EjecutarAsync();

		Assert.Equal([("ev-1", EstadoSincronizacion.Fallido)], _evidencias.Actualizadas);
	}

	[Fact]
	public async Task UnaEvidenciaQueFalla_dejaSuMotivoEnLaAuditoria()
	{
		_evidencias.Rezagadas.Add(Rezagada(intentos: 1, ultimoIntento: Ahora.AddMinutes(-5)));
		_jacobEvidencias.Responde(ResultadoEnvioEvidencia.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, "appincidencias.error.tecnico",
			"El servidor respondió HTTP 500 InternalServerError sin explicación."));

		await Crear().EjecutarAsync();

		Assert.Contains(_bitacora.Escrito, e =>
			e.Nivel == NivelAuditoria.Advertencia
			&& e.Mensaje == "Evidencia ev-1.jpg no llegó al CCO: El servidor respondió HTTP 500 " +
				"InternalServerError sin explicación. [appincidencias.error.tecnico]");
	}

	[Fact]
	public async Task UnaEvidenciaQueLlega_noEnsuciaLaAuditoria()
	{
		_evidencias.Rezagadas.Add(Rezagada(intentos: 1, ultimoIntento: Ahora.AddMinutes(-5)));

		await Crear().EjecutarAsync();

		Assert.DoesNotContain(_bitacora.Escrito, e => e.Mensaje.StartsWith("Evidencia ev-1.jpg"));
	}

	// ── Dobles de evidencia ───────────────────────────────────────────────────────────

	private sealed class EvidenciasFalsas : IRepositorioEvidencias
	{
		public List<EvidenciaAdjunta> Pendientes { get; } = [];

		public List<(string Uuid, EstadoSincronizacion Estado)> Actualizadas { get; } = [];

		public Task AgregarAsync(EvidenciaAdjunta evidencia, CancellationToken c = default) =>
			Task.CompletedTask;

		public Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerDeIncidenciaAsync(
			string incidenciaUuid, CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EvidenciaAdjunta>>([.. Pendientes]);

		public Task<int> ContarDeIncidenciaAsync(string incidenciaUuid, CancellationToken c = default) =>
			Task.FromResult(Pendientes.Count);

		public Task<EvidenciaAdjunta?> ObtenerAsync(string uuid, CancellationToken c = default) =>
			Task.FromResult(Pendientes.FirstOrDefault(e => e.Uuid == uuid));

		public Task EliminarAsync(string uuid, CancellationToken c = default) => Task.CompletedTask;

		public Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerPendientesDeIncidenciaAsync(
			string incidenciaUuid, CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EvidenciaAdjunta>>(
				[.. Pendientes.Where(e => e.IncidenciaUuid == incidenciaUuid)]);

		public Task<IReadOnlyList<EvidenciaPendiente>> ObtenerPendientesDelOperadorAsync(
			string operador, CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EvidenciaPendiente>>([]);

		public List<EvidenciaRezagada> Rezagadas { get; } = [];

		public List<string> RezagadasPedidasPara { get; } = [];

		public Task<IReadOnlyList<EvidenciaRezagada>> ObtenerRezagadasDelOperadorAsync(
			string operador, CancellationToken c = default)
		{
			RezagadasPedidasPara.Add(operador);
			return Task.FromResult<IReadOnlyList<EvidenciaRezagada>>([.. Rezagadas]);
		}

		public Task ActualizarEnvioAsync(
			string uuid, EstadoSincronizacion estado, string? codigoError, string? mensaje = null, CancellationToken c = default)
		{
			Actualizadas.Add((uuid, estado));
			return Task.CompletedTask;
		}
	}

	private sealed class EvidenciasJacobFalso : IEvidenciasJacobClient
	{
		private readonly Queue<ResultadoEnvioEvidencia> _programados = new();

		public List<string> Subidas { get; } = [];

		public EvidenciasJacobFalso Responde(ResultadoEnvioEvidencia resultado)
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
			Subidas.Add(rutaArchivo);

			return Task.FromResult(_programados.Count > 0
				? _programados.Dequeue()
				: ResultadoEnvioEvidencia.Aceptada(
					new EvidenciaRegistrada("id-remoto", "image/jpeg", "hash", YaExistia: false)));
		}
	}
}
