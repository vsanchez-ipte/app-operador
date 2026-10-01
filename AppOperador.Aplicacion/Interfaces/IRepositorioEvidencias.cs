using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Interfaces;

// Aparte de IIncidentRepository: la evidencia se reintenta por su cuenta. Guarda la ruta, nunca el binario.
public interface IRepositorioEvidencias
{
	Task AgregarAsync(EvidenciaAdjunta evidencia, CancellationToken cancelacion = default);

    // En orden de captura: como el operador las adjuntó.
	Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default);

	// Es lo que el cupo compara.
	Task<int> ContarDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default);

	Task<EvidenciaAdjunta?> ObtenerAsync(string uuid, CancellationToken cancelacion = default);

	// El archivo lo retira IAlmacenEvidencias.
	Task EliminarAsync(string uuid, CancellationToken cancelacion = default);

	// Por incidencia: la evidencia sube justo después de que su incidencia confirme.
	Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerPendientesDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default);

	// El mensaje es para la bitácora de intentos; no decide nada.
	Task ActualizarEnvioAsync(
		string uuid,
		EstadoSincronizacion estado,
		string? codigoError,
		string? mensaje = null,
		CancellationToken cancelacion = default);

	// El operador se recibe: la evidencia no lo lleva y el repositorio no conoce la sesión.
	Task<IReadOnlyList<EvidenciaPendiente>> ObtenerPendientesDelOperadorAsync(
		string operador,
		CancellationToken cancelacion = default);

	Task<IReadOnlyList<EvidenciaRezagada>> ObtenerRezagadasDelOperadorAsync(
		string operador,
		CancellationToken cancelacion = default);
}
