using AppOperador.Domain.Enums;

namespace AppOperador.Domain.Reglas;

/// <summary>
/// Traduce la gravedad de una incidencia a la prioridad con la que se atiende en la
/// cola de sincronización.
/// </summary>
/// <remarks>
/// Función pura del dominio, deliberadamente fuera de cualquier entidad.
/// </remarks>
public static class ReglaPrioridadSincronizacion
{
	/// <summary>
	/// Devuelve la prioridad de cola correspondiente a una gravedad.
	/// </summary>
	/// <remarks>
	/// Regla cerrada: <see cref="Gravedad.Critica"/> produce
	/// <see cref="SyncPriority.Critica"/>. El resto de gravedades produce
	/// <see cref="SyncPriority.Normal"/>; esa segunda mitad es una suposición, porque
	/// el enunciado solo fijó el caso crítico.
	/// </remarks>
	public static SyncPriority Para(Gravedad gravedad) =>
		gravedad == Gravedad.Critica ? SyncPriority.Critica : SyncPriority.Normal;
}
