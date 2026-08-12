using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
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

	private const string MensajeSinComunicacion = "Sigue sin haber comunicación con Jacob CCO.";
	private const string MensajeRevalidada = "Enlace recuperado. Sesión revalidada.";
	private const string MensajeSesionNoValida = "La sesión ya no es válida. Vuelva a iniciar sesión.";

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
			if (!await _conectividad.ComprobarAsync())
			{
				Detalle = MensajeSinComunicacion;
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

		if (estado != EstadoVigenciaSesion.Expirada)
		{
			return true;
		}

		await Shell.Current.GoToAsync("//acceso");
		return false;
	}

	/// <summary>Reevalúa el estado visible. La llaman las pantallas al aparecer.</summary>
	public void Refrescar()
	{
		OnPropertyChanged(nameof(HayEnlace));
		OnPropertyChanged(nameof(EnModoOffline));
		OnPropertyChanged(nameof(PuedeReintentar));
	}

	/// <summary>
	/// Al recuperar el enlace, revalida la sesión (CA 9).
	/// </summary>
	/// <remarks>
	/// No se espera al resultado: quien dispara el evento es el servicio de conectividad y
	/// bloquearlo dejaría la interfaz colgada mientras dura la petición.
	/// </remarks>
	private void AlCambiarElEnlace(object? origen, bool hayEnlace)
	{
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
