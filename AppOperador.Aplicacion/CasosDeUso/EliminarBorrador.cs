using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

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

	public async Task<bool> EjecutarAsync(string claveLocal, CancellationToken cancelacion = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(claveLocal);

		var borrador = await _incidencias.ObtenerBorradorAsync(claveLocal, cancelacion);

		if (borrador is null)
		{
			return false;
		}

		// Primero las evidencias: la llave foránea no deja borrar el borrador con ellas.
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
