using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Aplicacion.CasosDeUso;

// Primero se avisa a Jacob, que necesita el token; el cierre local ocurre aunque no conteste. No borra lo capturado.
public sealed class CerrarSesionMovil
{
	private readonly CustodiaSesionLocal _custodia;
	private readonly IAuditLog _bitacora;
	private readonly ISyncQueueService _cola;
	private readonly IAccesoJacobClient? _jacob;

	public CerrarSesionMovil(
		CustodiaSesionLocal custodia,
		IAuditLog bitacora,
		ISyncQueueService cola,
		IAccesoJacobClient? jacob = null)
	{
		_custodia = custodia;
		_bitacora = bitacora;
		_cola = cola;
		_jacob = jacob;
	}

	public async Task<ResultadoCierreSesion> CerrarAsync(CancellationToken cancelacion = default)
	{
		// Antes de revocar: después la cola se filtra por un operador que ya no existe.
		var pendientes = await ContarPendientesAsync(cancelacion);

		var avisado = await AvisarAJacobAsync(cancelacion);

		// Antes de revocar, mientras la sesión todavía dice de quién era.
		await _bitacora.RegistrarAsync(
			OperacionAuditada.CierreSesion, ResultadoAuditoria.Exito,
			avisado
				? $"Sesión cerrada por el operador. Pendientes conservados: {pendientes}."
				: $"Sesión cerrada sin conexión. Pendientes conservados: {pendientes}.",
			cancelacion: cancelacion);

		await _custodia.RevocarAsync(cancelacion);

		return new ResultadoCierreSesion(avisado, pendientes);
	}

	private async Task<bool> AvisarAJacobAsync(CancellationToken cancelacion)
	{
		if (_jacob is null)
		{
			return false;
		}

		var token = await _custodia.ObtenerTokenAsync(cancelacion);

		return !string.IsNullOrWhiteSpace(token)
			&& await _jacob.CerrarSesionAsync(token, cancelacion);
	}

	private async Task<int> ContarPendientesAsync(CancellationToken cancelacion)
	{
		try
		{
			return await _cola.ContarPendientesAsync(cancelacion);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			return 0;
		}
	}
}
