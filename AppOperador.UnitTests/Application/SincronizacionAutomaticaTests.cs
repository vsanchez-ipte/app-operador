using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.UnitTests.Application;

public sealed class SincronizacionAutomaticaTests
{
	private static readonly TimeSpan Margen = TimeSpan.FromSeconds(5);

	private readonly ConectividadFalsa _conectividad = new();
	private readonly SincronizadorFalso _sincronizador = new();
	private readonly BitacoraFalsa _bitacora = new();

	private SincronizacionAutomatica Crear() =>
		new(_conectividad, _sincronizador, _bitacora);

	[Fact]
	public async Task AlRecuperarElEnlace_loPendienteSaleSinQueNadieLoPida()
	{
		var servicio = Crear();
		servicio.Iniciar();

		var avisado = EsperarElAviso(servicio);
		_conectividad.Publicar(true);

		await avisado.WaitAsync(Margen);
		Assert.Equal(1, _sincronizador.Veces);
	}

	[Fact]
	public void AlPerderElEnlace_noSeIntentaNada()
	{
		var servicio = Crear();
		servicio.Iniciar();

		_conectividad.Publicar(false);

		Assert.Equal(0, _sincronizador.Veces);
	}

	[Fact]
	public void SinIniciar_noEscuchaNada()
	{
		_ = Crear();

		_conectividad.Publicar(true);

		Assert.Equal(0, _sincronizador.Veces);
	}

	[Fact]
	public void IniciarDosVeces_noDejaDosSuscripciones()
	{
		var servicio = Crear();
		servicio.Iniciar();
		servicio.Iniciar();

		_conectividad.Publicar(true);

		Assert.Equal(1, _sincronizador.Veces);
	}

	[Fact]
	public void DespuesDeLiberarlo_yaNoSincroniza()
	{
		var servicio = Crear();
		servicio.Iniciar();
		servicio.Dispose();

		_conectividad.Publicar(true);

		Assert.Equal(0, _sincronizador.Veces);
	}

	[Fact]
	public async Task SiLaSincronizacionRevienta_noSePropaga_yQuedaAnotada()
	{
		_sincronizador.Revienta = new InvalidOperationException("Se cayó a media tanda.");
		var servicio = Crear();
		servicio.Iniciar();

		_conectividad.Publicar(true);

		await _bitacora.SeAnotoAlgo.Task.WaitAsync(Margen);
		Assert.Contains(_bitacora.Mensajes, m => m.Contains("sincronización automática"));
	}

	[Fact]
	public async Task SiYaHabiaUnaTandaCorriendo_noSeAvisaDeNada()
	{
		var servicio = Crear();
		servicio.Iniciar();

		var avisos = 0;
		var segundoAviso = new TaskCompletionSource();
		servicio.SincronizacionTerminada += (_, _) =>
		{
			avisos++;
			segundoAviso.TrySetResult();
		};

		_sincronizador.Resultado = new ResultadoSincronizacion(0, 0, MotivoNoSincroniza.YaEnCurso);
		_conectividad.Publicar(true);

		// Testigo: si esta sí avisa, el enganche funciona y la anterior de verdad no avisó.
		_sincronizador.Resultado = new ResultadoSincronizacion(1, 1, null);
		_conectividad.Publicar(true);

		await segundoAviso.Task.WaitAsync(Margen);
		Assert.Equal(2, _sincronizador.Veces);
		Assert.Equal(1, avisos);
	}

	private static Task EsperarElAviso(SincronizacionAutomatica servicio)
	{
		var aviso = new TaskCompletionSource();
		servicio.SincronizacionTerminada += (_, _) => aviso.TrySetResult();
		return aviso.Task;
	}

	// ── Dobles ────────────────────────────────────────────────────────────────────────

	private sealed class ConectividadFalsa : IConnectivityService
	{
		public bool HayEnlace { get; private set; } = true;

		public event EventHandler<bool>? EnlaceCambio;

		public void Publicar(bool hayEnlace)
		{
			HayEnlace = hayEnlace;
			EnlaceCambio?.Invoke(this, hayEnlace);
		}

		public Task<ResultadoSondeo> ComprobarAsync(CancellationToken c = default) =>
			Task.FromResult(HayEnlace
				? ResultadoSondeo.Alcanzado()
				: ResultadoSondeo.SinTransporte("Apagado por la prueba."));

		public void AnotarIntercambio(bool jacobRespondio)
		{
		}
	}

	private sealed class SincronizadorFalso : ISincronizadorIncidencias
	{
		public int Veces { get; private set; }

		public ResultadoSincronizacion Resultado { get; set; } = new(1, 1, null);

		public Exception? Revienta { get; set; }

		public Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken c = default)
		{
			Veces++;

			return Revienta is not null
				? Task.FromException<ResultadoSincronizacion>(Revienta)
				: Task.FromResult(Resultado);
		}

		public Task<ResultadoSincronizacion> EnviarUnaAsync(
			string claveLocal, CancellationToken c = default) =>
			Task.FromResult(Resultado);
	}

	private sealed class BitacoraFalsa : IAuditLog
	{
		public List<string> Mensajes { get; } = [];

		public TaskCompletionSource SeAnotoAlgo { get; } = new();

		public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken c = default)
		{
			Mensajes.Add(mensaje);
			SeAnotoAlgo.TrySetResult();
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
}
