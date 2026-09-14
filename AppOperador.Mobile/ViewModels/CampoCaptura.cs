namespace AppOperador.Mobile.ViewModels;

/// <summary>Campo del formulario de captura al que pertenece un error de validación.</summary>
/// <remarks>
/// Lo levanta <see cref="CapturaViewModel.CampoConError"/> para que la página se desplace hasta el
/// campo. La página traduce cada valor a su elemento; el ViewModel no conoce la vista.
/// </remarks>
public enum CampoCaptura
{
	Tipo,
	Kilometro,
	Severidad,
	Nota,
	Evidencia,
}
