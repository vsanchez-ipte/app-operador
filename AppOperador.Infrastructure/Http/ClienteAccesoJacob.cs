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

// No registra nada: por aquí pasan contraseña, desafío y token. Nunca deja escapar una excepción de red o de formato.
public sealed class ClienteAccesoJacob : IAccesoJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;
	private readonly ITokenClaims _claims;

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

			// Cada valor en su propio bloque RSA.
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
			// La llave no sirve: no es culpa de la credencial.
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "llave.invalida");
		}
		catch (Exception excepcion) when (FalloDeComunicacion.Es(excepcion))
		{
			return ResultadoPreauth.Rechazado(MotivoRechazoAcceso.SinComunicacion, "conexion.fallida");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			// Timeout de HttpClient: solo si quien llamó no canceló.
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
			// Anónimo: el desafío es la credencial y no viaja ningún dato del operador.
			var solicitud = new SolicitudLogin { ChallengeId = challengeId, UnidadId = unidadId };

			using var respuesta = await _http.PostAsJsonAsync(
				Url(ConfiguracionApi.RutaLogin), solicitud, OpcionesJson, cancelacion);

			return await InterpretarLoginAsync(respuesta, cancelacion);
		}
		catch (Exception excepcion) when (FalloDeComunicacion.Es(excepcion))
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.SinComunicacion, "conexion.fallida");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return ResultadoLogin.Rechazado(MotivoRechazoAcceso.SinComunicacion, "tiempo.agotado");
		}
	}

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

			// Idempotente: un 200 basta como confirmación.
			return respuesta.IsSuccessStatusCode;
		}
		catch (Exception excepcion) when (FalloDeComunicacion.Es(excepcion))
		{
			return false;
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return false;
		}
	}

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
		catch (Exception excepcion) when (FalloDeComunicacion.Es(excepcion))
		{
			// Sin red no se sabe nada: sigue valiendo la ventana offline.
			return ResultadoRevalidacion.SinRespuesta("conexion.fallida");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return ResultadoRevalidacion.SinRespuesta("tiempo.agotado");
		}
	}

	// Un código funcional es negativa firme; un cuerpo ilegible o error del servidor solo es «no se pudo preguntar».
	private static async Task<ResultadoRevalidacion> InterpretarRevalidacionAsync(
		HttpResponseMessage respuesta,
		CancellationToken cancelacion)
	{
		if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
		{
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

	// Sin fechas o sin unidad no sirve, pero tampoco es negativa.
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

	// El API responde 400 a casi todo: el código HTTP no alcanza.
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

		// Sin token o sin fechas es un fallo de integración, no un rechazo del operador.
		return sesion is null
			? ResultadoLogin.Rechazado(MotivoRechazoAcceso.ErrorDelServicio, "respuesta.incompleta")
			: ResultadoLogin.Creada(sesion);
	}

	// Adopta la ventana del servidor sin recalcularla; si los permisos no cuadran con el token, no hay sesión.
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

	// Se espera aquí dentro: si no, el using desecha la petición en vuelo (falla al reconectar).
	private async Task<HttpResponseMessage> EnviarConTokenAsync(
		string ruta,
		string accessToken,
		CancellationToken cancelacion,
		HttpMethod? metodo = null)
	{
		using var peticion = new HttpRequestMessage(metodo ?? HttpMethod.Post, Url(ruta));
		peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

		return await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
	}

	public async Task<ResultadoSondeo> ComprobarEnlaceAsync(
		string accessToken,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return ResultadoSondeo.SinSesion();
		}

		try
		{
			using var respuesta = await EnviarConTokenAsync(
				ConfiguracionApi.RutaEstado, accessToken, cancelacion, HttpMethod.Get);

			// Basta el código: haber contestado ya separa servidor de comunicación.
			return ResultadoSondeo.Desde((int)respuesta.StatusCode);
		}
		catch (Exception excepcion) when (FalloDeComunicacion.Es(excepcion))
		{
			return ResultadoSondeo.SinTransporte($"No se alcanzó a Jacob CCO: {excepcion.Message}");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return ResultadoSondeo.SinTransporte("Jacob CCO no contestó dentro del tiempo de espera.");
		}
	}

	// System.Text.Json deja Local o Unspecified; el dominio exige Utc. Sin zona se toma como UTC.
	private static DateTime ComoUtc(DateTime instante) => instante.Kind switch
	{
		DateTimeKind.Utc => instante,
		DateTimeKind.Local => instante.ToUniversalTime(),
		_ => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
	};

	// En cada intento y sin caché: así no hay que invalidarla cuando el servidor la rote.
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

		// La llave viene dentro de 'resultado'.
		return CifradorRsa.DesdeBase64(sobre?.Resultado);
	}

	// 400 sirve para rechazo, cuerpo mal formado y excepciones no controladas: se lee el sobre.
	private static async Task<ResultadoPreauth> InterpretarAsync(
		HttpResponseMessage respuesta,
		CancellationToken cancelacion)
	{
		if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
		{
			// 401 llega sin cuerpo: no hay sobre que leer.
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
			// No es un sobre: validación de modelo, HTML de un proxy, etc.
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

		// Sin error y sin resultado útil: se trata como fallo del servicio, no como credencial inválida.
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

	// Un código desconocido no se asume como credencial inválida.
	private static MotivoRechazoAcceso MotivoDe(string codigoError) => codigoError switch
	{
		"appoperador.credenciales.invalidas" => MotivoRechazoAcceso.CredencialInvalida,
		"appoperador.permiso.requerido" => MotivoRechazoAcceso.SinPermiso,
		"appoperador.cuenta.inactiva" => MotivoRechazoAcceso.CuentaInactiva,
		"appoperador.cuenta.bloqueada" => MotivoRechazoAcceso.CuentaBloqueada,
		"appoperador.sin.vehiculos" => MotivoRechazoAcceso.SinUnidades,

		// Los tres del desafío se unifican: distinguirlos solo le serviría a un atacante.
		"appoperador.desafio.noexiste" => MotivoRechazoAcceso.DesafioNoValido,
		"appoperador.desafio.expirado" => MotivoRechazoAcceso.DesafioNoValido,
		"appoperador.desafio.consumido" => MotivoRechazoAcceso.DesafioNoValido,
		"appoperador.vehiculo.noautorizado" => MotivoRechazoAcceso.UnidadNoAutorizada,

		// De la revalidación: la sesión ya no existe para Jacob.
		"appoperador.sesion.revocada" => MotivoRechazoAcceso.SesionRevocada,
		"appoperador.sesion.invalida" => MotivoRechazoAcceso.SesionRevocada,

		_ => MotivoRechazoAcceso.ErrorDelServicio,
	};

	// Se conserva el id, que es contra lo que Jacob revalida. Sin id o sin clave, la unidad se descarta.
	private static IReadOnlyList<UnidadVehicular> ConvertirUnidades(IReadOnlyList<UnidadPreauth> unidades) =>
		[.. unidades
			.Where(unidad => !string.IsNullOrWhiteSpace(unidad.Id) && !string.IsNullOrWhiteSpace(unidad.Clave))
			.Select(unidad => new UnidadVehicular(unidad.Id!, unidad.Clave!, unidad.Descripcion ?? string.Empty))];

	private string Url(string ruta) => $"{_configuracion.UrlBase.TrimEnd('/')}{ruta}";
}
