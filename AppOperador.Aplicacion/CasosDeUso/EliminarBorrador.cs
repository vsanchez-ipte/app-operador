using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Elimina un borrador con todo lo que tenía adjunto (JTT-1399 CA 8, «eliminarlo»).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un borrador puede tener evidencia</b>: adjuntar una foto sin registro guardado crea uno
/// para no perderla (JTT-1398). Borrar solo la fila del borrador dejaba esas evidencias sin
/// dueño, con su archivo ocupando el espacio privado y sin nada que volviera a mirarlas. La
/// base local lo impide desde el esquema 11 —una evidencia apunta a su incidencia con llave
/// foránea—, así que el orden es el que era correcto desde antes: primero los adjuntos,
/// después el borrador.
/// </para>
/// <para>
/// Cada adjunto se quita por <see cref="QuitarEvidencia"/>, que es quien sabe borrar archivo y
/// fila en el orden seguro.
/// </para>
/// <para>
/// <b>Eliminar deja rastro en la bitácora</b>, igual que adjuntar: una sola línea por borrador,
/// con cuántas evidencias se fueron con él. Sin ella, una foto que el operador adjuntó y después
/// desapareció no tenía explicación en el historial.
/// </para>
/// </remarks>
public sealed class EliminarBorrador
{
	private readonly IIncidentRepository _incidencias;
	private readonly IRepositorioEvidencias _evidencias;
	private readonly QuitarEvidencia _quitarEvidencia;
	private readonly IAuditLog _bitacora;

	public EliminarBorrador(
		IIncidentRepository incidencias,
		IRepositorioEvidencias evidencias,
		QuitarEvidencia quitarEvidencia,
		IAuditLog bitacora)
	{
		_incidencias = incidencias;
		_evidencias = evidencias;
		_quitarEvidencia = quitarEvidencia;
		_bitacora = bitacora;
	}

	/// <summary>Elimina el borrador. Devuelve si existía y era del operador de la sesión.</summary>
	public async Task<bool> EjecutarAsync(string claveLocal, CancellationToken cancelacion = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(claveLocal);

		var borrador = await _incidencias.ObtenerBorradorAsync(claveLocal, cancelacion);

		if (borrador is null)
		{
			return false;
		}

		var adjuntas = await _evidencias.ObtenerDeIncidenciaAsync(borrador.Uuid, cancelacion);
		foreach (var evidencia in adjuntas)
		{
			await _quitarEvidencia.EjecutarAsync(evidencia.Uuid, cancelacion);
		}

		var eliminado = await _incidencias.EliminarBorradorAsync(claveLocal, cancelacion);

		if (eliminado)
		{
			await _bitacora.RegistrarAsync(
				OperacionAuditada.Captura, ResultadoAuditoria.Exito,
				adjuntas.Count switch
				{
					0 => $"Borrador {claveLocal} eliminado.",
					1 => $"Borrador {claveLocal} eliminado con su evidencia adjunta.",
					var n => $"Borrador {claveLocal} eliminado con sus {n} evidencias adjuntas.",
				},
				cancelacion: cancelacion);
		}

		return eliminado;
	}
}
