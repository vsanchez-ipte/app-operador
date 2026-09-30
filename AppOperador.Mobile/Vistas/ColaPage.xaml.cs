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

		// Antes de comprobar la sesión, para enterarse si la sincronización automática termina ahora.
		_modelo.Escuchar();

		// La ventana offline pudo vencer con la app abierta; entonces no hay nada que cargar.
		if (!await _modelo.Enlace.ComprobarSesionAsync())
		{
			return;
		}

		await _modelo.ActualizarAsync();
	}

	// Si no, cada visita dejaría un ViewModel colgado de un servicio que vive lo que la app.
	protected override void OnDisappearing()
	{
		_modelo.DejarDeEscuchar();
		base.OnDisappearing();
	}
}
