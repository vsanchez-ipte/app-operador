using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

// Al vencer se borra la sesión, no lo capturado.
public sealed class ComprobarVigenciaOffline
{
	private readonly CustodiaSesionLocal _custodia;
	private readonly AvisoDeSesionTerminada _aviso;
	private readonly IClock _reloj;
	private readonly IMonotonicClock _monotonico;
	private readonly IAuditLog _bitacora;

	public ComprobarVigenciaOffline(
		CustodiaSesionLocal custodia,
		AvisoDeSesionTerminada aviso,
		IClock reloj,
		IMonotonicClock monotonico,
		IAuditLog bitacora)
	{
		_custodia = custodia;
		_aviso = aviso;
		_reloj = reloj;
		_monotonico = monotonico;
		_bitacora = bitacora;
	}

	public async Task<EstadoVigenciaSesion> ComprobarAsync(CancellationToken cancelacion = default)
	{
		if (_custodia.Actual is null)
		{
			return EstadoVigenciaSesion.SinSesion;
		}

		var guardada = await _custodia.ObtenerPersistidaAsync(cancelacion);
		if (guardada is null)
		{
			// Sin sesión guardada no hay ventana que vigilar (recorrido contra simuladores).
			return EstadoVigenciaSesion.Vigente;
		}

		var transcurso = TranscursoOffline.Medir(
			guardada.Vigencia.LastValidatedAtUtc,
			_reloj.UtcAhora,
			guardada.MonotonicoAlValidar,
			_monotonico.Transcurrido);

		if (guardada.Vigencia.EstaVigenteTras(transcurso))
		{
			return EstadoVigenciaSesion.Vigente;
		}

		return await ExpirarAsync(cancelacion);
	}

	// No se avisa a Jacob: si hubiera enlace, la sesión se habría revalidado en vez de vencer.
	private async Task<EstadoVigenciaSesion> ExpirarAsync(CancellationToken cancelacion)
	{
		// La línea se escribe antes de revocar, para que lleve la sesión que vence.
		await _bitacora.RegistrarAsync(
			NivelAuditoria.Advertencia,
			"Sesión bloqueada: la ventana offline venció. Los registros pendientes se conservan.",
			cancelacion);

		await _custodia.RevocarAsync(cancelacion);
		_aviso.Registrar(MotivoRechazoAcceso.SesionOfflineExpirada);

		return EstadoVigenciaSesion.Expirada;
	}
}

public enum EstadoVigenciaSesion
{
	SinSesion = 0,

	Vigente = 1,

	Expirada = 2,
}
