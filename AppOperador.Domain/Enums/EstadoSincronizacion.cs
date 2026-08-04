namespace AppOperador.Domain.Enums;

/// <summary>
/// Estado de un elemento dentro de la cola de sincronización.
/// </summary>
public enum EstadoSincronizacion
{
	/// <summary>Capturado pero todavía no encolado. Nunca se envía directamente.</summary>
	Borrador = 1,

	/// <summary>Encolado y a la espera de envío.</summary>
	Pendiente = 2,

	/// <summary>Envío en curso.</summary>
	Enviando = 3,

	/// <summary>Envío confirmado por el servidor. Estado terminal.</summary>
	Sincronizado = 4,

	/// <summary>El envío falló. Admite reintento volviendo a <see cref="Pendiente"/>.</summary>
	Fallido = 5,
}
