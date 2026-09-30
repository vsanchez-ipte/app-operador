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

		// En un formulario largo, un aviso al pie no se ve: la página se desplaza al campo.
		_modelo.CampoConError += AlSenalarCampo;
	}

	private async void AlSenalarCampo(object? origen, CampoCaptura campo)
	{
		VisualElement elemento = campo switch
		{
			CampoCaptura.Tipo => CampoTipo,
			CampoCaptura.Kilometro => CampoKilometro,
			CampoCaptura.Severidad => CampoSeveridad,
			CampoCaptura.Nota => CampoNota,
			_ => CampoEvidencia,
		};

		await Desplazable.ScrollToAsync(elemento, ScrollToPosition.Center, animated: true);

		// Solo el KM recibe el foco: en la nota, el teclado taparía el aviso.
		if (campo == CampoCaptura.Kilometro)
		{
			EntradaKilometro.Focus();
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// La ventana offline pudo vencer con la app abierta; entonces no hay nada que cargar.
		if (!await _modelo.Enlace.ComprobarSesionAsync())
		{
			return;
		}

		await _modelo.InicializarAsync();
	}
}
