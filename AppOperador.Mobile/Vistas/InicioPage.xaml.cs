using AppOperador.Mobile.ViewModels;

namespace AppOperador.Mobile.Vistas;

public partial class InicioPage : ContentPage
{
	private readonly InicioViewModel _modelo;

	public InicioPage(InicioViewModel modelo)
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
