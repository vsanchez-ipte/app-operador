using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Reanuda una sesión ya validada en línea, sin volver a contactar a Jacob (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// <b>No es un acceso.</b> Solo prospera si hubo una validación en línea exitosa y su
/// ventana sigue abierta; el primer ingreso siempre exige enlace (CA 1 y CA 2).
/// </para>
/// <para>
/// La ventana no se mide con el reloj del dispositivo, que el operador puede mover, sino
/// con <see cref="TranscursoOffline"/>, que combina el reloj con el contador monotónico del
/// sistema. Ni atrasar la hora ni reiniciar el equipo alargan la sesión (CA 5, 13 y 14).
/// </para>
/// <para>
/// <b>Nada se renueva aquí.</b> Reanudar no extiende la vigencia: eso solo lo hace una
/// validación del servidor (CA 15).
/// </para>
/// </remarks>
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

	/// <summary>Intenta abrir la app con la sesión guardada.</summary>
	public async Task<ResultadoAcceso> ReanudarAsync(CancellationToken cancelacion = default)
	{
		var guardada = await _custodia.ObtenerPersistidaAsync(cancelacion);
		if (guardada is null)
		{
			return await RechazarAsync("Reanudación negada: no hay validación en línea previa.", cancelacion);
		}

		// Sin token no se podría hablar con Jacob al recuperar el enlace, y los permisos
		// quedarían sin nada que los respalde.
		var token = await _custodia.ObtenerTokenAsync(cancelacion);
		if (string.IsNullOrWhiteSpace(token))
		{
			return await RechazarAsync("Reanudación negada: no hay token de la sesión.", cancelacion);
		}

		// Los permisos guardados se vuelven a cotejar contra el token (JTT-1379 CA 8): el
		// archivo local es editable, el token va firmado.
		if (!_claims.Respaldan(guardada.Permisos, token))
		{
			await _custodia.RevocarAsync(cancelacion);
			return await RechazarAsync(
				"Reanudación negada: los permisos guardados no coinciden con el token.", cancelacion);
		}

		var transcurso = TranscursoOffline.Medir(
			guardada.Vigencia.LastValidatedAtUtc,
			_reloj.UtcAhora,
			guardada.MonotonicoAlValidar,
			_monotonico.Transcurrido);

		if (!guardada.Vigencia.EstaVigenteTras(transcurso))
		{
			return await RechazarAsync(MotivoDelVencimiento(transcurso), cancelacion);
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

	/// <summary>
	/// Distingue una ventana agotada de una que no se pudo medir.
	/// </summary>
	/// <remarks>
	/// Para el operador el desenlace es el mismo —autenticarse de nuevo— pero en la bitácora
	/// no lo es: «no se pudo medir» apunta a que el reloj se movió y el equipo se reinició,
	/// y eso conviene poder distinguirlo al dar soporte.
	/// </remarks>
	private static string MotivoDelVencimiento(TranscursoOffline transcurso) =>
		transcurso.EsDeterminable
			? "Reanudación negada: la ventana offline ya venció."
			: "Reanudación negada: no se pudo determinar el tiempo transcurrido.";

	private async Task<ResultadoAcceso> RechazarAsync(string motivo, CancellationToken cancelacion)
	{
		await _bitacora.RegistrarAsync(NivelAuditoria.Advertencia, motivo, cancelacion);
		return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.SesionOfflineExpirada);
	}

	private static string Formatear(TimeSpan restante) =>
		$"{(int)restante.TotalHours} h {restante.Minutes} min";
}
