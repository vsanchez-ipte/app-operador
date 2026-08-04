using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Ubicación del dispositivo traducida a la coordenada que usa el negocio: el kilómetro.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. La app no razona en
/// latitud y longitud: si la lectura cae dentro del corredor, se convierte a
/// <see cref="Kilometer"/> con fuente <see cref="KilometerSource.GPS"/>; si no, el
/// operador captura el kilómetro a mano. La geometría del corredor sigue abierta (DA-12).
/// </remarks>
public interface ILocationService
{
	/// <summary>Indica si el servicio de ubicación está habilitado y autorizado.</summary>
	Task<bool> HayPermisoAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Intenta obtener el kilómetro actual a partir del GPS.
	/// </summary>
	/// <returns>
	/// El kilómetro leído, o <see langword="null"/> si no hay lectura válida o la posición
	/// queda fuera del corredor. En ese caso corresponde captura manual.
	/// </returns>
	Task<Kilometer?> ObtenerKilometroAsync(CancellationToken cancelacion = default);
}
