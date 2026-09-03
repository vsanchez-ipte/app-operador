using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Revalida la sesión al recuperar el enlace con Jacob CCO (JTT-1383 CA 9, 10 y 11).
/// </summary>
/// <remarks>
/// <para>
/// Es el camino de vuelta del modo offline. Pregunta a Jacob si la sesión sigue siendo
/// válida y, si lo es, adopta lo que devuelva: permisos, unidad y una ventana nueva. Ahí
/// —y solo ahí— la vigencia se renueva, porque la calcula el servidor (CA 15).
/// </para>
/// <para>
/// <b>Los tres desenlaces no son dos.</b> Que Jacob niegue la sesión obliga a autenticarse;
/// que no conteste, no. Confundirlos sacaría al operador de una sesión offline vigente cada
/// vez que el enlace parpadee, que es justo lo que el modo offline evita.
/// </para>
/// </remarks>
public sealed class RevalidarSesionMovil
{
	private readonly IAccesoJacobClient _jacob;
	private readonly CustodiaSesionLocal _custodia;
	private readonly AvisoDeSesionTerminada _aviso;
	private readonly ITokenClaims _claims;
	private readonly IMonotonicClock _monotonico;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly IAuditLog _bitacora;
	private readonly ActualizarCatalogoLocal? _catalogos;

	public RevalidarSesionMovil(
		IAccesoJacobClient jacob,
		CustodiaSesionLocal custodia,
		AvisoDeSesionTerminada aviso,
		ITokenClaims claims,
		IMonotonicClock monotonico,
		ISincronizadorIncidencias sincronizador,
		IAuditLog bitacora,
		ActualizarCatalogoLocal? catalogos = null)
	{
		_jacob = jacob;
		_custodia = custodia;
		_aviso = aviso;
		_claims = claims;
		_monotonico = monotonico;
		_sincronizador = sincronizador;
		_bitacora = bitacora;
		_catalogos = catalogos;
	}

	/// <summary>Intenta revalidar la sesión guardada.</summary>
	public async Task<ResultadoRevalidacion> RevalidarAsync(CancellationToken cancelacion = default)
	{
		var guardada = await _custodia.ObtenerPersistidaAsync(cancelacion);
		var token = await _custodia.ObtenerTokenAsync(cancelacion);

		if (guardada is null || string.IsNullOrWhiteSpace(token))
		{
			// Nada que revalidar: no hay sesión que sostener ni que revocar.
			return ResultadoRevalidacion.SinRespuesta("sesion.ausente");
		}

		var resultado = await _jacob.RevalidarAsync(token, cancelacion);

		if (resultado.EsRechazoDefinitivo)
		{
			return await AplicarRechazoAsync(resultado, cancelacion);
		}

		if (!resultado.Exitoso)
		{
			// Sigue sin poder preguntarse. La ventana offline continúa como estaba.
			return resultado;
		}

		return await AplicarConfirmacionAsync(guardada, resultado, token, cancelacion);
	}

	/// <summary>
	/// Adopta lo que devolvió Jacob y dispara la sincronización (CA 10).
	/// </summary>
	/// <remarks>
	/// Los permisos se cotejan otra vez contra el token antes de adoptarlos, igual que en el
	/// acceso (JTT-1379 CA 8). Si no cuadran, se trata como negativa: es preferible pedir
	/// autenticación a conceder capacidades que el token no respalda.
	/// </remarks>
	private async Task<ResultadoRevalidacion> AplicarConfirmacionAsync(
		SesionOfflinePersistida guardada,
		ResultadoRevalidacion resultado,
		string token,
		CancellationToken cancelacion)
	{
		var permisos = resultado.Permisos!;

		if (!_claims.Respaldan(permisos, token))
		{
			return await AplicarRechazoAsync(
				ResultadoRevalidacion.Negada(MotivoRechazoAcceso.SesionRevocada, "permisos.sinrespaldo"),
				cancelacion);
		}

		var renovada = guardada with
		{
			Rol = string.IsNullOrWhiteSpace(resultado.Rol) ? guardada.Rol : resultado.Rol,
			Unidad = resultado.Unidad ?? guardada.Unidad,
			Permisos = permisos,
			Vigencia = resultado.Vigencia!,

			// Referencia nueva para medir la ventana nueva. Sin actualizarla, el transcurso
			// se seguiría contando desde el acceso original.
			MonotonicoAlValidar = _monotonico.Transcurrido,
		};

		// Sin token nuevo: la revalidación no emite uno y sobrescribirlo dejaría la sesión
		// sin credencial.
		await _custodia.AbrirAsync(renovada, accessToken: null, cancelacion);

		// El catálogo se refresca aquí, que es la otra mitad del CA 4 de JTT-1394: la
		// revalidación es la validación en línea que ocurre sin que el operador vuelva a
		// entrar. Su fallo no se propaga: la sesión ya quedó renovada.
		if (_catalogos is not null)
		{
			await _catalogos.EjecutarAsync(token, cancelacion);
		}

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Info,
			"Sesión revalidada con Jacob CCO. Ventana offline renovada.",
			cancelacion);

		await SincronizarAsync(cancelacion);

		return resultado;
	}

	/// <summary>
	/// Cierra la sesión local cuando Jacob la niega (CA 11).
	/// </summary>
	/// <remarks>
	/// Bloquea nuevas operaciones quitando la sesión, pide autenticación y <b>no toca la
	/// cola</b>: lo capturado en campo es del operador y se envía cuando alguien vuelva a
	/// entrar.
	/// </remarks>
	private async Task<ResultadoRevalidacion> AplicarRechazoAsync(
		ResultadoRevalidacion resultado,
		CancellationToken cancelacion)
	{
		await _custodia.RevocarAsync(cancelacion);

		// Para que la pantalla de acceso pueda decir por qué se cerró la sesión, en vez de
		// aparecer en blanco (JTT-1383 CA 11: «solicita autenticación»).
		_aviso.Registrar(resultado.Motivo ?? MotivoRechazoAcceso.SesionRevocada);

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Advertencia,
			$"Sesión negada por Jacob CCO al revalidar ({resultado.CodigoError ?? "sin código"}). " +
			"Los registros pendientes se conservan.",
			cancelacion);

		return resultado;
	}

	/// <summary>
	/// Envía lo que quedó pendiente durante el corte.
	/// </summary>
	/// <remarks>
	/// Un fallo aquí no puede deshacer la revalidación: la sesión ya está renovada y la cola
	/// se reintenta sola más adelante.
	/// </remarks>
	private async Task SincronizarAsync(CancellationToken cancelacion)
	{
		try
		{
			await _sincronizador.EjecutarAsync(cancelacion);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"No se pudo iniciar la sincronización tras revalidar.",
				cancelacion);
		}
	}
}
