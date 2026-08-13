using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Estado del enlace con Jacob CCO, compartido por todas las pantallas (JTT-1383 CA 7 y 8).
/// </summary>
/// <remarks>
/// <para>
/// Se registra como <b>singleton</b> a propósito: el criterio pide que el aviso de modo
/// offline se vea igual en todas las pantallas funcionales, y con una instancia por pantalla
/// cada una mostraría lo suyo.
/// </para>
/// <para>
/// El literal es el mismo que usa la pantalla de acceso y lo fija JTT-279. No se reformula.
/// </para>
/// </remarks>
public sealed partial class EstadoEnlaceViewModel : ObservableObject
{
	/// <summary>Texto exigido por JTT-279 para el modo sin conexión.</summary>
	public const string TextoModoOffline = "Sin conexión / Modo offline";

	/// <summary>Texto del estado en línea, fijado por JTT-1386 CA 2.</summary>
	public const string TextoEnLinea = "En línea";

	/// <summary>Texto del estado mientras se comprueba, fijado por JTT-1386 CA 2.</summary>
	public const string TextoRevalidando = "Revalidando";

	/// <summary>Texto del estado de error del servidor, fijado por JTT-1386 CA 2.</summary>
	public const string TextoErrorDeServicio = "Error de servicio";

	private const string MensajeSinComunicacion = "Sigue sin haber comunicación con Jacob CCO.";
	private const string MensajeRevalidada = "Enlace recuperado. Sesión revalidada.";
	private const string MensajeSesionNoValida = "La sesión ya no es válida. Vuelva a iniciar sesión.";

	/// <summary>
	/// Lo que ve el operador cuando el servidor contestó, pero con un error.
	/// </summary>
	/// <remarks>
	/// <para>
	/// No dice «sin conexión» porque sí la hubo: el servidor respondió. Decirlo mandaba a
	/// revisar la señal cuando el problema estaba en el despliegue —un <c>404</c> porque el
	/// servidor no publicaba la ruta del canal móvil— y costó una sesión entera de diagnóstico.
	/// </para>
	/// <para>
	/// Lleva el código porque es lo primero que va a preguntar quien dé soporte. El resto del
	/// detalle técnico va al registro.
	/// </para>
	/// </remarks>
	private const string MensajeServidorConError =
		"Jacob CCO respondió con un error ({0}). No es su conexión: repórtelo a soporte.";

	/// <summary>
	/// Lo que ve el operador cuando algo falla de forma imprevista.
	/// </summary>
	/// <remarks>
	/// Texto fijo a propósito. Mostrar el mensaje de la excepción le puso delante un
	/// «Cannot access a disposed object»: no le dice nada, no puede hacer nada con eso y
	/// deja a la vista detalles internos. El detalle técnico va al registro.
	/// </remarks>
	private const string MensajeFalloInesperado =
		"No se pudo comprobar el enlace. Intente de nuevo.";

	private readonly IConnectivityService _conectividad;
	private readonly ISessionStore _sesiones;
	private readonly ComprobarVigenciaOffline _vigencia;
	private readonly ILogger<EstadoEnlaceViewModel> _registro;
	private readonly RevalidarSesionMovil? _revalidar;

	/// <summary>
	/// Por qué falló el último sondeo. Distingue «error de servicio» de «sin conexión».
	/// </summary>
	/// <remarks>
	/// Se guarda porque el estado no se puede deducir solo de <c>HayEnlace</c>: sin enlace, que
	/// el servidor haya contestado o no cambia lo que el operador debe hacer.
	/// </remarks>
	private CausaSinEnlace _ultimaCausa = CausaSinEnlace.Ninguna;

	/// <param name="vigencia">
	/// Vigilancia de la ventana offline mientras se trabaja (JTT-1384).
	/// </param>
	/// <param name="registro">
	/// Dónde va el detalle técnico de un fallo. La bitácora local es del operador y se ve en
	/// el perfil; el rastro de una excepción no es para él.
	/// </param>
	/// <param name="revalidar">
	/// Revalidación contra Jacob. Opcional: sin canal real no hay a quién preguntar y el
	/// aviso queda como indicador, sin reintento.
	/// </param>
	public EstadoEnlaceViewModel(
		IConnectivityService conectividad,
		ISessionStore sesiones,
		ComprobarVigenciaOffline vigencia,
		ILogger<EstadoEnlaceViewModel> registro,
		RevalidarSesionMovil? revalidar = null)
	{
		_conectividad = conectividad;
		_sesiones = sesiones;
		_vigencia = vigencia;
		_registro = registro;
		_revalidar = revalidar;

		_conectividad.EnlaceCambio += AlCambiarElEnlace;
	}

	/// <summary>Indica si hay enlace con Jacob en este momento.</summary>
	public bool HayEnlace => _conectividad.HayEnlace;

	/// <summary>
	/// Estado de la comunicación que se pinta en la insignia (JTT-1386 CA 2).
	/// </summary>
	/// <remarks>
	/// La decisión vive en <see cref="ReglaEstadoComunicacion"/>, en la capa de aplicación, para
	/// que tenga pruebas. Aquí solo se le pasa lo que se sabe y se traduce a texto.
	/// </remarks>
	public EstadoComunicacion Estado =>
		ReglaEstadoComunicacion.Determinar(_conectividad.HayEnlace, Ocupado, _ultimaCausa);

	/// <summary>Texto del estado, con los literales que fija la historia.</summary>
	public string TextoEstado => Estado switch
	{
		EstadoComunicacion.EnLinea => TextoEnLinea,
		EstadoComunicacion.Revalidando => TextoRevalidando,
		EstadoComunicacion.ErrorDeServicio => TextoErrorDeServicio,
		_ => TextoModoOffline,
	};

	/// <summary>
	/// Banderas de estado para la vista.
	/// </summary>
	/// <remarks>
	/// Se exponen así, y no como un color o un estilo, para que el ViewModel no decida la
	/// apariencia: la vista muestra la insignia que corresponda. Enlazar un estilo por
	/// disparadores sobre un enum obliga a acrobacias en XAML sin ganar nada.
	/// </remarks>
	public bool EstaEnLinea => Estado == EstadoComunicacion.EnLinea;

	/// <inheritdoc cref="EstaEnLinea"/>
	public bool EstaSinConexion => Estado == EstadoComunicacion.SinConexion;

	/// <inheritdoc cref="EstaEnLinea"/>
	public bool EstaRevalidando => Estado == EstadoComunicacion.Revalidando;

	/// <inheritdoc cref="EstaEnLinea"/>
	public bool EstaEnErrorDeServicio => Estado == EstadoComunicacion.ErrorDeServicio;

	/// <summary>
	/// Fecha y hora <b>local</b> hasta la que se admite trabajo sin conexión (CA 4).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Lleva la fecha y no solo la hora: la ventana puede vencer al día siguiente, y «11:30 p. m.»
	/// a secas no dice cuál.
	/// </para>
	/// <para>
	/// El instante lo calculó el servidor y la app lo adopta sin recalcularlo (CA 5); lo único
	/// que se hace aquí es pasarlo a la hora del dispositivo para presentarlo (DA-10).
	/// </para>
	/// </remarks>
	public string? ExpiraEl =>
		_sesiones.Actual?.Vigencia.OfflineUntilUtc.ToLocalTime().ToString("dd/MM/yyyy, hh:mm tt");

	/// <summary>Indica si hay una expiración que mostrar.</summary>
	public bool HayExpiracion => ExpiraEl is not null;

	/// <summary>
	/// Indica si hay que mostrar el aviso de modo offline.
	/// </summary>
	/// <remarks>
	/// Solo con sesión abierta: sin sesión, la pantalla de acceso ya dice lo suyo y repetirlo
	/// en un aviso no aporta.
	/// </remarks>
	public bool EnModoOffline => !_conectividad.HayEnlace && _sesiones.Actual is not null;

	/// <summary>Texto del aviso.</summary>
	public string Texto => TextoModoOffline;

	/// <summary>Indica si se puede ofrecer el reintento.</summary>
	public bool PuedeReintentar => !Ocupado;

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	/// <summary>Explicación del último intento, cuando hay algo que decir.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HayDetalle))]
	public partial string? Detalle { get; set; }

	/// <summary>Indica si hay explicación que mostrar bajo el aviso.</summary>
	public bool HayDetalle => !string.IsNullOrEmpty(Detalle);

	/// <summary>
	/// Comprueba el enlace y, si lo hay, revalida la sesión.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Los dos pasos, y en ese orden: primero se sondea a Jacob para saber si se le alcanza
	/// (JTT-1391) y solo entonces se le pide que revalide la sesión (JTT-1383 CA 9). Ir
	/// directo a la revalidación gastaría una petición pesada para averiguar algo que la
	/// sonda liviana responde.
	/// </para>
	/// <para>
	/// Es el reintento manual. La recuperación automática llega por
	/// <see cref="IConnectivityService.EnlaceCambio"/>, pero en campo conviene poder forzarla
	/// sin esperar a que el sistema note el cambio.
	/// </para>
	/// </remarks>
	[RelayCommand]
	private async Task ReintentarAsync()
	{
		if (Ocupado)
		{
			return;
		}

		Ocupado = true;
		OnPropertyChanged(nameof(PuedeReintentar));
		try
		{
			// Comprobar el enlace puede levantar el evento de recuperación, y quien lo
			// escucha revalida por su cuenta. Mientras Ocupado esté encendido ese camino se
			// abstiene, así que la revalidación de aquí abajo es la única que corre.
			var sondeo = await _conectividad.ComprobarAsync();
			_ultimaCausa = sondeo.Causa;

			if (!sondeo.HayEnlace)
			{
				Detalle = ExplicarSondeo(sondeo);
				return;
			}

			if (_revalidar is null || _sesiones.Actual is null)
			{
				Detalle = "Enlace recuperado.";
				return;
			}

			var resultado = await _revalidar.RevalidarAsync();

			Detalle = resultado switch
			{
				{ Exitoso: true } => MensajeRevalidada,
				{ EsRechazoDefinitivo: true } => MensajeSesionNoValida,
				_ => MensajeSinComunicacion,
			};
		}
		catch (Exception excepcion)
		{
			// Un reintento que falla es un contratiempo, no un motivo para cerrar la app.
			// Sin esto la excepción sube al hilo de interfaz y la tumba.
			_registro.LogError(excepcion, "Falló el reintento manual del enlace con Jacob CCO.");
			Detalle = MensajeFalloInesperado;
		}
		finally
		{
			Ocupado = false;
			Refrescar();
		}
	}

	/// <summary>
	/// Traduce el sondeo a algo que el operador pueda usar, y deja el detalle en el registro.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Las tres causas piden cosas distintas de quien lee: sin transporte, esperar o moverse;
	/// sesión rechazada, volver a entrar; error del servidor, avisar a soporte. Un único
	/// «sin conexión» las tapaba todas.
	/// </para>
	/// <para>
	/// El registro se lleva siempre el detalle técnico, incluida la ruta y el código, porque en
	/// campo nadie va a copiar un mensaje de pantalla.
	/// </para>
	/// </remarks>
	private string ExplicarSondeo(ResultadoSondeo sondeo)
	{
		_registro.LogWarning(
			"Sondeo del enlace sin éxito. Causa: {Causa}. Código: {Codigo}. Detalle: {Detalle}",
			sondeo.Causa,
			sondeo.CodigoHttp,
			sondeo.Detalle);

		return sondeo.Causa switch
		{
			CausaSinEnlace.RespuestaDeError =>
				string.Format(MensajeServidorConError, sondeo.CodigoHttp),
			CausaSinEnlace.SesionRechazada => MensajeSesionNoValida,
			_ => MensajeSinComunicacion,
		};
	}

	/// <summary>
	/// Comprueba que la sesión siga vigente y expulsa al acceso si venció (JTT-1384).
	/// </summary>
	/// <returns>
	/// <see langword="false"/> si la sesión quedó cerrada, para que la pantalla no siga
	/// cargando datos de una sesión que ya no existe.
	/// </returns>
	/// <remarks>
	/// La llaman las cuatro pantallas al aparecer. No hay temporizador: comprobar al entrar a
	/// cada pantalla acota la ventana a lo que dure una pantalla abierta, y un reloj corriendo
	/// en segundo plano gastaría batería en campo para adelantar minutos.
	/// </remarks>
	public async Task<bool> ComprobarSesionAsync()
	{
		var estado = await _vigencia.ComprobarAsync();

		Refrescar();

		if (estado == EstadoVigenciaSesion.Expirada)
		{
			await Shell.Current.GoToAsync("//acceso");
			return false;
		}

		// Sin esto el estado se queda como lo dejó el último sondeo. Ver las notas del método.
		_ = ActualizarEstadoDelEnlaceAsync();
		return true;
	}

	/// <summary>
	/// Vuelve a preguntar a Jacob CCO en qué estado está el enlace (JTT-1386 CA 10).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Hace falta porque el estado del enlace es un valor guardado, no una medición viva.</b>
	/// Solo se actualiza al sondear o cuando el dispositivo cambia de red, y apagar el servidor
	/// no cambia la red del teléfono: la app seguía anunciando «En línea» contra un API caído
	/// hasta que alguien pulsara «Reintentar».
	/// </para>
	/// <para>
	/// Se lanza <b>sin esperarlo</b>: la pantalla debe aparecer ya, y no retenida hasta veinte
	/// segundos si el servidor no contesta. Al llegar la respuesta se refresca lo visible.
	/// </para>
	/// <para>
	/// <b>No enciende <see cref="Ocupado"/></b>, y es deliberado. Ese indicador lo mira
	/// <see cref="AlCambiarElEnlace"/> para abstenerse de revalidar cuando ya hay un reintento
	/// manual en curso; encenderlo aquí haría que una recuperación detectada por este sondeo
	/// no revalidara la sesión, que es lo que pide JTT-1383 CA 9.
	/// </para>
	/// <para>
	/// Es el mismo criterio de JTT-1384: se comprueba al entrar a cada pantalla y no con un
	/// temporizador, para no dejar un reloj corriendo en segundo plano gastando batería.
	/// </para>
	/// </remarks>
	private async Task ActualizarEstadoDelEnlaceAsync()
	{
		// Sin sesión no hay sonda autenticada que enviar, y la pantalla de acceso ya dice lo suyo.
		if (_sesiones.Actual is null)
		{
			return;
		}

		try
		{
			var sondeo = await _conectividad.ComprobarAsync();
			_ultimaCausa = sondeo.Causa;
		}
		catch (Exception excepcion)
		{
			// Nadie espera este resultado, así que una excepción aquí no tendría quién la
			// recogiera y en el hilo de interfaz cierra la app.
			_registro.LogError(excepcion, "Falló el sondeo del enlace al mostrar una pantalla.");
		}
		finally
		{
			Refrescar();
		}
	}

	/// <summary>Reevalúa el estado visible. La llaman las pantallas al aparecer.</summary>
	public void Refrescar()
	{
		OnPropertyChanged(nameof(HayEnlace));
		OnPropertyChanged(nameof(EnModoOffline));
		OnPropertyChanged(nameof(PuedeReintentar));
		OnPropertyChanged(nameof(ExpiraEl));
		OnPropertyChanged(nameof(HayExpiracion));
		NotificarEstado();
	}

	/// <summary>
	/// Avisa de que cambió el estado de comunicación y todo lo que se deriva de él.
	/// </summary>
	/// <remarks>
	/// Se llama también al empezar y terminar una comprobación: mientras dura, el estado es
	/// «Revalidando», y sin esto la insignia se quedaría con el valor anterior.
	/// </remarks>
	private void NotificarEstado()
	{
		OnPropertyChanged(nameof(Estado));
		OnPropertyChanged(nameof(TextoEstado));
		OnPropertyChanged(nameof(EstaEnLinea));
		OnPropertyChanged(nameof(EstaSinConexion));
		OnPropertyChanged(nameof(EstaRevalidando));
		OnPropertyChanged(nameof(EstaEnErrorDeServicio));
	}

	partial void OnOcupadoChanged(bool value) => NotificarEstado();

	/// <summary>
	/// Al recuperar el enlace, revalida la sesión (CA 9).
	/// </summary>
	/// <remarks>
	/// No se espera al resultado: quien dispara el evento es el servicio de conectividad y
	/// bloquearlo dejaría la interfaz colgada mientras dura la petición.
	/// </remarks>
	private void AlCambiarElEnlace(object? origen, bool hayEnlace)
	{
		// El evento solo dice si hay enlace, no por qué se perdió, así que la causa del sondeo
		// anterior deja de valer. Sin esto, un «Error de servicio» viejo seguiría pintado tras
		// una caída posterior que nada tuvo que ver con el servidor.
		_ultimaCausa = CausaSinEnlace.Ninguna;

		Refrescar();

		// Aquí no se vuelve a sondear: el servicio de conectividad ya lo hizo para poder
		// levantar este evento. Solo queda revalidar.
		if (!hayEnlace || _revalidar is null || _sesiones.Actual is null)
		{
			return;
		}

		// Con un reintento manual en curso, quien revalida es él. Sin esta condición se
		// lanzaban dos revalidaciones a la vez sobre la misma sesión y la misma base local,
		// porque comprobar el enlace es justamente lo que levanta este evento.
		if (Ocupado)
		{
			return;
		}

		_ = RevalidarTrasRecuperarAsync();
	}

	/// <summary>
	/// Revalida la sesión al volver el enlace, sin bloquear a quien avisó.
	/// </summary>
	/// <remarks>
	/// <b>No se espera al resultado</b>, así que aquí no puede escapar ninguna excepción:
	/// nadie estaría para recogerla y en el hilo de interfaz cierra la app.
	/// </remarks>
	private async Task RevalidarTrasRecuperarAsync()
	{
		try
		{
			var resultado = await _revalidar!.RevalidarAsync();

			Detalle = resultado.EsRechazoDefinitivo ? MensajeSesionNoValida : MensajeRevalidada;
		}
		catch (Exception excepcion)
		{
			_registro.LogError(excepcion, "Falló la revalidación automática tras recuperar el enlace.");
			Detalle = MensajeFalloInesperado;
		}
		finally
		{
			Refrescar();
		}
	}
}
