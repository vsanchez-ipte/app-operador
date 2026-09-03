using AppOperador.Domain.Enums;

namespace AppOperador.Domain.Reglas;

/// <summary>
/// Transiciones admitidas entre estados de la cola de sincronización.
/// </summary>
/// <remarks>
/// Función pura del dominio, deliberadamente fuera de cualquier entidad.
/// </remarks>
public static class ReglaTransicionSincronizacion
{
	// Único lugar donde vive el grafo de transiciones. Todo lo que no esté aquí
	// es inválido, incluidas las transiciones de un estado a sí mismo.
	private static readonly Dictionary<EstadoSincronizacion, EstadoSincronizacion[]> TransicionesValidas = new()
	{
		[EstadoSincronizacion.Borrador] = [EstadoSincronizacion.Pendiente],
		[EstadoSincronizacion.Pendiente] = [EstadoSincronizacion.Enviando],

		// Enviando vuelve a Pendiente, y esa arista no es un adorno del grafo: es la salida de
		// un envío que nunca terminó. Sin ella, una incidencia a la que se le cierra la app o
		// se le acaba la batería a media llamada se queda en Enviando para siempre —no la toma
		// la cola, no la cuenta el contador y no la reintenta nadie—, sin perderse pero sin
		// llegar jamás a Jacob. Reenviarla no duplica: el POST es idempotente por uuid.
		[EstadoSincronizacion.Enviando] =
		[
			EstadoSincronizacion.Sincronizado,
			EstadoSincronizacion.Fallido,
			EstadoSincronizacion.Pendiente,
		],

		[EstadoSincronizacion.Fallido] = [EstadoSincronizacion.Pendiente],
		[EstadoSincronizacion.Sincronizado] = [],
	};

	/// <summary>
	/// Indica si se puede pasar de <paramref name="origen"/> a <paramref name="destino"/>.
	/// </summary>
	public static bool EsTransicionValida(EstadoSincronizacion origen, EstadoSincronizacion destino) =>
		TransicionesValidas.TryGetValue(origen, out var destinos) && destinos.Contains(destino);

	/// <summary>
	/// Indica si un estado es terminal, es decir, si no admite ninguna transición de salida.
	/// </summary>
	public static bool EsTerminal(EstadoSincronizacion estado) =>
		TransicionesValidas.TryGetValue(estado, out var destinos) && destinos.Length == 0;

	/// <summary>
	/// Devuelve los destinos admitidos desde <paramref name="origen"/>.
	/// </summary>
	public static IReadOnlyCollection<EstadoSincronizacion> DestinosDesde(EstadoSincronizacion origen) =>
		TransicionesValidas.TryGetValue(origen, out var destinos) ? destinos : [];
}
