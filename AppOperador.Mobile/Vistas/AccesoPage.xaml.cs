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

	/// <summary>
	/// Limpia la pantalla cada vez que se navega a ella (JTT-1390).
	/// </summary>
	/// <remarks>
	/// Va aquí y no en <c>OnAppearing</c> a propósito: aquel también se dispara al volver de
	/// los ajustes del sistema, y ahí borrar lo tecleado sería una molestia, no una medida de
	/// seguridad.
	/// </remarks>
	protected override async void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		await _modelo.ReiniciarAsync();
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
