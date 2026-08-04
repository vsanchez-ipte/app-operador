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
		await _modelo.ActualizarAsync();
	}
}
