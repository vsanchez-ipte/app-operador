using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

public sealed partial class ColaViewModel : ObservableObject
{
	private readonly ISyncQueueService _cola;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly SincronizacionAutomatica _sincronizacionAutomatica;

	private bool _escuchandoLaSincronizacionAutomatica;

	// La espera más corta es de un minuto: medio minuto de resolución basta.
	private static readonly TimeSpan CadaCuantoSeRevisaElReintento = TimeSpan.FromSeconds(30);

	private IDispatcherTimer? _relojDeReintentos;

	[ObservableProperty]
	public partial int Pendientes { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	// Aparte de Ocupado: esto es la pantalla cargando al entrar, no un botón trabajando.
	[ObservableProperty]
	public partial bool Cargando { get; set; }

	// Sin aviso, pulsar «Sincronizar» sin enlace no cambiaría nada visible.
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

	[RelayCommand]
	private async Task CorregirAsync(RegistroColaVista? vista)
	{
		if (vista is null || !vista.MuestraCorregir)
		{
			return;
		}

		await Shell.Current.GoToAsync(
			"//principal/captura",
			new Dictionary<string, object> { [CapturaViewModel.ParametroCorregir] = vista.ClaveLocal });
	}

	// Desde la página y no el constructor: el ViewModel es transitorio y quedaría colgado del servicio.
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

	// Sin este reloj, con señal estable nada envía cuando vence la espera anunciada.
	private void IniciarRelojDeReintentos()
	{
		// Puede no haber despachador todavía; entonces solo se pierde el disparo por vencimiento.
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

	// Sin enlace no se intenta: cada vuelta dejaría una línea idéntica en la bitácora.
	private async void AlTocarRevisarReintentos(object? origen, EventArgs argumentos)
	{
		// No enciende Ocupado: con Jacob caído dejaría el botón apagado casi siempre.
		if (_revisandoReintentos || Ocupado || !HayAlgoQueIntentar())
		{
			return;
		}

		_revisandoReintentos = true;
		try
		{
			// Se sondea: si cayó Jacob y no la red, el estado guardado se quedaría en «sin enlace».
			if (!await Enlace.ComprobarElEnlaceAsync())
			{
				// Sin enlace se repinta igual: la lista puede mostrar un envío que ya terminó.
				await RefrescarAsync();
				return;
			}

			var resultado = await _sincronizador.EjecutarAsync();

			// Solo se avisa de lo que cambió algo: el operador no pidió esta sincronización.
			if (resultado.Confirmados > 0)
			{
				MensajeSincronizacion = TextoDe(resultado);
			}

			await RefrescarAsync();
		}
		// Lo dispara un temporizador: nadie recogería una excepción.
		catch (Exception)
		{
		}
		finally
		{
			_revisandoReintentos = false;
		}
	}

	// Evita que dos vueltas del reloj se encimen; no es Ocupado a propósito, ver arriba.
	private bool _revisandoReintentos;

	private bool HayAlgoQueIntentar()
	{
		var ahora = DateTime.UtcNow;

		return Registros.Any(r => r.Registro.TocaIntentarlo(ahora));
	}

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

	// Llega de otro hilo: la lista se toca en el principal.
	private void AlTerminarUnEnvioAutomatico(object? origen, ResultadoSincronizacion resultado)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			if (resultado.Confirmados > 0)
			{
				MensajeSincronizacion = TextoDe(resultado);
			}

			try
			{
				await RefrescarAsync();
			}
			// async void en el hilo principal: lo que escape cierra la app.
			catch (Exception)
			{
			}
		});
	}

	public bool PuedeConsultar => _capacidades.Puede(CapacidadOperador.ConsultarCola);

	public bool PuedeSincronizar =>
		!Ocupado && _capacidades.Puede(CapacidadOperador.Sincronizar);

	public EstadoEnlaceViewModel Enlace { get; }

	public ObservableCollection<RegistroColaVista> Registros { get; } = [];

	// «Sin enviar» y no «pendientes»: la cuenta incluye rechazadas que no salen solas.
	public string TextoPendientes => Pendientes switch
	{
		0 => "No hay incidencias sin enviar.",
		1 => "1 incidencia sin enviar al CCO.",
		_ => $"{Pendientes} incidencias sin enviar al CCO.",
	};

	private static string Incidencias(int cuantas) =>
		cuantas == 1 ? "1 incidencia" : $"{cuantas} incidencias";

	// Nombra las dos razones cuando se dan, para que cuadre con el encabezado.
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

	public bool HayMensajeSincronizacion => !string.IsNullOrEmpty(MensajeSincronizacion);

	public async Task ActualizarAsync()
	{
		Cargando = true;
		try
		{
			await RefrescarAsync();
		}
		finally
		{
			Cargando = false;
		}
	}

	// Se lee y luego se sustituye, sin await en medio: dos refrescos encimados duplicaban tarjetas.
	private async Task RefrescarAsync()
	{
		if (!PuedeConsultar)
		{
			Registros.Clear();
			Pendientes = 0;
			NotificarAutorizacion();
			return;
		}

		var registros = await _cola.ObtenerRegistrosAsync();
		var pendientes = await _cola.ContarPendientesAsync();

		Registros.Clear();
		foreach (var registro in registros)
		{
			Registros.Add(new RegistroColaVista(registro));
		}

		Pendientes = pendientes;
		OnPropertyChanged(nameof(HayRegistros));
		NotificarAutorizacion();
	}

	// La autorización se revisa al ejecutar, no al dibujar el botón.
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
			// Sondear antes: el estado guardado puede ser viejo y haría falta un segundo toque.
			await Enlace.ComprobarElEnlaceAsync();

			var resultado = await _sincronizador.EjecutarAsync();
			MensajeSincronizacion = TextoDe(resultado);
			await RefrescarAsync();
		}
		finally
		{
			Ocupado = false;
		}
	}

	private static string TextoDe(ResultadoSincronizacion resultado) => resultado.MotivoBloqueo switch
	{
		MotivoNoSincroniza.SinPermiso =>
			"Su cuenta no tiene autorizado sincronizar. Solicite el acceso al CCO.",
		MotivoNoSincroniza.SinEnlaceConJacob =>
			"Sin conexión con CCO. Lo capturado se conserva y se enviará al recuperar la señal.",
		MotivoNoSincroniza.SinSesion =>
			"La sesión expiró. Vuelva a ingresar para sincronizar.",
		// No es un fallo: el envío ya está corriendo.
		MotivoNoSincroniza.YaEnCurso =>
			"El envío ya está en marcha. Espere a que termine.",
		// Nada se intentó, pero puede haber registros esperando o rechazados.
		_ when resultado.Intentados == 0
			&& (resultado.OmitidosPorCorregir > 0 || resultado.OmitidosEnEspera > 0) =>
			MotivoDeLoOmitido(resultado.OmitidosPorCorregir, resultado.OmitidosEnEspera),

		_ when resultado.Intentados == 0 =>
			"No hay incidencias sin enviar.",
		_ when resultado.Confirmados == resultado.Intentados =>
			$"{Incidencias(resultado.Confirmados)} enviadas al CCO.",
		// El fallo fue del camino: Jacob no llegó a evaluarlas.
		_ when resultado.Confirmados == 0
			&& resultado.FamiliaUltimoError == FamiliaErrorSincronizacion.Tecnico =>
			"No se pudo contactar al CCO. Lo pendiente se conserva y se reintentará.",

		// Jacob las rechazó: se muestra su mensaje.
		_ when resultado.Confirmados == 0 && resultado.MensajeUltimoError is { } motivo =>
			$"El CCO rechazó el envío: {motivo}",

		// Que unas salgan y otras no es lo normal: se dice el reparto.
		_ => $"{resultado.Confirmados} de {resultado.Intentados} enviadas. El resto sigue pendiente.",
	};

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
