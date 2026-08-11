using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Http.Dtos;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Acceso real contra el canal móvil de Jacob CCO, en sus dos pasos.
/// </summary>
/// <remarks>
/// <para>
/// Paso 1: <c>GetPublicKey → cifrado RSA → POST Preauth</c>, que devuelve un desafío y las
/// unidades del operador. Paso 2: <c>POST AppLogin</c>, que consume el desafío junto con la
/// unidad elegida y abre la sesión. Cualquier desenlace se traduce a un resultado; nunca se
/// deja escapar una excepción de red o de formato: para la pantalla de acceso, "no hubo red"
/// y "la credencial es incorrecta" son dos resultados normales, no fallos del programa.
/// </para>
/// <para>
/// <b>Seguridad.</b> Esta clase no registra nada. No hay bitácora ni <c>ILogger</c> a
/// propósito: por aquí pasan la contraseña en claro, su versión cifrada, el desafío y el
/// token de sesión, y ninguno puede acabar en un log (JTT-1378 §7).
/// </para>
/// </remarks>
public sealed class ClienteAccesoJacob : IAccesoJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;
	private readonly ITokenClaims _claims;

	/// <param name="claims">
	/// Lectura de lo que el token trae firmado. Se recibe por el contrato y no se llama al
	/// lector concreto: es el mismo cotejo que hacen la reanudación y la revalidación, y
	/// tenerlo por dos caminos distintos permitiría que divergieran.
	/// </param>
	public ClienteAccesoJacob(HttpClient http, ConfiguracionApi configuracion, ITokenClaims claims)
	{
		_http = http;
		_configuracion = configuracion;
		_claims = claims;
	}

	public async Task<ResultadoPreauth> PreautenticarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default)
	{
		try
		{
			using var cifrador = await ObtenerCifradorAsync(cancelacion);

			// Email y contraseña se cifran por separado, cada uno en su propio bloque.
			var solicitud = new SolicitudPreauth
			{
				Email = cifrador.Cifrar(email),
				Password = cifrador.Cifrar(contrasena),
				Plataforma = _configuracion.Plataforma,
			};

			using var respuesta = await _http.PostAsJsonAsync(
				Url(ConfiguracionApi.RutaPreauth), solicitud, OpcionesJson, cancelacion);

			return await InterpretarAsync(respuesta, cancelacion);
		}
		catch (ErrorDeCifradoException)
		{
			// La llave pública no sirve. No es culpa de la credencial del operador.
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "llave.invalida");
		}
		catch (HttpRequestException)
		{
			// Servidor inalcanzable, DNS, certificado, conexión rechazada.
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.SinComunicacion, "conexion.fallida");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			// HttpClient señala el vencimiento del tiempo de espera con esta excepción; solo
			// es un timeout si quien llamó no fue el que canceló.
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.SinComunicacion, "tiempo.agotado");
		}
	}

	public async Task<ResultadoLogin> CompletarAccesoAsync(
		string challengeId,
		string unidadId,
		CancellationToken cancelacion = default)
	{
		try
		{
			// Endpoint anónimo: el desafío es la credencial. No se vuelve a cifrar nada,
			// porque no viaja ningún dato del operador.
			var solicitud = new SolicitudLogin { ChallengeId = challengeId, UnidadId = unidadId };

			using var respuesta = await _http.PostAsJsonAsync(
				Url(ConfiguracionApi.RutaLogin), solicitud, OpcionesJson, cancelacion);

			return await InterpretarLoginAsync(respuesta, cancelacion);
		}
		catch (HttpRequestException)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.SinComunicacion, "conexion.fallida");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.SinComunicacion, "tiempo.agotado");
		}
	}

	/// <inheritdoc />
	public async Task<bool> CerrarSesionAsync(string accessToken, CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return false;
		}

		try
		{
			using var respuesta = await EnviarConTokenAsync(
				ConfiguracionApi.RutaLogout, accessToken, cancelacion);

			// El endpoint es idempotente: un 200 basta como confirmación y no hay cuerpo que
			// interpretar. Cualquier otro código significa que la revocación no consta.
			return respuesta.IsSuccessStatusCode;
		}
		catch (HttpRequestException)
		{
			return false;
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<ResultadoRevalidacion> RevalidarAsync(
		string accessToken,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return ResultadoRevalidacion.Negada(MotivoRechazoAcceso.SesionRevocada, "token.ausente");
		}

		try
		{
			using var respuesta = await EnviarConTokenAsync(
				ConfiguracionApi.RutaRevalidar, accessToken, cancelacion);

			return await InterpretarRevalidacionAsync(respuesta, cancelacion);
		}
		catch (HttpRequestException)
		{
			// Sin red no se sabe nada de la sesión: sigue valiendo la ventana offline.
			return ResultadoRevalidacion.SinRespuesta("conexion.fallida");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return ResultadoRevalidacion.SinRespuesta("tiempo.agotado");
		}
	}

	/// <summary>
	/// Traduce la respuesta de la revalidación.
	/// </summary>
	/// <remarks>
	/// La distinción que importa: un código funcional de Jacob es una negativa firme —la
	/// sesión dejó de ser válida— mientras que un cuerpo ilegible o un error del servidor
	/// solo significan que no se pudo preguntar. Tratar lo segundo como negativa sacaría al
	/// operador de una sesión offline perfectamente vigente.
	/// </remarks>
	private static async Task<ResultadoRevalidacion> InterpretarRevalidacionAsync(
		HttpResponseMessage respuesta,
		CancellationToken cancelacion)
	{
		if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
		{
			// El token ya no autentica: la sesión se acabó para Jacob.
			return ResultadoRevalidacion.Negada(MotivoRechazoAcceso.SesionRevocada, "http.401");
		}

		var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

		Envelope<RespuestaRevalidacion>? sobre;
		try
		{
			sobre = JsonSerializer.Deserialize<Envelope<RespuestaRevalidacion>>(cuerpo, OpcionesJson);
		}
		catch (JsonException)
		{
			return ResultadoRevalidacion.SinRespuesta("respuesta.desconocida");
		}

		if (sobre is null)
		{
			return ResultadoRevalidacion.SinRespuesta("respuesta.vacia");
		}

		if (sobre.HayError)
		{
			return ResultadoRevalidacion.Negada(MotivoDe(sobre.CodigoError!), sobre.CodigoError);
		}

		var resultado = sobre.Resultado;
		if (!respuesta.IsSuccessStatusCode || resultado is null)
		{
			return ResultadoRevalidacion.SinRespuesta("respuesta.incompleta");
		}

		return ConvertirRevalidacion(resultado);
	}

	/// <summary>
	/// Arma el resultado de una revalidación confirmada.
	/// </summary>
	/// <remarks>
	/// Sin las fechas o sin unidad la respuesta no sirve, pero tampoco es una negativa: se
	/// informa como «no se pudo preguntar» y la sesión offline continúa.
	/// </remarks>
	private static ResultadoRevalidacion ConvertirRevalidacion(RespuestaRevalidacion respuesta)
	{
		if (respuesta.LastValidatedAtUtc is null
			|| respuesta.OfflineUntilUtc is null
			|| respuesta.Unidad?.Id is null
			|| string.IsNullOrWhiteSpace(respuesta.Unidad.Clave))
		{
			return ResultadoRevalidacion.SinRespuesta("respuesta.incompleta");
		}

		var validado = ComoUtc(respuesta.LastValidatedAtUtc.Value);
		var hastaOffline = ComoUtc(respuesta.OfflineUntilUtc.Value);

		if (hastaOffline < validado)
		{
			return ResultadoRevalidacion.SinRespuesta("respuesta.incompleta");
		}

		return ResultadoRevalidacion.Confirmada(
			rol: respuesta.Rol?.Nombre ?? string.Empty,
			unidad: new UnidadVehicular(
				respuesta.Unidad.Id,
				respuesta.Unidad.Clave!,
				respuesta.Unidad.Descripcion ?? string.Empty),
			permisos: PermisosOperador.DelServidor(respuesta.Permisos),
			vigencia: VigenciaOffline.DelServidor(validado, hastaOffline));
	}

	/// <summary>
	/// Traduce la respuesta del segundo paso a un resultado de dominio.
	/// </summary>
	/// <remarks>
	/// Mismo criterio que el paso 1: el código HTTP no alcanza, porque el API responde
	/// <c>400</c> tanto para un rechazo funcional como para un cuerpo mal formado o una
	/// excepción no controlada.
	/// </remarks>
	private async Task<ResultadoLogin> InterpretarLoginAsync(
		HttpResponseMessage respuesta,
		CancellationToken cancelacion)
	{
		if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "http.401");
		}

		var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

		Envelope<RespuestaLogin>? sobre;
		try
		{
			sobre = JsonSerializer.Deserialize<Envelope<RespuestaLogin>>(cuerpo, OpcionesJson);
		}
		catch (JsonException)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.desconocida");
		}

		if (sobre is null)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.vacia");
		}

		if (sobre.HayError)
		{
			return ResultadoLogin.Rechazado(MotivoDe(sobre.CodigoError!), sobre.CodigoError);
		}

		var resultado = sobre.Resultado;
		if (!respuesta.IsSuccessStatusCode || resultado is null)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.incompleta");
		}

		var sesion = ConvertirSesion(resultado);

		// Sin token no hay sesión utilizable, y sin las fechas del servidor no se puede
		// saber hasta cuándo vale sin conexión. Cualquiera de las dos ausencias es un fallo
		// de integración, no un rechazo del operador.
		return sesion is null
			? ResultadoLogin.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.incompleta")
			: ResultadoLogin.Creada(sesion);
	}

	/// <summary>
	/// Convierte la respuesta del API en la sesión de la app, o <see langword="null"/> si le
	/// falta algo imprescindible.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Las fechas se adoptan tal como llegan y se normalizan a UTC. <b>No se recalcula la
	/// ventana offline</b>: la calcula el servidor, y rehacerla contra el reloj del teléfono
	/// daría una vigencia distinta en cuanto ese reloj esté desfasado (JTT-1382 CA 3 y CA 4).
	/// </para>
	/// <para>
	/// Los permisos del cuerpo se cotejan contra el claim <c>module</c> del token antes de
	/// aceptarlos (JTT-1379 CA 8). Si el cuerpo concede algo que el token no respalda, la
	/// sesión no se construye: el acceso termina en
	/// <see cref="MotivoRechazoAcceso.ErrorDelServicio"/>, que es lo que corresponde a una
	/// respuesta incoherente, no a un rechazo del operador.
	/// </para>
	/// </remarks>
	private SesionValidada? ConvertirSesion(RespuestaLogin respuesta)
	{
		if (string.IsNullOrWhiteSpace(respuesta.AccessToken)
			|| respuesta.LastValidatedAtUtc is null
			|| respuesta.OfflineUntilUtc is null
			|| respuesta.Unidad?.Id is null
			|| string.IsNullOrWhiteSpace(respuesta.Unidad.Clave))
		{
			return null;
		}

		var validado = ComoUtc(respuesta.LastValidatedAtUtc.Value);
		var hastaOffline = ComoUtc(respuesta.OfflineUntilUtc.Value);

		if (hastaOffline < validado)
		{
			return null;
		}

		var permisos = PermisosOperador.DelServidor(respuesta.Permisos);
		if (!_claims.Respaldan(permisos, respuesta.AccessToken))
		{
			return null;
		}

		return new SesionValidada(
			sessionId: respuesta.SessionId ?? string.Empty,
			accessToken: respuesta.AccessToken,
			tokenExpiraUtc: ComoUtc(respuesta.TokenExpiresAtUtc ?? hastaOffline),
			operador: respuesta.Operador?.Nombre ?? respuesta.Operador?.Email ?? string.Empty,
			rol: respuesta.Rol?.Nombre ?? string.Empty,
			unidad: new UnidadVehicular(
				respuesta.Unidad.Id,
				respuesta.Unidad.Clave!,
				respuesta.Unidad.Descripcion ?? string.Empty),
			permisos: permisos,
			vigencia: VigenciaOffline.DelServidor(validado, hastaOffline),
			horaServidorUtc: ComoUtc(respuesta.ServerTimeUtc ?? validado));
	}

	/// <summary>
	/// Envía un POST autenticado con el token de la sesión.
	/// </summary>
	/// <remarks>
	/// Las dos operaciones que lo necesitan —cierre y revalidación— arman la petición igual:
	/// sin cuerpo y con el token en la cabecera <c>Authorization</c>. <b>El token no viaja en
	/// el cuerpo</b>, para que no acabe en trazas intermedias.
	/// </remarks>
	private Task<HttpResponseMessage> EnviarConTokenAsync(
		string ruta,
		string accessToken,
		CancellationToken cancelacion)
	{
		using var peticion = new HttpRequestMessage(HttpMethod.Post, Url(ruta));
		peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

		return _http.SendAsync(peticion, cancelacion);
	}

	/// <summary>
	/// Normaliza a UTC lo que devuelve el deserializador.
	/// </summary>
	/// <remarks>
	/// <c>System.Text.Json</c> convierte a hora local un instante con zona, y deja
	/// <c>Unspecified</c> uno sin ella. La regla de vigencia exige <c>Kind.Utc</c> explícito
	/// y lanza si no lo recibe, así que aquí se fija: lo que trae zona se convierte, lo que
	/// no la trae se toma como UTC, que es lo que declara el contrato.
	/// </remarks>
	private static DateTime ComoUtc(DateTime instante) => instante.Kind switch
	{
		DateTimeKind.Utc => instante,
		DateTimeKind.Local => instante.ToUniversalTime(),
		_ => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
	};

	/// <summary>
	/// Trae la llave pública y arma el cifrador.
	/// </summary>
	/// <remarks>
	/// La llave se pide en cada preautenticación y vive solo durante la operación. No se
	/// guarda en disco ni se cachea entre intentos: es pública, pero cachearla obligaría a
	/// invalidarla cuando el servidor la rote, y el ahorro no compensa ese riesgo.
	/// </remarks>
	private async Task<CifradorRsa> ObtenerCifradorAsync(CancellationToken cancelacion)
	{
		using var respuesta = await _http.GetAsync(Url(ConfiguracionApi.RutaLlavePublica), cancelacion);
		respuesta.EnsureSuccessStatusCode();

		var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

		Envelope<string>? sobre;
		try
		{
			sobre = JsonSerializer.Deserialize<Envelope<string>>(cuerpo, OpcionesJson);
		}
		catch (JsonException excepcion)
		{
			throw new ErrorDeCifradoException("La respuesta de la llave pública no es JSON válido.", excepcion);
		}

		// La llave viene DENTRO de 'resultado'. Decodificar el cuerpo entero falla.
		return CifradorRsa.DesdeBase64(sobre?.Resultado);
	}

	/// <summary>
	/// Traduce la respuesta del API a un resultado de dominio.
	/// </summary>
	/// <remarks>
	/// No basta con mirar el código HTTP. El API responde <c>400</c> tanto para un rechazo
	/// funcional con <c>Envelope</c> como para un cuerpo mal formado, que llega con otra
	/// forma distinta (<c>{ "codigo": "422", "mensaje": … }</c>) y ni siquiera es un sobre.
	/// Y las excepciones no controladas también llegan como <c>400</c>, nunca como 500.
	/// </remarks>
	private static async Task<ResultadoPreauth> InterpretarAsync(
		HttpResponseMessage respuesta,
		CancellationToken cancelacion)
	{
		// 401 llega sin cuerpo útil: no hay Envelope que leer.
		if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
		{
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "http.401");
		}

		var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion);

		Envelope<RespuestaPreauth>? sobre;
		try
		{
			sobre = JsonSerializer.Deserialize<Envelope<RespuestaPreauth>>(cuerpo, OpcionesJson);
		}
		catch (JsonException)
		{
			// Cuerpo que no es un Envelope: validación de modelo, HTML de un proxy, etc.
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.desconocida");
		}

		if (sobre is null)
		{
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.vacia");
		}

		if (sobre.HayError)
		{
			return ResultadoPreauth.Rechazado(MotivoDe(sobre.CodigoError!), sobre.CodigoError);
		}

		// Sin código de error pero tampoco resultado utilizable: el contrato no contempla
		// este caso, así que se trata como fallo del servicio y no como credencial inválida.
		var resultado = sobre.Resultado;
		if (!respuesta.IsSuccessStatusCode || resultado is null || string.IsNullOrWhiteSpace(resultado.ChallengeId))
		{
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.incompleta");
		}

		return ResultadoPreauth.Emitido(
			resultado.ChallengeId,
			resultado.ExpiresAtUtc ?? DateTime.UtcNow,
			ConvertirUnidades(resultado.Unidades));
	}

	/// <summary>Mapea el catálogo <c>appoperador.*</c> a la causa que entiende la app.</summary>
	/// <remarks>
	/// Un código desconocido no se asume como credencial inválida: si el API incorpora un
	/// caso nuevo, es preferible un fallo genérico honesto a un mensaje equivocado.
	/// </remarks>
	private static MotivoRechazoAcceso MotivoDe(string codigoError) => codigoError switch
	{
		"appoperador.credenciales.invalidas" => MotivoRechazoAcceso.CredencialInvalida,
		"appoperador.permiso.requerido" => MotivoRechazoAcceso.SinPermiso,
		"appoperador.cuenta.inactiva" => MotivoRechazoAcceso.CuentaInactiva,
		"appoperador.cuenta.bloqueada" => MotivoRechazoAcceso.CuentaBloqueada,
		"appoperador.sin.vehiculos" => MotivoRechazoAcceso.SinUnidades,

		// Del segundo paso. Los tres del desafío se unifican: la salida del operador es la
		// misma —repetir el acceso— y distinguirlos solo le diría a un atacante en qué falló.
		"appoperador.desafio.noexiste" => MotivoRechazoAcceso.DesafioNoValido,
		"appoperador.desafio.expirado" => MotivoRechazoAcceso.DesafioNoValido,
		"appoperador.desafio.consumido" => MotivoRechazoAcceso.DesafioNoValido,
		"appoperador.vehiculo.noautorizado" => MotivoRechazoAcceso.UnidadNoAutorizada,

		// De la revalidación (JTT-1383). Las dos llevan a lo mismo: la sesión ya no existe
		// para Jacob y hay que autenticarse de nuevo.
		"appoperador.sesion.revocada" => MotivoRechazoAcceso.SesionRevocada,
		"appoperador.sesion.invalida" => MotivoRechazoAcceso.SesionRevocada,

		_ => MotivoRechazoAcceso.ErrorDelServicio,
	};

	/// <summary>
	/// Convierte las unidades del contrato al modelo de la aplicación.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Se conserva el <c>id</c> además de la clave: es lo que el segundo paso del acceso le
	/// envía a Jacob, que revalida la unidad contra él y no contra el número económico
	/// (JTT-1381 CA 6).
	/// </para>
	/// <para>
	/// Una unidad sin <c>id</c> o sin <c>clave</c> se descarta en vez de completarse con un
	/// valor vacío: sin identificador no se puede seleccionar —el API la rechazaría— y sin
	/// clave el operador no sabría cuál está eligiendo. Es preferible una lista más corta
	/// que una entrada que no funciona.
	/// </para>
	/// </remarks>
	private static IReadOnlyList<UnidadVehicular> ConvertirUnidades(IReadOnlyList<UnidadPreauth> unidades) =>
		[.. unidades
			.Where(unidad => !string.IsNullOrWhiteSpace(unidad.Id) && !string.IsNullOrWhiteSpace(unidad.Clave))
			.Select(unidad => new UnidadVehicular(unidad.Id!, unidad.Clave!, unidad.Descripcion ?? string.Empty))];

	private string Url(string ruta) => $"{_configuracion.UrlBase.TrimEnd('/')}{ruta}";
}
