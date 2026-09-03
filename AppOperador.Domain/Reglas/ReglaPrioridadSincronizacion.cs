using AppOperador.Domain.Enums;

namespace AppOperador.Domain.Reglas;

/// <summary>
/// Traduce la severidad de una incidencia a la prioridad con la que se atiende en la
/// cola de sincronización (JTT-1394 CA 3).
/// </summary>
/// <remarks>
/// Función pura del dominio, deliberadamente fuera de cualquier entidad.
/// </remarks>
public static class ReglaPrioridadSincronizacion
{
	/// <summary>
	/// Posición del nivel más grave en la escala del catálogo.
	/// </summary>
	/// <remarks>
	/// El catálogo ordena de más grave a menos grave y arranca en 1, así que el primero es el
	/// crítico. Está declarado como constante y no escrito en la comparación para que se vea
	/// que es una convención del catálogo y no un número mágico.
	/// </remarks>
	private const int OrdenDelNivelCritico = 1;

	/// <summary>
	/// Devuelve la prioridad de cola que corresponde a un nivel de severidad.
	/// </summary>
	/// <param name="ordenDeSeveridad">
	/// Posición del nivel en la escala del catálogo, donde <b>menor es más grave</b>.
	/// </param>
	/// <remarks>
	/// <para>
	/// <b>Decide por el orden, no por el nombre ni por un enum propio.</b> Antes comparaba
	/// contra <c>Gravedad.Critica</c>, un valor que la app se inventaba; ahora el catálogo de
	/// Jacob dice cuál es el nivel más grave y la regla lo respeta. Así el CA 3 —«la severidad
	/// Crítica tiene prioridad Crítica»— se cumple sin que nadie mantenga una tabla de
	/// equivalencias, y sigue cumpliéndose el día que alguien renombre el nivel.
	/// </para>
	/// <para>
	/// Se compara con <c>&lt;=</c> y no con <c>==</c> a propósito: un catálogo que empezara a
	/// numerar en 0 seguiría marcando su nivel más grave como crítico, en vez de degradarlo a
	/// normal en silencio.
	/// </para>
	/// </remarks>
	public static SyncPriority Para(int ordenDeSeveridad) =>
		ordenDeSeveridad <= OrdenDelNivelCritico ? SyncPriority.Critica : SyncPriority.Normal;
}
