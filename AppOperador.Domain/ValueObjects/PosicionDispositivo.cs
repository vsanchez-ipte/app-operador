namespace AppOperador.Domain.ValueObjects;

/// <summary>
/// Una lectura de ubicación del dispositivo, con todo lo que el CA 2 de JTT-1395 exige.
/// </summary>
/// <remarks>
/// <para>
/// <b>Los cuatro datos van juntos y ninguno es opcional.</b> Una latitud y una longitud sin
/// precisión no se pueden aceptar ni rechazar —no hay con qué decidir si el punto vale— y sin
/// instante no se puede saber si es de ahora o de hace media hora, que en un vehículo en marcha
/// son kilómetros de diferencia.
/// </para>
/// <para>
/// <b>Es también lo que se conserva para auditoría</b> (CA 9). La incidencia guarda la lectura
/// tal como llegó, no solo el kilómetro que se dedujo de ella: si mañana se corrige la geometría
/// del corredor, con la posición original se puede recalcular; con el kilómetro solo, no.
/// </para>
/// </remarks>
/// <param name="Latitud">Grados decimales, entre -90 y 90.</param>
/// <param name="Longitud">Grados decimales, entre -180 y 180.</param>
/// <param name="PrecisionMetros">
/// Radio de incertidumbre que reporta el dispositivo. <b>Menor es mejor.</b>
/// </param>
/// <param name="InstanteUtc">Cuándo se tomó la lectura, en UTC.</param>
public sealed record PosicionDispositivo(
	double Latitud,
	double Longitud,
	double PrecisionMetros,
	DateTime InstanteUtc)
{
	/// <summary>
	/// Crea una posición validando el rango de cada dato.
	/// </summary>
	/// <remarks>
	/// Se valida en el constructor y no en quien la consume porque una posición imposible
	/// —latitud 200, precisión negativa— **proyectada sobre el corredor daría un kilómetro
	/// perfectamente creíble**, y nadie volvería a mirarla.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">Algún valor está fuera de rango.</exception>
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
