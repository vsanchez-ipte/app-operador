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

		// La pantalla reaparece al volver de la configuración del sistema. Si el operador
		// concedió ahí el permiso o encendió la ubicación, el bloqueo tiene que desaparecer
		// sin obligarlo a reiniciar la app (JTT-1380).
		await _modelo.RevisarUbicacionAsync();
	}
}
