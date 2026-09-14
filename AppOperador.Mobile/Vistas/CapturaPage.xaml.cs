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

		// El ViewModel dice qué campo falló; la página sabe dónde está y se desplaza hasta él.
		// Con el formulario largo, un aviso al pie no se ve y el operador no sabe qué corregir.
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

		// El KM es el único campo que se teclea corto: se pone el cursor ahí, listo para
		// corregir. En la nota no, porque abrir el teclado taparía el aviso que se acaba de leer.
		if (campo == CampoCaptura.Kilometro)
		{
			EntradaKilometro.Focus();
		}
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
