using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// El envío que ocurre solo al recuperar el enlace (JTT-1406).
/// </summary>
/// <remarks>
/// Lo que se prueba aquí es <b>el enganche</b>: cuándo se dispara, cuándo no, y que un fallo suyo
/// no se lleve por delante nada. Qué se envía y en qué orden es de
/// <c>SincronizarIncidenciasTests</c>.
/// </remarks>
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
		// Es el defecto que esta historia viene a arreglar: hasta ahora lo capturado solo salía
		// si el operador pulsaba el botón, y en campo el teléfono va en la bolsa.
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
		// Perder la señal no da trabajo: lo capturado ya está guardado y la cola lo conserva.
		// Intentar el envío justo cuando se acaba de caer solo quema un intento.
		var servicio = Crear();
		servicio.Iniciar();

		_conectividad.Publicar(false);

		Assert.Equal(0, _sincronizador.Veces);
	}

	[Fact]
	public void SinIniciar_noEscuchaNada()
	{
		// Construir el servicio no debe poder lanzar trabajo de fondo, por lo mismo que el
		// servicio de conectividad no sondea desde su constructor.
		_ = Crear();

		_conectividad.Publicar(true);

		Assert.Equal(0, _sincronizador.Veces);
	}

	[Fact]
	public void IniciarDosVeces_noDejaDosSuscripciones()
	{
		// Si no fuera idempotente, cada reconexión dispararía dos tandas y la segunda se
		// encontraría la cola ya vacía.
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
		// Lo dispara un evento del sistema y nadie espera el resultado: una excepción que
		// escapara de aquí no tendría quién la recogiera y se llevaría el proceso por delante.
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
		// Recuperar la señal mientras el operador ya pulsó el botón es lo normal, no una
		// anomalía: la otra tanda se está ocupando y no hay nada que contarle a la pantalla.
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

		// Una segunda reconexión que sí hace trabajo. Sirve de testigo: cuando esta avisa, se
		// sabe que el enganche funciona y que el aviso de la anterior no llegó.
		_sincronizador.Resultado = new ResultadoSincronizacion(1, 1, null);
		_conectividad.Publicar(true);

		await segundoAviso.Task.WaitAsync(Margen);
		Assert.Equal(2, _sincronizador.Veces);
		Assert.Equal(1, avisos);
	}

	/// <summary>Devuelve una tarea que termina cuando el servicio avisa.</summary>
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

		/// <summary>Anuncia un cambio de enlace, como haría el servicio real.</summary>
		public void Publicar(bool hayEnlace)
		{
			HayEnlace = hayEnlace;
			EnlaceCambio?.Invoke(this, hayEnlace);
		}

		public Task<ResultadoSondeo> ComprobarAsync(CancellationToken c = default) =>
			Task.FromResult(HayEnlace
				? ResultadoSondeo.Alcanzado()
				: ResultadoSondeo.SinTransporte("Apagado por la prueba."));
	}

	private sealed class SincronizadorFalso : ISincronizadorIncidencias
	{
		/// <summary>
		/// Cuántas veces se pidió la sincronización.
		/// </summary>
		/// <remarks>
		/// Se incrementa <b>antes</b> del primer await, así que ya está actualizado cuando
		/// <c>Publicar</c> devuelve el control. Las pruebas que solo cuentan no necesitan esperar.
		/// </remarks>
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

		public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EventoAuditoria>>([]);
	}
}
