using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Cierra la sesión del operador sin tocar lo que quedó pendiente de enviar (JTT-1390).
/// </summary>
/// <remarks>
/// <para>
/// El orden importa: primero se avisa a Jacob CCO, que necesita el token para revocarlo, y
/// solo después se borra lo local. Al revés, el aviso se quedaría sin credencial.
/// </para>
/// <para>
/// <b>El cierre local ocurre pase lo que pase.</b> Sin señal, con el servidor caído o con
/// un token ya vencido, la sesión se cierra igual. En campo quedarse sin cobertura es lo
/// normal, y dejar la sesión abierta porque el servidor no contesta sería lo contrario de
/// lo que pide la historia.
/// </para>
/// <para>
/// <b>Lo que no hace, y es deliberado:</b> no borra incidencias, evidencias, intentos de
/// sincronización ni la bitácora local. Cerrar sesión no es desinstalar la app. Lo
/// capturado en campo pertenece al operador y a la unidad, y se sigue enviando cuando
/// alguien vuelva a entrar.
/// </para>
/// </remarks>
public sealed class CerrarSesionMovil
{
	private readonly CustodiaSesionLocal _custodia;
	private readonly IAuditLog _bitacora;
	private readonly ISyncQueueService _cola;
	private readonly IAccesoJacobClient? _jacob;

	/// <param name="custodia">
	/// Rastro local de la sesión. Cerrarla lo borra entero: si quedara la sesión persistida,
	/// bastaría reabrir la app para entrar sin autenticarse (JTT-1383).
	/// </param>
	/// <param name="jacob">
	/// Canal real con Jacob CCO. Opcional a propósito: solo se registra con el API real
	/// encendido, y con los simuladores el cierre es puramente local.
	/// </param>
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

	/// <summary>Termina la sesión y deja la app lista para que entre otro operador.</summary>
	public async Task<ResultadoCierreSesion> CerrarAsync(CancellationToken cancelacion = default)
	{
		// Se cuentan antes de limpiar la sesión: después, la cola ya está filtrada por el
		// operador de la sesión, que ya no existe, y siempre daría cero.
		var pendientes = await ContarPendientesAsync(cancelacion);

		var avisado = await AvisarAJacobAsync(cancelacion);

		await _custodia.RevocarAsync(cancelacion);

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Info,
			avisado
				? $"Sesión cerrada por el operador. Pendientes conservados: {pendientes}."
				: $"Sesión cerrada sin conexión. Pendientes conservados: {pendientes}.",
			cancelacion);

		return new ResultadoCierreSesion(avisado, pendientes);
	}

	/// <summary>
	/// Pide a Jacob que revoque el token.
	/// </summary>
	/// <remarks>
	/// Sin canal real o sin token guardado no hay a quién avisar, y eso no es un error: es
	/// el caso normal de una sesión que nunca llegó a existir en el servidor.
	/// </remarks>
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

	/// <summary>
	/// Cuenta lo que queda por enviar, solo para dejarlo asentado en la bitácora.
	/// </summary>
	/// <remarks>
	/// Un fallo al contar no puede impedir el cierre: se informa cero y la sesión se cierra
	/// igual. La cola no se toca en ningún caso.
	/// </remarks>
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
