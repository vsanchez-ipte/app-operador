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
		await _modelo.ActualizarAsync();
	}
}
