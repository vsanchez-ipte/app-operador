using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Persistencia local de las incidencias capturadas por el operador.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. Guardar nunca
/// depende de la conexión: toda captura sobrevive al cierre de la app.
/// </remarks>
public interface IIncidentRepository
{
	/// <summary>Catálogo de tipos de incidencia disponibles para capturar.</summary>
	Task<IReadOnlyList<TipoIncidencia>> ObtenerTiposAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Guarda una incidencia y la deja lista para enviarse.
	/// </summary>
	/// <returns>La clave local asignada, con la forma <c>LOC-######</c>.</returns>
	Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		Gravedad gravedad,
		string nota,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Guarda una incidencia como borrador, fuera de la cola.
	/// </summary>
	/// <remarks>Un borrador nunca se envía: es editable hasta que el operador lo confirme.</remarks>
	/// <returns>La clave local asignada.</returns>
	Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		Gravedad gravedad,
		string nota,
		CancellationToken cancelacion = default);

	/// <summary>Borradores del operador que aún no se han confirmado.</summary>
	Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default);
}
