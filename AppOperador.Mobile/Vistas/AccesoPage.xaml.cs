using AppOperador.Mobile.ViewModels;

namespace AppOperador.Mobile.Vistas;

public partial class AccesoPage : ContentPage
{
	private readonly AccesoViewModel _modelo;

	public AccesoPage(AccesoViewModel modelo)
	{
		InitializeComponent();
		_modelo = modelo;
		BindingContext = modelo;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _modelo.InicializarAsync();
	}
}
