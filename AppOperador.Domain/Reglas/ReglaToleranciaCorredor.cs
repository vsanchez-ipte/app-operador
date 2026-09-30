namespace AppOperador.Domain.Reglas;

// Valores provisionales: Producto todavía no los confirma.
public static class ReglaToleranciaCorredor
{
	public const double ToleranciaLateralMetros = 60;

	public const double PrecisionMaximaMetros = 50;

	public static bool PrecisionAceptable(double precisionMetros) =>
		!double.IsNaN(precisionMetros)
		&& precisionMetros >= 0
		&& precisionMetros <= PrecisionMaximaMetros;

	public static bool DentroDelCorredor(double desviacionMetros) =>
		!double.IsNaN(desviacionMetros)
		&& desviacionMetros >= 0
		&& desviacionMetros <= ToleranciaLateralMetros;
}
