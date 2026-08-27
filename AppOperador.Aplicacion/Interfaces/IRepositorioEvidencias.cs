using AppOperador.Aplicacion.Modelos;

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
}
