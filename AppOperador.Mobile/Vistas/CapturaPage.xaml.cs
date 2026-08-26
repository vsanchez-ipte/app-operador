using AppOperador.Mobile.ViewModels;

namespace AppOperador.Mobile.Vistas;

public partial class CapturaPage : ContentPage
{
	private readonly CapturaViewModel _modelo;

	public CapturaPage(CapturaViewModel modelo)
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

		await _modelo.InicializarAsync();
	}
}
