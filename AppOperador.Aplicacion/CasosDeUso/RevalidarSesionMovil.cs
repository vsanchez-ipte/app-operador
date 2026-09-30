using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Aplicacion.CasosDeUso;

// Negar la sesión obliga a autenticarse; no contestar, no. Solo aquí se renueva la vigencia.
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

	private async Task<ResultadoRevalidacion> AplicarConfirmacionAsync(
		SesionOfflinePersistida guardada,
		ResultadoRevalidacion resultado,
		string token,
		CancellationToken cancelacion)
	{
		var permisos = resultado.Permisos!;

		// Si los permisos no cuadran con el token, se trata como negativa.
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

			// Referencia nueva: si no, el transcurso se seguiría contando desde el acceso original.
			MonotonicoAlValidar = _monotonico.Transcurrido,
		};

		// Sin token nuevo: la revalidación no emite uno.
		await _custodia.AbrirAsync(renovada, accessToken: null, cancelacion);

		// La revalidación también es validación en línea, así que refresca el catálogo.
		if (_catalogos is not null)
		{
			await _catalogos.EjecutarAsync(token, cancelacion);
		}

		await _bitacora.RegistrarAsync(
			OperacionAuditada.RevalidacionSesion, ResultadoAuditoria.Exito,
			"Sesión revalidada con Jacob CCO. Ventana offline renovada.",
			cancelacion: cancelacion);

		await SincronizarAsync(cancelacion);

		return resultado;
	}

	// No toca la cola: lo capturado se envía cuando alguien vuelva a entrar.
	private async Task<ResultadoRevalidacion> AplicarRechazoAsync(
		ResultadoRevalidacion resultado,
		CancellationToken cancelacion)
	{
		// La línea se escribe antes de revocar, para que lleve la sesión que se está cerrando.
		await _bitacora.RegistrarAsync(
			OperacionAuditada.RevalidacionSesion, ResultadoAuditoria.Rechazo,
			"Sesión negada por Jacob CCO al revalidar. Los registros pendientes se conservan.",
			motivoCodigo: resultado.CodigoError, cancelacion: cancelacion);

		await _custodia.RevocarAsync(cancelacion);

		_aviso.Registrar(resultado.Motivo ?? MotivoRechazoAcceso.SesionRevocada);

		return resultado;
	}

	// Un fallo aquí no deshace la revalidación; la cola se reintenta sola.
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
