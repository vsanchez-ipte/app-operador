using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Aplicacion.CasosDeUso;

// El desafío vive solo aquí y en memoria; cada pantalla de acceso tiene su propia instancia.
public sealed class AbrirSesionMovil
{
	private readonly IAccesoJacobClient _jacob;
	private readonly CustodiaSesionLocal _custodia;
	private readonly ActualizarCatalogoLocal? _catalogos;
	private readonly IConnectivityService? _conectividad;
	private readonly DatosDeInstalacion _instalacion;
	private readonly IMonotonicClock _monotonico;

	private string? _desafio;

	public AbrirSesionMovil(
		IAccesoJacobClient jacob,
		CustodiaSesionLocal custodia,
		DatosDeInstalacion instalacion,
		IMonotonicClock monotonico,
		IAuditLog bitacora,
		ActualizarCatalogoLocal? catalogos = null,
		IConnectivityService? conectividad = null)
	{
		_jacob = jacob;
		_custodia = custodia;
		_instalacion = instalacion;
		_monotonico = monotonico;
		_bitacora = bitacora;
		_catalogos = catalogos;
		_conectividad = conectividad;
	}

	private readonly IAuditLog _bitacora;

	// Aún no hay sesión: la bitácora atribuye el intento al correo tecleado.
	private string? _emailEnCurso;

	public async Task<ResultadoPreauth> IdentificarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default)
	{
		var resultado = await _jacob.PreautenticarAsync(email, contrasena, cancelacion);
		AnotarEnlace(resultado.Motivo);
		// Un rechazo descarta el desafío anterior.
		_desafio = resultado.Exitoso ? resultado.ChallengeId : null;
		_emailEnCurso = email;

		await AplicarRevocacionAsync(resultado.Motivo, cancelacion);

		if (resultado.Exitoso)
		{
			await _bitacora.RegistrarAsync(
				OperacionAuditada.Autenticacion, ResultadoAuditoria.Exito,
				$"Credenciales validadas por Jacob CCO para {email}.",
				operador: email, cancelacion: cancelacion);
		}
		else
		{
			await _bitacora.RegistrarAsync(
				OperacionAuditada.Autenticacion, ResultadoAuditoria.Rechazo,
				$"Acceso denegado para {email}: {resultado.Motivo}.",
				motivoCodigo: resultado.CodigoError, operador: email, cancelacion: cancelacion);
		}

		return resultado;
	}

	public async Task<ResultadoLogin> AbrirAsync(
		UnidadVehicular unidad,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(_desafio))
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.DesafioNoValido, "desafio.ausente");
		}

		var desafio = _desafio;
		// Un solo uso: se descarta pase lo que pase.
		_desafio = null;

		var resultado = await _jacob.CompletarAccesoAsync(desafio, unidad.Id, cancelacion);
		AnotarEnlace(resultado.Motivo);
		if (!resultado.Exitoso)
		{
			// El permiso puede retirarse entre un paso y el otro.
			await AplicarRevocacionAsync(resultado.Motivo, cancelacion);
			await _bitacora.RegistrarAsync(
				OperacionAuditada.CreacionSesion, ResultadoAuditoria.Rechazo,
				$"Jacob CCO no abrió la sesión con la unidad {unidad.Clave}: {resultado.Motivo}.",
				motivoCodigo: resultado.CodigoError, operador: _emailEnCurso, cancelacion: cancelacion);
			return resultado;
		}

		await RegistrarAsync(resultado.Sesion!, cancelacion);

		// Pasa las líneas del correo al operador que confirmó Jacob.
		if (_emailEnCurso is not null)
		{
			await _bitacora.AtribuirAsync(_emailEnCurso, resultado.Sesion!.Operador, cancelacion);
		}

		await _bitacora.RegistrarAsync(
			OperacionAuditada.SeleccionUnidad, ResultadoAuditoria.Exito,
			$"Unidad {unidad.Clave} seleccionada.", cancelacion: cancelacion);
		await _bitacora.RegistrarAsync(
			OperacionAuditada.CreacionSesion, ResultadoAuditoria.Exito,
			"Sesión abierta con Jacob CCO.", cancelacion: cancelacion);

		return resultado;
	}

	public void Descartar() => _desafio = null;

	// Solo la falta de comunicación niega el enlace: un rechazo también es respuesta de Jacob.
	private void AnotarEnlace(MotivoRechazoAcceso? motivo) =>
		_conectividad?.AnotarIntercambio(motivo != MotivoRechazoAcceso.SinComunicacion);

	// Solo ante SinPermiso: una contraseña mal escrita no dice nada del permiso. No toca los pendientes.
	private async Task AplicarRevocacionAsync(
		MotivoRechazoAcceso? motivo,
		CancellationToken cancelacion)
	{
		if (motivo != MotivoRechazoAcceso.SinPermiso)
		{
			return;
		}

		await _custodia.RevocarAsync(cancelacion);
	}

	private async Task RegistrarAsync(SesionValidada sesion, CancellationToken cancelacion)
	{
		// El monotónico se lee lo más cerca posible de la validación: es la referencia de la ventana offline.
		var persistida = new SesionOfflinePersistida(
			SessionId: sesion.SessionId,
			Operador: sesion.Operador,
			Rol: sesion.Rol,
			Unidad: sesion.Unidad,
			Permisos: sesion.Permisos,
			Vigencia: sesion.Vigencia,
			MonotonicoAlValidar: _monotonico.Transcurrido,
			Instalacion: _instalacion);

		await _custodia.AbrirAsync(persistida, sesion.AccessToken, cancelacion);

		// Después de abrir la sesión y sin propagar fallos: sin red, se entra con la copia local.
		if (_catalogos is not null)
		{
			await _catalogos.EjecutarAsync(sesion.AccessToken, cancelacion);
		}
	}
}
