using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// Solo persiste; la orquestación es SincronizarIncidencias. Solo ve los registros del operador con sesión.
public interface ISyncQueueService
{
	Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default);

	Task<int> ContarPendientesAsync(CancellationToken cancelacion = default);

	// Si el proceso muere a media llamada, el registro queda en Enviando; reenviar no duplica.
	Task<int> RecuperarEnviosInterrumpidosAsync(CancellationToken cancelacion = default);

	// Primero prioridad y después antigüedad. Nunca borradores.
	Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(CancellationToken cancelacion = default);

	Task<IncidenciaEnviable?> ObtenerEnviablePorClaveAsync(
		string claveLocal,
		CancellationToken cancelacion = default);

	Task ActualizarEnvioAsync(ActualizacionEnvio actualizacion, CancellationToken cancelacion = default);

	// Éxitos y fallos: es lo único con qué diagnosticar un teléfono que vuelve de campo.
	Task RegistrarIntentoAsync(
		string uuid,
		bool exito,
		string? codigo,
		string? mensaje,
		CancellationToken cancelacion = default);
}
