using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Retira una evidencia adjunta: borra el archivo y su fila (JTT-1398 CA 1).
/// </summary>
/// <remarks>
/// <para>
/// El CA 1 pide poder quitar un archivo antes de guardar, y quitar tiene que ser de verdad:
/// <b>si solo se borrara la fila, el archivo se quedaría ocupando el espacio privado para
/// siempre</b>, sin nada que lo referenciara y sin forma de encontrarlo. En un teléfono con
/// ocho videos por incidencia —lo que fijó JTT-289— eso se nota.
/// </para>
/// <para>
/// <b>Se borra de verdad y no se marca.</b> Es la misma decisión que se tomó con los borradores
/// en JTT-1399: una evidencia que nunca llegó a Jacob es trabajo propio a medio hacer, no hay
/// histórico que conservar ni nadie a quien rendirle cuentas de ella.
/// </para>
/// <para>
/// <b>Lo que no hace: quitar una evidencia ya confirmada por el servidor.</b> Ahí el archivo
/// existe del otro lado y borrarlo aquí solo rompería la trazabilidad de este dispositivo.
/// </para>
/// </remarks>
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

	/// <summary>Retira la evidencia. Devuelve si se pudo.</summary>
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

		// Primero el archivo y después la fila. Al revés, un corte entre las dos dejaría el
		// archivo huérfano y sin nada que apuntara a él; en este orden, lo peor que queda es
		// una fila sin archivo, que la cola sí sabe reconocer.
		await _almacen.EliminarAsync(evidencia.RutaArchivo, cancelacion);
		await _evidencias.EliminarAsync(uuid, cancelacion);

		return true;
	}
}
