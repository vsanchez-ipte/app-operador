using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

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
	private readonly ITokenProvider _tokens;
	private readonly ISessionStore _sesiones;
	private readonly DatosDeInstalacion _instalacion;

	private string? _desafio;

	public AbrirSesionMovil(
		IAccesoJacobClient jacob,
		ITokenProvider tokens,
		ISessionStore sesiones,
		DatosDeInstalacion instalacion)
	{
		_jacob = jacob;
		_tokens = tokens;
		_sesiones = sesiones;
		_instalacion = instalacion;
	}

	/// <summary>
	/// Paso 1: valida credenciales y retiene el desafío para el paso 2.
	/// </summary>
	/// <remarks>
	/// Un rechazo descarta cualquier desafío anterior: si el operador se equivocó de cuenta,
	/// el desafío de la anterior no puede seguir sirviendo.
	/// </remarks>
	public async Task<ResultadoPreauth> IdentificarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default)
	{
		var resultado = await _jacob.PreautenticarAsync(email, contrasena, cancelacion);
		_desafio = resultado.Exitoso ? resultado.ChallengeId : null;

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
			return resultado;
		}

		await RegistrarAsync(resultado.Sesion!, cancelacion);
		return resultado;
	}

	/// <summary>Olvida el desafío retenido, al abandonar el acceso.</summary>
	public void Descartar() => _desafio = null;

	/// <summary>
	/// Custodia el token y publica la sesión para el resto de las pantallas.
	/// </summary>
	/// <remarks>
	/// El token va primero: si fallara al guardarse, es preferible no haber anunciado una
	/// sesión que después no podría autenticar ninguna petición.
	/// </remarks>
	private async Task RegistrarAsync(SesionValidada sesion, CancellationToken cancelacion)
	{
		await _tokens.GuardarAsync(sesion.AccessToken, cancelacion);

		_sesiones.Guardar(new SesionOperador(
			operador: sesion.Operador,
			rol: sesion.Rol,
			unidadVehicular: sesion.Unidad.Clave,
			vigencia: sesion.Vigencia,
			permisos: sesion.Permisos,
			versionAplicacion: _instalacion.VersionAplicacion,
			versionCatalogos: _instalacion.VersionCatalogos));
	}
}
