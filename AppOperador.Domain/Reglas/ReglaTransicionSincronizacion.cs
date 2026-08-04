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
		[EstadoSincronizacion.Enviando] = [EstadoSincronizacion.Sincronizado, EstadoSincronizacion.Fallido],
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
