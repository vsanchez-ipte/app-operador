using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Las filas de <c>evidencia_local</c>: qué evidencias tiene cada incidencia (JTT-1398).
/// </summary>
/// <remarks>
/// <para>
/// <b>Va aparte de <see cref="IIncidentRepository"/> aunque compartan base.</b> La evidencia se
/// sincroniza y se reintenta por su cuenta —una evidencia fallida no revierte una incidencia ya
/// confirmada—, y meterla en el repositorio de incidencias empujaría a tratarlas como una sola
/// operación, que es justo lo que el diseño de la tabla evita desde que se creó.
/// </para>
/// <para>
/// <b>Aquí no se guarda el binario</b>, solo la ruta al archivo privado: lo prohíbe el documento
/// de arquitectura y lo repite el comentario de la entidad.
/// </para>
/// </remarks>
public interface IRepositorioEvidencias
{
	/// <summary>Registra una evidencia ya copiada al espacio privado.</summary>
	Task AgregarAsync(EvidenciaAdjunta evidencia, CancellationToken cancelacion = default);

    /// <summary>Evidencias de una incidencia, de la más antigua a la más reciente.</summary>
    /// <remarks>
    /// En orden de captura y no de nombre: es como el operador las adjuntó y como espera
    /// reconocerlas en la lista.
    /// </remarks>
	Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default);

	/// <summary>Cuántas evidencias tiene ya esa incidencia. Es lo que el cupo compara.</summary>
	Task<int> ContarDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default);

	/// <summary>Busca una evidencia por su identidad, o <see langword="null"/> si no está.</summary>
	Task<EvidenciaAdjunta?> ObtenerAsync(string uuid, CancellationToken cancelacion = default);

	/// <summary>Borra la fila. El archivo lo retira <see cref="IAlmacenEvidencias"/>.</summary>
	Task EliminarAsync(string uuid, CancellationToken cancelacion = default);

	/// <summary>
	/// Evidencias que todavía no ha confirmado el servidor, de una incidencia (JTT-1398 CA 11).
	/// </summary>
	/// <remarks>
	/// <b>Se piden por incidencia y no en una lista global</b>, porque el envío va encadenado: la
	/// evidencia no puede subir antes que su incidencia, así que se atienden las de cada registro
	/// justo después de que ese registro confirme.
	/// </remarks>
	Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerPendientesDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default);

	/// <summary>Deja constancia de cómo terminó un intento de envío.</summary>
	/// <param name="uuid">La evidencia.</param>
	/// <param name="estado">Dónde queda tras el intento.</param>
	/// <param name="codigoError">Último código de Jacob, o <see langword="null"/> si salió bien.</param>
	/// <param name="mensaje">Lo que dijo Jacob, para la bitácora de intentos; no decide nada.</param>
	Task ActualizarEnvioAsync(
		string uuid,
		EstadoSincronizacion estado,
		string? codigoError,
		string? mensaje = null,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Evidencias del operador que el CCO todavía no confirmó, con la clave local de su
	/// incidencia (JTT-292 CA 4 y 6).
	/// </summary>
	/// <remarks>
	/// El operador se recibe y no se deduce: este repositorio no conoce la sesión, y la
	/// evidencia tampoco lleva operador —lo hereda de su incidencia—. Todo lo que no esté
	/// <c>Sincronizado</c> cuenta, incluido lo fallido.
	/// </remarks>
	Task<IReadOnlyList<EvidenciaPendiente>> ObtenerPendientesDelOperadorAsync(
		string operador,
		CancellationToken cancelacion = default);
}
