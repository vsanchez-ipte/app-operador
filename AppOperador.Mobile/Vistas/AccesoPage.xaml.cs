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

	// Aquí y no en OnAppearing, que también salta al volver de Ajustes y borraría lo tecleado.
	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		await _modelo.ReiniciarAsync();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _modelo.InicializarAsync();

		// Al volver de Ajustes, el bloqueo debe desaparecer sin reiniciar la app.
		await _modelo.RevisarUbicacionAsync();
	}
}
