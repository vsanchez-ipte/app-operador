using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Cola de sincronización simulada.
/// </summary>
/// <remarks>
/// Respeta el grafo de estados del dominio: recorre
/// <c>Pendiente → Enviando → Sincronizado</c> apoyándose en
/// <see cref="ReglaTransicionSincronizacion"/> en lugar de asignar estados a mano, y los
/// borradores nunca entran al envío. Al sincronizar asigna un folio central
/// <c>INC-####</c>, que antes no existía.
/// </remarks>
public sealed class ColaSincronizacionEnMemoria : ISyncQueueService
{
	private readonly AlmacenRegistrosEnMemoria _almacen;
	private readonly IConnectivityService _conectividad;
	private readonly IAuditLog _bitacora;

	private int _folio = 9_959;

	public ColaSincronizacionEnMemoria(
		AlmacenRegistrosEnMemoria almacen,
		IConnectivityService conectividad,
		IAuditLog bitacora)
	{
		_almacen = almacen;
		_conectividad = conectividad;
		_bitacora = bitacora;
	}

	public Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default)
	{
		// Los borradores se listan aparte, en la pantalla de Captura.
		IReadOnlyList<RegistroCola> registros = _almacen.Todos()
			.Where(r => r.Estado != EstadoSincronizacion.Borrador)
			.ToList();

		return Task.FromResult(registros);
	}

	public Task<int> ContarPendientesAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(_almacen.Contar(EstadoSincronizacion.Pendiente));

	public async Task<int> SincronizarAsync(CancellationToken cancelacion = default)
	{
		if (!_conectividad.HayEnlace)
		{
			await _bitacora.RegistrarAsync(NivelAuditoria.Advertencia, "Sync omitido: sin enlace con CCO.", cancelacion);
			return 0;
		}

		// Las críticas primero; entre iguales, las más antiguas antes, para que las
		// normales no se queden esperando indefinidamente.
		var porEnviar = _almacen.Todos()
			.Where(r => r.Estado == EstadoSincronizacion.Pendiente)
			.OrderByDescending(r => r.Prioridad == SyncPriority.Critica)
			.Reverse()
			.ToList();

		var confirmados = 0;
		foreach (var registro in porEnviar)
		{
			if (!ReglaTransicionSincronizacion.EsTransicionValida(registro.Estado, EstadoSincronizacion.Enviando))
			{
				continue;
			}

			// El folio central solo existe a partir de la confirmación de Jacob.
			var sincronizado = registro with
			{
				Estado = EstadoSincronizacion.Sincronizado,
				FolioCentral = registro.Clase == ClaseRegistro.Incidencia ? $"INC-{++_folio}" : null,
			};

			_almacen.Reemplazar(sincronizado);
			confirmados++;
		}

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Info,
			$"Sync intentado: {confirmados}/{porEnviar.Count} registros creados en Incidencias.",
			cancelacion);

		return confirmados;
	}
}
