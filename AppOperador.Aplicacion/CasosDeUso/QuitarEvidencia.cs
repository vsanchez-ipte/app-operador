using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.CasosDeUso;

public sealed class QuitarEvidencia
{
	private readonly IRepositorioEvidencias _evidencias;
	private readonly IAlmacenEvidencias _almacen;
	private readonly IAuditLog _bitacora;

	public QuitarEvidencia(
		IRepositorioEvidencias evidencias,
		IAlmacenEvidencias almacen,
		IAuditLog bitacora)
	{
		_evidencias = evidencias;
		_almacen = almacen;
		_bitacora = bitacora;
	}

	public async Task<bool> EjecutarAsync(string uuid, CancellationToken cancelacion = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(uuid);

		var evidencia = await _evidencias.ObtenerAsync(uuid, cancelacion);

		if (evidencia is null)
		{
			return false;
		}

		if (evidencia.Estado == EstadoSincronizacion.Sincronizado)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Se intentó quitar la evidencia {uuid}, que el CCO ya confirmó.",
				cancelacion);

			return false;
		}

		// Primero el archivo: un corte deja a lo sumo una fila sin archivo, que la cola sí reconoce.
		await _almacen.EliminarAsync(evidencia.RutaArchivo, cancelacion);
		await _evidencias.EliminarAsync(uuid, cancelacion);

		return true;
	}
}
