using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Vigila que la ventana offline siga abierta mientras el operador trabaja (JTT-1384).
/// </summary>
/// <remarks>
/// <para>
/// <b>El hueco que cierra.</b> La reanudación comprueba la vigencia <i>al entrar</i>
/// (JTT-1383), pero una vez dentro nadie la volvía a mirar. Un operador que entrara con
/// siete horas y media consumidas seguía capturando indefinidamente: la ventana vencía a los
/// treinta minutos y la app no se enteraba.
/// </para>
/// <para>
/// Se mide con la misma regla que la reanudación, así que atrasar el reloj o reiniciar el
/// equipo tampoco sirven aquí para estirar la sesión.
/// </para>
/// <para>
/// <b>Al vencer se borra la sesión, no lo capturado.</b> Los registros pendientes, las
/// evidencias y la cola quedan intactos: son del operador y de la unidad, y se envían cuando
/// alguien vuelva a autenticarse (CA 5 y CA 6).
/// </para>
/// </remarks>
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

	/// <summary>Comprueba la vigencia y cierra la sesión si ya venció.</summary>
	public async Task<EstadoVigenciaSesion> ComprobarAsync(CancellationToken cancelacion = default)
	{
		if (_custodia.Actual is null)
		{
			return EstadoVigenciaSesion.SinSesion;
		}

		var guardada = await _custodia.ObtenerPersistidaAsync(cancelacion);
		if (guardada is null)
		{
			// Hay sesión viva pero no queda nada guardado con qué medirla. Es el recorrido
			// contra simuladores, que no persiste sesión: ahí no hay ventana que vigilar.
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

	/// <summary>
	/// Cierra la sesión vencida y deja el aviso para la pantalla de acceso.
	/// </summary>
	/// <remarks>
	/// No se avisa a Jacob: si hubiera enlace, la sesión se habría revalidado en vez de
	/// vencer. Gastar una petición aquí solo retrasaría el bloqueo.
	/// </remarks>
	private async Task<EstadoVigenciaSesion> ExpirarAsync(CancellationToken cancelacion)
	{
		await _custodia.RevocarAsync(cancelacion);
		_aviso.Registrar(MotivoRechazoAcceso.SesionOfflineExpirada);

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Advertencia,
			"Sesión bloqueada: la ventana offline venció. Los registros pendientes se conservan.",
			cancelacion);

		return EstadoVigenciaSesion.Expirada;
	}
}

/// <summary>Desenlace de comprobar la vigencia de la sesión abierta.</summary>
public enum EstadoVigenciaSesion
{
	/// <summary>No hay sesión que vigilar.</summary>
	SinSesion = 0,

	/// <summary>La ventana sigue abierta.</summary>
	Vigente = 1,

	/// <summary>La ventana venció y la sesión quedó cerrada.</summary>
	Expirada = 2,
}
