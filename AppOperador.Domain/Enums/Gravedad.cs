namespace AppOperador.Domain.Enums;

/// <summary>
/// Gravedad de una incidencia.
/// </summary>
/// <remarks>
/// El prompt de la historia solo fijó el valor <see cref="Critica"/>, que es el único
/// que participa en una regla cerrada (ver
/// <c>Reglas.ReglaPrioridadSincronizacion</c>). Los otros tres niveles son una
/// suposición pendiente de confirmar con el documento de arquitectura.
/// </remarks>
public enum Gravedad
{
	Baja = 1,
	Media = 2,
	Alta = 3,

	/// <summary>Único valor fijado por la regla de negocio.</summary>
	Critica = 4,
}
