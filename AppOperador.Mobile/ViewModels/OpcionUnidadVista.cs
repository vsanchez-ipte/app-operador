using System.Text.RegularExpressions;
using AppOperador.Aplicacion.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppOperador.Mobile.ViewModels;

// Una tarjeta del selector de unidad. El color nunca va solo: el nombre del estado lo acompaña.
public sealed partial class OpcionUnidadVista : ObservableObject
{
	private const string TextoSinEstado = "Sin estado";
	private const string TextoYaNoDisponible = "Ya no disponible";

	private static readonly Regex ColorHexadecimal = new("^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$");

	// Pinceles y no texto: en Android un "#RRGGBB" enlazado a un Brush no se convierte y no pinta nada.
	private static readonly Brush PincelSinEstado = new SolidColorBrush(Color.FromArgb("#7E7E81"));
	private static readonly Brush BordeElegida = new SolidColorBrush(Color.FromArgb("#7A1F3D"));
	private static readonly Brush BordeNormal = new SolidColorBrush(Color.FromArgb("#E0D6C6"));
	private static readonly Brush FondoElegida = new SolidColorBrush(Color.FromArgb("#F7DCE6"));
	private static readonly Brush FondoNormal = new SolidColorBrush(Colors.White);

	public OpcionUnidadVista(UnidadVehicular unidad, string? textoEstado = null)
	{
		Unidad = unidad;
		TextoEstado = textoEstado ?? unidad.Estado?.Nombre ?? TextoSinEstado;
		PincelEstado = unidad.Estado is { } estado && ColorHexadecimal.IsMatch(estado.Color)
			? new SolidColorBrush(Color.FromArgb(estado.Color))
			: PincelSinEstado;
	}

	// Sin estado ni color: el que tenía al listarse ya no es cierto.
	public static OpcionUnidadVista YaNoDisponible(UnidadVehicular unidad) =>
		new(unidad with { Disponible = false, Estado = null }, TextoYaNoDisponible);

	public UnidadVehicular Unidad { get; }

	public string Clave => Unidad.Clave;

	public string Descripcion => Unidad.Descripcion;

	public bool HayDescripcion => !string.IsNullOrWhiteSpace(Unidad.Descripcion);

	public bool Disponible => Unidad.Disponible;

	public string TextoEstado { get; }

	public Brush PincelEstado { get; }

	// Se ve, pero atenuada: el operador sabe que existe y por qué no la puede tomar.
	public double Opacidad => Disponible ? 1 : 0.45;

	[ObservableProperty]
	public partial bool Seleccionada { get; set; }

	public Brush PincelBorde => Seleccionada ? BordeElegida : BordeNormal;

	public Brush PincelFondo => Seleccionada ? FondoElegida : FondoNormal;

	public double GrosorBorde => Seleccionada ? 2 : 1;

	partial void OnSeleccionadaChanged(bool value)
	{
		OnPropertyChanged(nameof(PincelBorde));
		OnPropertyChanged(nameof(PincelFondo));
		OnPropertyChanged(nameof(GrosorBorde));
	}
}
