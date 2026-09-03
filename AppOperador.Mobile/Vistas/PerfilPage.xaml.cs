using AppOperador.Mobile.ViewModels;

namespace AppOperador.Mobile.Vistas;

public partial class PerfilPage : ContentPage
{
	private readonly PerfilViewModel _modelo;

	public PerfilPage(PerfilViewModel modelo)
	{
		InitializeComponent();
		_modelo = modelo;
		BindingContext = modelo;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// La ventana offline pudo vencer mientras la app estaba abierta (JTT-1384).
		// Si vencio, la sesion ya se cerro y no hay nada que cargar.
		if (!await _modelo.Enlace.ComprobarSesionAsync())
		{
			return;
		}

		await _modelo.ActualizarAsync();
	}
}
