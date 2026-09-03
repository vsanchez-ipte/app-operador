using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Los dos pasos del acceso a Jacob CCO, hasta dejar la sesión abierta (JTT-1382).
/// </summary>
/// <remarks>
/// <para>
/// Orquesta: identificar al operador, guardar el desafío, consumirlo con la unidad elegida,
/// custodiar el token y registrar la sesión. La pantalla solo pide y muestra.
/// </para>
/// <para>
/// <b>El desafío vive aquí y solo en memoria.</b> Es la credencial del segundo paso: no se
/// registra, no se persiste y no sale en ninguna propiedad pública. Que no lo sostenga el
/// ViewModel evita que acabe en un binding por descuido.
/// </para>
/// <para>
/// <b>Es transitorio, no compartido.</b> Cada pantalla de acceso trabaja con su propia
/// instancia, así que un desafío no puede filtrarse de un intento a otro.
/// </para>
/// </remarks>
public sealed class AbrirSesionMovil
{
	private readonly IAccesoJacobClient _jacob;
	private readonly CustodiaSesionLocal _custodia;
	private readonly ActualizarCatalogoLocal? _catalogos;
	private readonly DatosDeInstalacion _instalacion;
	private readonly IMonotonicClock _monotonico;

	private string? _desafio;

	/// <param name="custodia">
	/// Dónde queda el rastro local de la sesión: token, sesión viva y sesión persistida.
	/// </param>
	/// <param name="monotonico">
	/// Contador que se guarda junto con la sesión para poder medir después cuánto tiempo
	/// pasó de verdad, aunque muevan el reloj (JTT-1383).
	/// </param>
	/// <param name="catalogos">
	/// Refresco del catálogo local (JTT-1394 CA 4). Opcional para no obligar a las pruebas de
	/// acceso, que no lo ejercitan, a proporcionarlo.
	/// </param>
	public AbrirSesionMovil(
		IAccesoJacobClient jacob,
		CustodiaSesionLocal custodia,
		DatosDeInstalacion instalacion,
		IMonotonicClock monotonico,
		ActualizarCatalogoLocal? catalogos = null)
	{
		_jacob = jacob;
		_custodia = custodia;
		_instalacion = instalacion;
		_monotonico = monotonico;
		_catalogos = catalogos;
	}

	/// <summary>
	/// Paso 1: valida credenciales y retiene el desafío para el paso 2.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Un rechazo descarta cualquier desafío anterior: si el operador se equivocó de cuenta,
	/// el desafío de la anterior no puede seguir sirviendo.
	/// </para>
	/// <para>
	/// Si Jacob responde que la cuenta ya no tiene el permiso funcional, se revoca lo que
	/// hubiera quedado guardado del acceso anterior (JTT-1379 CA 6).
	/// </para>
	/// </remarks>
	public async Task<ResultadoPreauth> IdentificarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default)
	{
		var resultado = await _jacob.PreautenticarAsync(email, contrasena, cancelacion);
		_desafio = resultado.Exitoso ? resultado.ChallengeId : null;

		await AplicarRevocacionAsync(resultado.Motivo, cancelacion);

		return resultado;
	}

	/// <summary>
	/// Paso 2: consume el desafío con la unidad elegida, guarda el token y abre la sesión.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Devuelve <see cref="MotivoRechazoAcceso.DesafioNoValido"/> sin llamar al API si no hay
	/// desafío retenido. Es la misma salida que daría el servidor y ahorra una petición que
	/// se sabe perdida.
	/// </para>
	/// <para>
	/// El desafío se descarta pase lo que pase: es de un solo uso, así que reintentar con el
	/// mismo solo produciría <c>appoperador.desafio.consumido</c>.
	/// </para>
	/// </remarks>
	public async Task<ResultadoLogin> AbrirAsync(
		UnidadVehicular unidad,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(_desafio))
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.DesafioNoValido, "desafio.ausente");
		}

		var desafio = _desafio;
		_desafio = null;

		var resultado = await _jacob.CompletarAccesoAsync(desafio, unidad.Id, cancelacion);
		if (!resultado.Exitoso)
		{
			// El permiso se revalida al crear la sesión, no solo al preautenticar: puede
			// retirarse entre un paso y el otro.
			await AplicarRevocacionAsync(resultado.Motivo, cancelacion);
			return resultado;
		}

		await RegistrarAsync(resultado.Sesion!, cancelacion);
		return resultado;
	}

	/// <summary>Olvida el desafío retenido, al abandonar el acceso.</summary>
	public void Descartar() => _desafio = null;

	/// <summary>
	/// Borra sesión y token locales cuando Jacob niega el permiso funcional (JTT-1379 CA 6).
	/// </summary>
	/// <remarks>
	/// <para>
	/// La revocación se decide en Jacob y llega a la app en la siguiente validación en línea.
	/// Sin este paso, un operador al que le retiraron el permiso conservaría la sesión de su
	/// último acceso correcto y podría seguir trabajando sin conexión hasta que venciera la
	/// ventana offline. El criterio pide justamente lo contrario.
	/// </para>
	/// <para>
	/// <b>Solo se revoca ante <see cref="MotivoRechazoAcceso.SinPermiso"/>.</b> Una
	/// contraseña mal escrita o una caída de red no dicen nada sobre la autorización del
	/// operador, y cerrarle la sesión por eso convertiría cualquier tropiezo en una salida
	/// forzada.
	/// </para>
	/// <para>
	/// <b>No toca los registros pendientes.</b> Lo capturado es del operador y de la unidad,
	/// no del permiso; borrarlo aquí perdería trabajo de campo ya hecho (JTT-1390).
	/// </para>
	/// </remarks>
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

	/// <summary>
	/// Custodia el token y publica la sesión para el resto de las pantallas.
	/// </summary>
	/// <remarks>
	/// El token va primero: si fallara al guardarse, es preferible no haber anunciado una
	/// sesión que después no podría autenticar ninguna petición.
	/// </remarks>
	private async Task RegistrarAsync(SesionValidada sesion, CancellationToken cancelacion)
	{
		// El contador monotónico se lee aquí, lo más cerca posible de la validación: es la
		// referencia contra la que se medirá la ventana offline (JTT-1383).
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

		// El catálogo se refresca aquí, que es la «validación en línea» del CA 4: en el momento
		// que ya existe, sin temporizador propio. Va DESPUÉS de abrir la sesión y su fallo no se
		// propaga: si no hay red para el catálogo, el operador entra igual y captura con la
		// copia que ya tenía, que es justo lo que el CA 2 promete.
		if (_catalogos is not null)
		{
			await _catalogos.EjecutarAsync(sesion.AccessToken, cancelacion);
		}
	}
}
