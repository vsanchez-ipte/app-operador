using AppOperador.Domain.Enums;

namespace AppOperador.Domain.Reglas;

public static class ReglaTransicionSincronizacion
{
	// Lo que no está aquí es inválido, incluso pasar de un estado a sí mismo.
	private static readonly Dictionary<EstadoSincronizacion, EstadoSincronizacion[]> TransicionesValidas = new()
	{
		[EstadoSincronizacion.Borrador] = [EstadoSincronizacion.Pendiente],
		[EstadoSincronizacion.Pendiente] = [EstadoSincronizacion.Enviando],

		// Volver a Pendiente rescata el envío que se cortó (app cerrada, batería); el POST es idempotente.
		[EstadoSincronizacion.Enviando] =
		[
			EstadoSincronizacion.Sincronizado,
			EstadoSincronizacion.Fallido,
			EstadoSincronizacion.Pendiente,
		],

		[EstadoSincronizacion.Fallido] = [EstadoSincronizacion.Pendiente],
		[EstadoSincronizacion.Sincronizado] = [],
	};

	public static bool EsTransicionValida(EstadoSincronizacion origen, EstadoSincronizacion destino) =>
		TransicionesValidas.TryGetValue(origen, out var destinos) && destinos.Contains(destino);

	public static bool EsTerminal(EstadoSincronizacion estado) =>
		TransicionesValidas.TryGetValue(estado, out var destinos) && destinos.Length == 0;

	public static IReadOnlyCollection<EstadoSincronizacion> DestinosDesde(EstadoSincronizacion origen) =>
		TransicionesValidas.TryGetValue(origen, out var destinos) ? destinos : [];
}
