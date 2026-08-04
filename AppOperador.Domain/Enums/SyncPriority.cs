namespace AppOperador.Domain.Enums;

/// <summary>
/// Prioridad con la que un elemento se atiende en la cola de sincronización.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. El prompt solo
/// fijó el valor <see cref="Critica"/>; <see cref="Normal"/> es la suposición para
/// todo lo demás, pendiente de confirmar.
/// </remarks>
public enum SyncPriority
{
	/// <summary>Prioridad ordinaria de la cola.</summary>
	Normal = 1,

	/// <summary>Se atiende antes que cualquier elemento <see cref="Normal"/>.</summary>
	Critica = 2,
}
