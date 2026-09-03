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
	/// <summary>
	/// Guarda una incidencia y la deja lista para enviarse.
	/// </summary>
	/// <param name="posicionGps">
	/// Lectura original que produjo el kilómetro. Solo se conserva cuando la fuente es GPS.
	/// </param>
	/// <returns>La clave local asignada, con la forma <c>LOC-######</c>.</returns>
	Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Guarda una incidencia como borrador, fuera de la cola.
	/// </summary>
	/// <remarks>Un borrador nunca se envía: es editable hasta que el operador lo confirme.</remarks>
	/// <returns>La clave local asignada.</returns>
	Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default);

	/// <summary>Borradores del operador que aún no se han confirmado.</summary>
	Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Vuelve a abrir un borrador para seguir editándolo (JTT-1399 CA 8).
	/// </summary>
	/// <returns>
	/// Su contenido, o <c>null</c> si no existe o no es del operador de la sesión.
	/// </returns>
	Task<BorradorIncidencia?> ObtenerBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Guarda los cambios de un borrador sin sacarlo de borrador (JTT-1399 CA 8).
	/// </summary>
	/// <remarks>
	/// Igual que al crearlo, no exige que los datos estén completos: editar un borrador es
	/// seguir capturando, no confirmar.
	/// </remarks>
	/// <returns><c>true</c> si se actualizó; <c>false</c> si no existe o no es suyo.</returns>
	Task<bool> ActualizarBorradorAsync(
		string claveLocal,
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Elimina un borrador (JTT-1399 CA 8).
	/// </summary>
	/// <remarks>
	/// Se borra de verdad, no se marca. Un borrador es trabajo propio a medio escribir y nunca
	/// llegó a Jacob: no hay histórico que conservar ni nadie a quien rendirle cuentas de él.
	/// </remarks>
	/// <returns><c>true</c> si se eliminó; <c>false</c> si no existe o no es suyo.</returns>
	Task<bool> EliminarBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Convierte un borrador en incidencia y la deja en la cola (JTT-1399 CA 8 y 9).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Los parámetros son los mismos que los de <see cref="GuardarAsync"/>, y no por
	/// simetría.</b> El CA 9 exige que convertir ejecute todas las validaciones de envío: al
	/// pedir un <c>Kilometer</c> ya construido y un tipo y una severidad no nulos,
	/// <b>convertir sin validar deja de ser expresable</b> en vez de depender de que alguien se
	/// acuerde de validar antes de llamar.
	/// </para>
	/// <para>
	/// <b>Conserva la clave local y el UUID</b> del borrador. Convertir es que el mismo registro
	/// cambie de estado, no crear otro y borrar el primero: si la clave cambiara, el operador
	/// vería desaparecer un <c>LOC-</c> y aparecer otro sin explicación.
	/// </para>
	/// </remarks>
	/// <returns><c>true</c> si se convirtió; <c>false</c> si no existe o no es suyo.</returns>
	/// <param name="posicionGps">
	/// Lectura original que produjo el kilómetro. Solo se conserva cuando la fuente es GPS.
	/// </param>
	Task<bool> ConvertirBorradorAsync(
		string claveLocal,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default);
}
