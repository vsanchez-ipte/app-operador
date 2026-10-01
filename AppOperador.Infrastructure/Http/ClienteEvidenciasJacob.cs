using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Http.Dtos;

namespace AppOperador.Infrastructure.Http;

// En flujo y no en memoria; el estado HTTP se mira antes del Envelope porque el proxy contesta HTML.
public sealed class ClienteEvidenciasJacob : IEvidenciasJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	// Lo fija el contrato.
	private const string CampoArchivo = "archivo";

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;

	public ClienteEvidenciasJacob(HttpClient http, ConfiguracionApi configuracion)
	{
		_http = http;
		_configuracion = configuracion;
	}

	public async Task<ResultadoEnvioEvidencia> SubirAsync(
		string incidenciaUuid,
		string rutaArchivo,
		string nombreOriginal,
		string accessToken,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return FalloTecnico("La sesión no tiene token para subir la evidencia.");
		}

		if (!File.Exists(rutaArchivo))
		{
			// El archivo ya no está: es funcional, reintentar no lo devuelve.
			return ResultadoEnvioEvidencia.Rechazada(
				FamiliaErrorSincronizacion.Funcional,
				CodigosErrorJacob.EvidenciaSinArchivo,
				"El archivo de la evidencia ya no está en el dispositivo.");
		}

		try
		{
			await using var contenido = File.OpenRead(rutaArchivo);

			using var formulario = new MultipartFormDataContent();
			using var parte = new StreamContent(contenido);
			parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			formulario.Add(parte, CampoArchivo, nombreOriginal);

			using var peticion = new HttpRequestMessage(
				HttpMethod.Post,
				new Uri(new Uri(_configuracion.UrlBase), RutaDe(incidenciaUuid)))
			{
				Content = formulario,
			};

			peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

			using var respuesta = await _http.SendAsync(peticion, cancelacion).ConfigureAwait(false);
			var cuerpo = await respuesta.Content.ReadAsStringAsync(cancelacion).ConfigureAwait(false);

			return Interpretar(respuesta.StatusCode, cuerpo);
		}
		catch (Exception excepcion) when (
			FalloDeComunicacion.Es(excepcion) || excepcion is UriFormatException)
		{
			return FalloTecnico($"No se pudo contactar al servidor ({excepcion.GetType().Name}).");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return FalloTecnico("El envío de la evidencia tardó demasiado.");
		}
	}

	private static string RutaDe(string incidenciaUuid) =>
		$"{ConfiguracionApi.RutaIncidencias}/{Uri.EscapeDataString(incidenciaUuid)}/Evidencias";

	// yaExistia es éxito: la subida es idempotente por contenido y el servidor ya la tenía.
	private static ResultadoEnvioEvidencia Interpretar(HttpStatusCode estado, string cuerpo)
	{
		// Lo que responde el proxy es HTML: se atiende antes de leer un Envelope, o un 413 se reintentaría para siempre.
		if (estado == HttpStatusCode.RequestEntityTooLarge)
		{
			return ResultadoEnvioEvidencia.Rechazada(
				FamiliaErrorSincronizacion.Tecnico,
				CodigosErrorJacob.EvidenciaRechazadaPorProxy,
				"El servidor no acepta archivos de este tamaño todavía (HTTP 413).");
		}

		// 502, 503 y 504 los produce el proxy: se reintentan, sin confundirlos con un rechazo del CCO.
		if (EsDelProxy(estado))
		{
			return FalloTecnico($"El servidor no está disponible ({Http(estado)}).");
		}

		// Un cuerpo vacío, como un 401 del middleware de JWT, tampoco se puede deserializar.
		if (string.IsNullOrWhiteSpace(cuerpo))
		{
			return FalloTecnico($"El servidor respondió {Http(estado)} sin explicación.");
		}

		Envelope<RespuestaEvidencia>? sobre;
		try
		{
			sobre = JsonSerializer.Deserialize<Envelope<RespuestaEvidencia>>(cuerpo, OpcionesJson);
		}
		catch (JsonException)
		{
			return FalloTecnico($"El servidor respondió {Http(estado)} con algo que no se pudo leer.");
		}

		if (sobre is null)
		{
			return FalloTecnico($"El servidor respondió {Http(estado)} con algo que no se pudo leer.");
		}

		if (sobre.HayError)
		{
			var codigo = sobre.CodigoError ?? CodigosErrorJacob.ErrorTecnico;

			return ResultadoEnvioEvidencia.Rechazada(
				CodigosErrorJacob.FamiliaDe(codigo),
				codigo,
				sobre.MensajeError ?? "El CCO rechazó la evidencia.");
		}

		// Un ProblemDetails se deserializa sin fallar y sin error declarado.
		if ((int)estado is < 200 or > 299)
		{
			return FalloTecnico($"El servidor respondió {Http(estado)} sin explicación.");
		}

		var resultado = sobre.Resultado;

		if (resultado is null || string.IsNullOrWhiteSpace(resultado.IdEvidencia))
		{
			return FalloTecnico("El servidor aceptó la evidencia pero no devolvió su identificador.");
		}

		return ResultadoEnvioEvidencia.Aceptada(new EvidenciaRegistrada(
			resultado.IdEvidencia!,
			resultado.TipoMime ?? string.Empty,
			resultado.HashSha256 ?? string.Empty,
			resultado.YaExistia ?? false));
	}

	private static bool EsDelProxy(HttpStatusCode estado) =>
		estado is HttpStatusCode.BadGateway
			or HttpStatusCode.ServiceUnavailable
			or HttpStatusCode.GatewayTimeout;

	private static string Http(HttpStatusCode estado) => $"HTTP {(int)estado} {estado}";

	private static ResultadoEnvioEvidencia FalloTecnico(string mensaje) =>
		ResultadoEnvioEvidencia.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, CodigosErrorJacob.ErrorTecnico, mensaje);
}
