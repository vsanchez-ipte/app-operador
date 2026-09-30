namespace AppOperador.Domain.ValueObjects;

public sealed record PosicionDispositivo(
	double Latitud,
	double Longitud,
	double PrecisionMetros,
	DateTime InstanteUtc)
{
	// Una posición imposible proyectada sobre el corredor daría un kilómetro creíble.
	public static PosicionDispositivo Crear(
		double latitud,
		double longitud,
		double precisionMetros,
		DateTime instanteUtc)
	{
		if (double.IsNaN(latitud) || latitud is < -90 or > 90)
		{
			throw new ArgumentOutOfRangeException(
				nameof(latitud), latitud, "La latitud debe estar entre -90 y 90 grados.");
		}

		if (double.IsNaN(longitud) || longitud is < -180 or > 180)
		{
			throw new ArgumentOutOfRangeException(
				nameof(longitud), longitud, "La longitud debe estar entre -180 y 180 grados.");
		}

		if (double.IsNaN(precisionMetros) || precisionMetros < 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(precisionMetros), precisionMetros, "La precisión no puede ser negativa.");
		}

		return new PosicionDispositivo(
			latitud,
			longitud,
			precisionMetros,
			DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc));
	}
}
