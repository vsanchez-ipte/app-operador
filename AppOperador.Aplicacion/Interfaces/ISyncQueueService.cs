using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Cola local de registros pendientes de enviar a Jacob CCO.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. La prioridad la
/// determina la regla de dominio <c>ReglaPrioridadSincronizacion</c>; esta interfaz solo
/// expone la cola y el disparo del envío.
/// </remarks>
public interface ISyncQueueService
{
	/// <summary>Registros en cola, del más reciente al más antiguo.</summary>
	Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default);

	/// <summary>Cantidad de registros que siguen esperando envío.</summary>
	Task<int> ContarPendientesAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Intenta enviar los registros pendientes.
	/// </summary>
	/// <returns>Cuántos registros quedaron confirmados por Jacob.</returns>
	Task<int> SincronizarAsync(CancellationToken cancelacion = default);
}
