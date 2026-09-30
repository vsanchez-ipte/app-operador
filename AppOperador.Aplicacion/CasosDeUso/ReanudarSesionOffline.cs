using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

// No es un acceso ni renueva la vigencia: solo reabre una sesión validada en línea cuya ventana sigue abierta.
public sealed class ReanudarSesionOffline
{
	private readonly CustodiaSesionLocal _custodia;
	private readonly ISessionStore _sesiones;
	private readonly ITokenClaims _claims;
	private readonly IClock _reloj;
	private readonly IMonotonicClock _monotonico;
	private readonly IAuditLog _bitacora;

	public ReanudarSesionOffline(
		CustodiaSesionLocal custodia,
		ISessionStore sesiones,
		ITokenClaims claims,
		IClock reloj,
		IMonotonicClock monotonico,
		IAuditLog bitacora)
	{
		_custodia = custodia;
		_sesiones = sesiones;
		_claims = claims;
		_reloj = reloj;
		_monotonico = monotonico;
		_bitacora = bitacora;
	}

	// Mismas condiciones que ReanudarAsync, pero sin efectos: la pantalla nunca anuncia lo que no se puede reanudar.
	public async Task<SesionReanudable?> ConsultarGuardadaAsync(CancellationToken cancelacion = default)
	{
		var guardada = await _custodia.ObtenerPersistidaAsync(cancelacion);
		if (guardada is null)
		{
			return null;
		}

		var token = await _custodia.ObtenerTokenAsync(cancelacion);
		if (string.IsNullOrWhiteSpace(token) || !_claims.Respaldan(guardada.Permisos, token))
		{
			return null;
		}

		var transcurso = TranscursoOffline.Medir(
			guardada.Vigencia.LastValidatedAtUtc,
			_reloj.UtcAhora,
			guardada.MonotonicoAlValidar,
			_monotonico.Transcurrido);

		return guardada.Vigencia.EstaVigenteTras(transcurso)
			? new SesionReanudable(guardada.Operador, guardada.Vigencia.OfflineUntilUtc)
			: null;
	}

	public async Task<ResultadoAcceso> ReanudarAsync(CancellationToken cancelacion = default)
	{
		var guardada = await _custodia.ObtenerPersistidaAsync(cancelacion);
		if (guardada is null)
		{
			return await RechazarAsync("Reanudación negada: no hay validación en línea previa.", null, cancelacion);
		}

		var token = await _custodia.ObtenerTokenAsync(cancelacion);
		if (string.IsNullOrWhiteSpace(token))
		{
			return await RechazarAsync("Reanudación negada: no hay token de la sesión.", guardada.Operador, cancelacion);
		}

		// El archivo local es editable; el token va firmado.
		if (!_claims.Respaldan(guardada.Permisos, token))
		{
			await _custodia.RevocarAsync(cancelacion);
			return await RechazarAsync(
				"Reanudación negada: los permisos guardados no coinciden con el token.", guardada.Operador, cancelacion);
		}

		var transcurso = TranscursoOffline.Medir(
			guardada.Vigencia.LastValidatedAtUtc,
			_reloj.UtcAhora,
			guardada.MonotonicoAlValidar,
			_monotonico.Transcurrido);

		if (!guardada.Vigencia.EstaVigenteTras(transcurso))
		{
			return await RechazarAsync(MotivoDelVencimiento(transcurso), guardada.Operador, cancelacion);
		}

		if (transcurso.RelojRetrocedido)
		{
			// No bloquea —el contador monotónico cubrió el hueco— pero queda asentado.
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"El reloj del dispositivo quedó por detrás de la última validación.",
				cancelacion);
		}

		var sesion = guardada.ComoSesionOperador();
		_sesiones.Guardar(sesion);

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Advertencia,
			$"Modo offline activado. Restan {Formatear(transcurso.RestanteDe(guardada.Vigencia.Ventana))}.",
			cancelacion);

		return ResultadoAcceso.Autorizar(sesion);
	}

	// Para el operador es lo mismo, pero en soporte importa distinguir una ventana vencida de una que no se pudo medir.
	private static string MotivoDelVencimiento(TranscursoOffline transcurso) =>
		transcurso.EsDeterminable
			? "Reanudación negada: la ventana offline ya venció."
			: "Reanudación negada: no se pudo determinar el tiempo transcurrido.";

	private async Task<ResultadoAcceso> RechazarAsync(
		string motivo,
		string? operador,
		CancellationToken cancelacion)
	{
		// Aún no hay sesión abierta: la línea se atribuye al dueño de la sesión guardada.
		await _bitacora.RegistrarAsync(
			OperacionAuditada.CreacionSesion, ResultadoAuditoria.Rechazo, motivo,
			operador: operador, cancelacion: cancelacion);
		return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.SesionOfflineExpirada);
	}

	private static string Formatear(TimeSpan restante) =>
		$"{(int)restante.TotalHours} h {restante.Minutes} min";
}
