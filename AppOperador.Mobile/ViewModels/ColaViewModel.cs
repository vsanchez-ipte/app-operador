using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Cola local de registros pendientes de sincronizar (JTT-290 y JTT-291).
/// </summary>
public sealed partial class ColaViewModel : ObservableObject
{
	private readonly ISyncQueueService _cola;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly SincronizacionAutomatica _sincronizacionAutomatica;

	private bool _escuchandoLaSincronizacionAutomatica;

	/// <summary>
	/// Cada cuánto se comprueba si a algún registro ya le tocó su reintento.
	/// </summary>
	/// <remarks>
	/// <b>Espaciado a propósito.</b> La tarjeta muestra la hora del reintento y no una cuenta
	/// atrás, así que no hay nada que repintar cada segundo: esto solo mira si venció alguno. La
	/// espera más corta es de un minuto, de modo que medio minuto de resolución basta para que el
	/// envío ocurra cuando la pantalla dice que va a ocurrir.
	/// </remarks>
	private static readonly TimeSpan CadaCuantoSeRevisaElReintento = TimeSpan.FromSeconds(30);

	private IDispatcherTimer? _relojDeReintentos;

	[ObservableProperty]
	public partial int Pendientes { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	/// <summary>
	/// Qué pasó en la última sincronización, o <see langword="null"/> si no se ha pulsado
	/// (JTT-1401 CA 10).
	/// </summary>
	/// <remarks>
	/// <b>Sin esto, pulsar «Sincronizar» sin enlace no produce ningún cambio visible</b> y el
	/// operador no puede distinguir un botón que no respondió de una cola que no tenía nada que
	/// enviar. El criterio pide que la app muestre el motivo.
	/// </remarks>
	[ObservableProperty]
	public partial string? MensajeSincronizacion { get; set; }

	public ColaViewModel(
		ISyncQueueService cola,
		ISincronizadorIncidencias sincronizador,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace,
		SincronizacionAutomatica sincronizacionAutomatica)
	{
		_cola = cola;
		_sincronizador = sincronizador;
		_capacidades = capacidades;
		_sincronizacionAutomatica = sincronizacionAutomatica;
		Enlace = enlace;
	}

	/// <summary>
	/// Empieza a atender los envíos que ocurren solos (JTT-1406).
	/// </summary>
	/// <remarks>
	/// <b>Lo llama la página al aparecer, y no el constructor</b>: este ViewModel es transitorio
	/// y cada visita a la pantalla crea uno nuevo. Suscribir en el constructor dejaría vivos a
	/// todos los anteriores, colgados de un servicio que dura lo que la aplicación. Además, el
	/// refresco solo tiene sentido mientras la lista se está viendo.
	/// </remarks>
	public void Escuchar()
	{
		if (_escuchandoLaSincronizacionAutomatica)
		{
			return;
		}

		_sincronizacionAutomatica.SincronizacionTerminada += AlTerminarUnEnvioAutomatico;
		_escuchandoLaSincronizacionAutomatica = true;

		IniciarRelojDeReintentos();
	}

	/// <summary>
	/// Arranca la comprobación de reintentos vencidos.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Sin esto la hora que muestra la tarjeta sería una promesa vacía.</b> Los disparadores
	/// del envío son el botón, la revalidación de sesión y la recuperación del enlace; con señal
	/// estable, ninguno de los tres ocurre, así que la espera de un registro fallido vence y ahí
	/// se queda. Un aviso que dice «Reintento a las 17:42» y a las 17:42 no hace nada es peor que
	/// no decir nada.
	/// </para>
	/// <para>
	/// <b>Solo corre mientras la pantalla está a la vista</b>, y hay que decirlo: fuera de aquí el
	/// reintento sigue dependiendo de que vuelva el enlace.
	/// </para>
	/// </remarks>
	private void IniciarRelojDeReintentos()
	{
		// En algún destino puede no haber despachador todavía; sin él, la pantalla sigue
		// funcionando y solo se pierde el disparo por vencimiento.
		_relojDeReintentos ??= Application.Current?.Dispatcher.CreateTimer();
		if (_relojDeReintentos is null)
		{
			return;
		}

		_relojDeReintentos.Interval = CadaCuantoSeRevisaElReintento;
		_relojDeReintentos.Tick -= AlTocarRevisarReintentos;
		_relojDeReintentos.Tick += AlTocarRevisarReintentos;
		_relojDeReintentos.Start();
	}

	/// <summary>
	/// Envía lo que ya cumplió su espera, si hay enlace.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Se comprueba primero en la lista que ya está en memoria, sin tocar la base: la inmensa
	/// mayoría de las veces no hay nada vencido y no tiene sentido pagar una consulta cada medio
	/// minuto.
	/// </para>
	/// <para>
	/// Sin enlace no se intenta. El sincronizador lo comprobaría igual, pero dejaría una línea en
	/// la bitácora en cada vuelta: media hora sin señal la llenaría de ruido idéntico.
	/// </para>
	/// </remarks>
	private async void AlTocarRevisarReintentos(object? origen, EventArgs argumentos)
	{
		if (Ocupado || !Enlace.HayEnlace || !HayAlgunReintentoVencido())
		{
			return;
		}

		Ocupado = true;
		try
		{
			var resultado = await _sincronizador.EjecutarAsync();

			// Solo se avisa de lo que cambió algo, igual que con el envío automático: el operador
			// no pidió esta sincronización y anunciarla cada vez llenaría la pantalla.
			if (resultado.Confirmados > 0)
			{
				MensajeSincronizacion = TextoDe(resultado);
			}

			await ActualizarAsync();
		}
		catch (Exception)
		{
			// Lo dispara un temporizador y nadie espera el resultado: una excepción que se
			// escapara de aquí no tendría quién la recogiera. El registro conserva su estado y
			// al operador le queda el botón.
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>Indica si algún registro de la lista ya cumplió su espera de reintento.</summary>
	private bool HayAlgunReintentoVencido()
	{
		var ahora = DateTime.UtcNow;

		return Registros.Any(r =>
			r.Registro.ReintentoUtc is { } reintento && reintento <= ahora);
	}

	/// <summary>Deja de atenderlos. Lo llama la página al desaparecer.</summary>
	public void DejarDeEscuchar()
	{
		if (!_escuchandoLaSincronizacionAutomatica)
		{
			return;
		}

		_sincronizacionAutomatica.SincronizacionTerminada -= AlTerminarUnEnvioAutomatico;
		_escuchandoLaSincronizacionAutomatica = false;

		_relojDeReintentos?.Stop();
	}

	/// <summary>
	/// Repinta la cola cuando el envío ocurrió sin que el operador lo pidiera.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Sin esto, recuperar la señal con la pantalla abierta enviaría los registros y la lista
	/// seguiría enseñándolos como pendientes hasta que alguien saliera y volviera a entrar. El
	/// operador leería que no salió nada cuando ya está en el CCO.
	/// </para>
	/// <para>
	/// Llega desde el hilo en el que corrió la sincronización, no del principal, así que la
	/// actualización de la lista se marshalea: tocar la colección enlazada desde otro hilo hace
	/// fallar el pintado.
	/// </para>
	/// </remarks>
	private void AlTerminarUnEnvioAutomatico(object? origen, ResultadoSincronizacion resultado)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			// Solo se avisa de lo que cambió algo. Una tanda automática que no encontró nada que
			// enviar es el caso corriente —el enlace va y viene todo el día— y anunciarla
			// llenaría la pantalla de mensajes que el operador no pidió.
			if (resultado.Confirmados > 0)
			{
				MensajeSincronizacion = TextoDe(resultado);
			}

			await ActualizarAsync();
		});
	}

	/// <summary>Indica si la sesión autoriza ver la cola (JTT-1385 CA 3 y 4).</summary>
	public bool PuedeConsultar => _capacidades.Puede(CapacidadOperador.ConsultarCola);

	/// <summary>Indica si la sesión autoriza sincronizar y no hay un envío en curso.</summary>
	public bool PuedeSincronizar =>
		!Ocupado && _capacidades.Puede(CapacidadOperador.Sincronizar);

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>Registros de la cola, listos para mostrarse. Los borradores no aparecen aquí.</summary>
	public ObservableCollection<RegistroColaVista> Registros { get; } = [];

	/// <summary>Resumen que encabeza la pantalla.</summary>
	/// <remarks>
	/// <para>
	/// Dice <b>«sin enviar»</b> y no «pendientes de sincronizar». La cuenta incluye las
	/// rechazadas, que no van a salir solas, y decir de ellas que están «pendientes de
	/// sincronizar» contradice el aviso de abajo, que avisa justamente de que no se reintentan.
	/// Las dos frases eran ciertas y juntas se leían como un error de la aplicación.
	/// </para>
	/// <para>
	/// «Sin enviar» es lo único que vale para los tres estados que cuenta —pendiente, fallida y
	/// el envío que quedó a medias— y es además lo que el operador necesita saber: cuántas de
	/// las suyas no están todavía en el CCO.
	/// </para>
	/// </remarks>
	public string TextoPendientes => Pendientes switch
	{
		0 => "No hay incidencias sin enviar.",
		1 => "1 incidencia sin enviar al CCO.",
		_ => $"{Pendientes} incidencias sin enviar al CCO.",
	};

	/// <summary>Concuerda el número con su sustantivo. «1 incidencias» se lee como descuido.</summary>
	private static string Incidencias(int cuantas) =>
		cuantas == 1 ? "1 incidencia" : $"{cuantas} incidencias";

	/// <summary>
	/// Explica por qué no salió nada, nombrando <b>las dos</b> razones cuando las dos se dan.
	/// </summary>
	/// <remarks>
	/// La suma de lo que se nombra aquí tiene que cuadrar con el «sin enviar» del encabezado.
	/// Que no cuadre es lo que hace que el operador desconfíe de la pantalla, y la diferencia
	/// entre las dos razones es la que decide qué hacer: <b>las que esperan salen solas y las
	/// rechazadas no</b>.
	/// </remarks>
	private static string MotivoDeLoOmitido(int porCorregir, int enEspera)
	{
		var rechazadas = $"{Incidencias(porCorregir)} sin enviar: el CCO las rechazó y no saldrán "
			+ "solas. Revise su detalle en la lista.";

		var esperando = $"{Incidencias(enEspera)} esperan su reintento. Saldrán solas.";

		if (porCorregir > 0 && enEspera > 0)
		{
			// Primero lo que exige algo del operador; después lo que se resuelve solo.
			return $"{rechazadas} {esperando}";
		}

		return porCorregir > 0 ? rechazadas : esperando;
	}

	public bool HayRegistros => Registros.Count > 0;

	/// <summary>Indica si hay algo que decir sobre la última sincronización.</summary>
	public bool HayMensajeSincronizacion => !string.IsNullOrEmpty(MensajeSincronizacion);

	/// <summary>
	/// Recarga la cola desde el almacenamiento local.
	/// </summary>
	/// <remarks>
	/// Sin autorización no se lee nada y la pantalla queda vacía: los registros llevan datos de
	/// operación y no deben quedar a la vista de una sesión que ya no vale (CA 4).
	/// </remarks>
	public async Task ActualizarAsync()
	{
		Registros.Clear();

		if (!PuedeConsultar)
		{
			Pendientes = 0;
			NotificarAutorizacion();
			return;
		}

		foreach (var registro in await _cola.ObtenerRegistrosAsync())
		{
			Registros.Add(new RegistroColaVista(registro));
		}

		Pendientes = await _cola.ContarPendientesAsync();
		OnPropertyChanged(nameof(HayRegistros));
		NotificarAutorizacion();
	}

	/// <summary>
	/// Envía los pendientes a Jacob CCO.
	/// </summary>
	/// <remarks>
	/// La comprobación se repite aquí aunque el botón ya esté deshabilitado: ocultar el control
	/// es presentación, y la sesión puede cerrarse entre que la pantalla se pintó y alguien
	/// pulsa. La autorización se decide al ejecutar, no al dibujar.
	/// </remarks>
	[RelayCommand(CanExecute = nameof(PuedeSincronizar))]
	private async Task SincronizarAsync()
	{
		if (!_capacidades.Puede(CapacidadOperador.Sincronizar))
		{
			return;
		}

		Ocupado = true;
		try
		{
			var resultado = await _sincronizador.EjecutarAsync();
			MensajeSincronizacion = TextoDe(resultado);
			await ActualizarAsync();
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>
	/// Traduce el resultado de la sincronización al aviso que lee el operador (CA 10).
	/// </summary>
	/// <remarks>
	/// Los literales son provisionales: Producto no ha fijado los de esta pantalla. Lo que no es
	/// provisional es que <b>cada motivo diga algo distinto</b>: «no se pudo sincronizar» deja al
	/// operador sin saber si esperar, buscar señal o llamar al CCO.
	/// </remarks>
	private static string TextoDe(ResultadoSincronizacion resultado) => resultado.MotivoBloqueo switch
	{
		MotivoNoSincroniza.SinPermiso =>
			"Su cuenta no tiene autorizado sincronizar. Solicite el acceso al CCO.",
		MotivoNoSincroniza.SinEnlaceConJacob =>
			"Sin enlace con el CCO. Lo capturado se conserva y se enviará al recuperar la señal.",
		MotivoNoSincroniza.SinSesion =>
			"La sesión expiró. Vuelva a ingresar para sincronizar.",
		// No es un fallo: el envío ya está corriendo, disparado por la reconexión o por un
		// toque anterior. Decir «no se pudo» mandaría a pulsar otra vez algo que ya funciona.
		MotivoNoSincroniza.YaEnCurso =>
			"El envío ya está en marcha. Espere a que termine.",
		// Nada se intentó, pero eso NO significa que no haya nada. Decir «no hay pendientes»
		// con tres en la lista de arriba es contradecirse en la misma pantalla, y deja al
		// operador sin saber si el botón funcionó.
		// Las dos razones de saltarse un registro pueden darse a la vez, y antes ganaba la
		// primera rama del switch: con dos rechazadas y una esperando, la pantalla decía «3 sin
		// enviar» arriba y hablaba solo de 2 abajo. La que faltaba era justo la que sí va a
		// salir sola, así que el operador la daba por perdida.
		_ when resultado.Intentados == 0
			&& (resultado.OmitidosPorCorregir > 0 || resultado.OmitidosEnEspera > 0) =>
			MotivoDeLoOmitido(resultado.OmitidosPorCorregir, resultado.OmitidosEnEspera),

		_ when resultado.Intentados == 0 =>
			"No hay incidencias sin enviar.",
		_ when resultado.Confirmados == resultado.Intentados =>
			$"{Incidencias(resultado.Confirmados)} enviadas al CCO.",
		// Ninguna salió y el fallo fue del camino: Jacob no llegó a evaluarlas. Decir que las
		// rechazó mandaría al operador a revisar capturas que están bien.
		_ when resultado.Confirmados == 0
			&& resultado.FamiliaUltimoError == FamiliaErrorSincronizacion.Tecnico =>
			"No se pudo contactar al CCO. Lo pendiente se conserva y se reintentará.",

		// Ninguna salió y Jacob las rechazó: se muestra SU mensaje, que dice qué corregir.
		_ when resultado.Confirmados == 0 && resultado.MensajeUltimoError is { } motivo =>
			$"El CCO rechazó el envío: {motivo}",

		// Que unas salgan y otras no es lo normal, no un fallo: el CA 13 pide justamente que
		// una falla no detenga a las demás. Se dice el reparto en vez de un «error» a secas.
		_ => $"{resultado.Confirmados} de {resultado.Intentados} enviadas. El resto sigue pendiente.",
	};

	/// <summary>Reevalúa lo que la sesión autoriza. La sesión puede haber cambiado.</summary>
	private void NotificarAutorizacion()
	{
		OnPropertyChanged(nameof(PuedeConsultar));
		OnPropertyChanged(nameof(PuedeSincronizar));
		SincronizarCommand.NotifyCanExecuteChanged();
	}

	partial void OnPendientesChanged(int value) => OnPropertyChanged(nameof(TextoPendientes));

	partial void OnMensajeSincronizacionChanged(string? value) =>
		OnPropertyChanged(nameof(HayMensajeSincronizacion));

	partial void OnOcupadoChanged(bool value) => NotificarAutorizacion();
}
