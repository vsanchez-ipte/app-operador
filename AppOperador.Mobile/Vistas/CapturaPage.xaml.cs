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
		await _modelo.InicializarAsync();
	}
}
