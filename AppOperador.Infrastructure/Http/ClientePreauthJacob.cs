using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Http.Dtos;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Preautenticación real contra el canal móvil de Jacob CCO.
/// </summary>
/// <remarks>
/// <para>
/// Ejecuta el flujo completo del primer paso del acceso:
/// <c>GetPublicKey → cifrado RSA → POST Preauth</c>, y traduce cualquier desenlace a un
/// <see cref="ResultadoPreauth"/>. Nunca deja escapar una excepción de red o de formato:
/// para la pantalla de acceso, "no hubo red" y "la credencial es incorrecta" son dos
/// resultados normales, no fallos del programa.
/// </para>
/// <para>
/// <b>Seguridad.</b> Esta clase no registra nada. No hay bitácora ni <c>ILogger</c> a
/// propósito: por aquí pasan la contraseña en claro, su versión cifrada y el desafío, y
/// ninguno de los tres puede acabar en un log (JTT-1378 §7).
/// </para>
/// </remarks>
public sealed class ClientePreauthJacob : IPreauthClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;

	public ClientePreauthJacob(HttpClient http, ConfiguracionApi configuracion)
	{
		_http = http;
		_configuracion = configuracion;
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
		_ => MotivoRechazoAcceso.ErrorDelServicio,
	};

	/// <summary>
	/// Convierte las unidades del contrato al modelo de la aplicación.
	/// </summary>
	/// <remarks>
	/// Se deserializan porque forman parte de la respuesta, pero JTT-1378 no las muestra.
	/// El identificador que consume el segundo paso del acceso no cabe en
	/// <see cref="UnidadVehicular"/>; incorporarlo es trabajo de JTT-1380, cuando exista
	/// quien lo use.
	/// </remarks>
	private static IReadOnlyList<UnidadVehicular> ConvertirUnidades(IReadOnlyList<UnidadPreauth> unidades) =>
		[.. unidades
			.Where(unidad => !string.IsNullOrWhiteSpace(unidad.Clave))
			.Select(unidad => new UnidadVehicular(unidad.Clave!, unidad.Descripcion ?? string.Empty))];

	private string Url(string ruta) => $"{_configuracion.UrlBase.TrimEnd('/')}{ruta}";
}
