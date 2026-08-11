using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

	private readonly IConnectivityService _conectividad;
	private readonly ISessionStore _sesiones;
	private readonly RevalidarSesionMovil? _revalidar;

	/// <param name="revalidar">
	/// Revalidación contra Jacob. Opcional: sin canal real no hay a quién preguntar y el
	/// aviso queda como indicador, sin reintento.
	/// </param>
	public EstadoEnlaceViewModel(
		IConnectivityService conectividad,
		ISessionStore sesiones,
		RevalidarSesionMovil? revalidar = null)
	{
		_conectividad = conectividad;
		_sesiones = sesiones;
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
	public bool PuedeReintentar => _revalidar is not null && !Ocupado;

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	/// <summary>Explicación del último intento, cuando hay algo que decir.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HayDetalle))]
	public partial string? Detalle { get; set; }

	/// <summary>Indica si hay explicación que mostrar bajo el aviso.</summary>
	public bool HayDetalle => !string.IsNullOrEmpty(Detalle);

	/// <summary>
	/// Vuelve a preguntar a Jacob si la sesión sigue siendo válida.
	/// </summary>
	/// <remarks>
	/// Es el reintento manual. La recuperación automática llega por
	/// <see cref="IConnectivityService.EnlaceCambio"/>, pero en campo conviene poder forzarla
	/// sin esperar a que el sistema note el cambio.
	/// </remarks>
	[RelayCommand]
	private async Task ReintentarAsync()
	{
		if (_revalidar is null || Ocupado)
		{
			return;
		}

		Ocupado = true;
		OnPropertyChanged(nameof(PuedeReintentar));
		try
		{
			var resultado = await _revalidar.RevalidarAsync();

			Detalle = resultado switch
			{
				{ Exitoso: true } => "Enlace recuperado. Sesión revalidada.",
				{ EsRechazoDefinitivo: true } => "La sesión ya no es válida. Vuelva a iniciar sesión.",
				_ => "Sigue sin haber comunicación con Jacob CCO.",
			};
		}
		finally
		{
			Ocupado = false;
			Refrescar();
		}
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

		if (!hayEnlace || _revalidar is null || _sesiones.Actual is null)
		{
			return;
		}

		_ = ReintentarAsync();
	}
}
