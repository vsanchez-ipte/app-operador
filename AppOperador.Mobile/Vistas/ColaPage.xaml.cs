using AppOperador.Mobile.ViewModels;

namespace AppOperador.Mobile.Vistas;

public partial class ColaPage : ContentPage
{
	private readonly ColaViewModel _modelo;

	public ColaPage(ColaViewModel modelo)
	{
		InitializeComponent();
		_modelo = modelo;
		BindingContext = modelo;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Antes de la comprobacion de sesion: si la sincronizacion automatica termina justo
		// ahora, la lista debe enterarse igual (JTT-1406).
		_modelo.Escuchar();

		// La ventana offline pudo vencer mientras la app estaba abierta (JTT-1384).
		// Si vencio, la sesion ya se cerro y no hay nada que cargar.
		if (!await _modelo.Enlace.ComprobarSesionAsync())
		{
			return;
		}

		await _modelo.ActualizarAsync();
	}

	/// <remarks>
	/// Sin esto, cada visita a la pantalla dejaria un ViewModel mas colgado del servicio de
	/// sincronizacion, que vive lo que la aplicacion.
	/// </remarks>
	protected override void OnDisappearing()
	{
		_modelo.DejarDeEscuchar();
		base.OnDisappearing();
	}
}
