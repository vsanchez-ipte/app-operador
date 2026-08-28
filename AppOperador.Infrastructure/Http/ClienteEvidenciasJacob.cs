using System.Net.Http.Headers;
using System.Text.Json;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Http.Dtos;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Sube evidencias a <c>POST /ITS/AppIncidencias/{uuid}/Evidencias</c> (JTT-1398 CA 11).
/// </summary>
/// <remarks>
/// <para>
/// <b>El archivo se transmite en flujo, no cargado en memoria.</b> Con el tope actual de 5 MB
/// daría igual, pero JTT-289 ya fijó 15 MB con video: leer el archivo entero a un arreglo de
/// bytes en un teléfono de campo con varias incidencias en cola es cómo se provoca un cierre
/// por memoria.
/// </para>
/// <para>
/// <b>La respuesta viaja en <c>Envelope</c> también en éxito.</b> El §3 del contrato afirmaba lo
/// contrario y era falso; ya costó un defecto en JTT-1401, donde la incidencia llegaba al CCO y
/// la app la marcaba fallida. Aquí se lee del sobre desde el principio.
/// </para>
/// </remarks>
public sealed class ClienteEvidenciasJacob : IEvidenciasJacobClient
{
	private static readonly JsonSerializerOptions OpcionesJson = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	/// <summary>Nombre del campo del formulario. Lo fija el contrato.</summary>
	private const string CampoArchivo = "archivo";

	private readonly HttpClient _http;
	private readonly ConfiguracionApi _configuracion;

	public ClienteEvidenciasJacob(HttpClient http, ConfiguracionApi configuracion)
	{
		_http = http;
		_configuracion = configuracion;
	}

	/// <inheritdoc />
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
			// La fila apunta a un archivo que ya no está. Es funcional: reintentar no lo va a
			// devolver, y quien tiene que actuar es el operador volviendo a adjuntarlo.
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

			return Interpretar(cuerpo);
		}
		catch (Exception excepcion) when (
			excepcion is HttpRequestException or JsonException or IOException or UriFormatException)
		{
			return FalloTecnico("No se pudo subir la evidencia. Se reintentará solo.");
		}
		catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
		{
			return FalloTecnico("El envío de la evidencia tardó demasiado. Se reintentará solo.");
		}
	}

	/// <summary>Ruta del endpoint para esa incidencia.</summary>
	private static string RutaDe(string incidenciaUuid) =>
		$"{ConfiguracionApi.RutaIncidencias}/{Uri.EscapeDataString(incidenciaUuid)}/Evidencias";

	/// <summary>
	/// Traduce la respuesta del servidor.
	/// </summary>
	/// <remarks>
	/// <b><c>yaExistia</c> es éxito.</b> Significa que el servidor ya tenía ese contenido para
	/// esa incidencia: la subida es idempotente por contenido, no por nombre, así que un
	/// reintento tras una respuesta perdida devuelve el mismo identificador sin duplicar ni
	/// gastar cupo. Tratarlo como error dejaría la evidencia reintentándose para siempre contra
	/// un servidor que ya la tiene.
	/// </remarks>
	private static ResultadoEnvioEvidencia Interpretar(string cuerpo)
	{
		var sobre = JsonSerializer.Deserialize<Envelope<RespuestaEvidencia>>(cuerpo, OpcionesJson);

		if (sobre is null)
		{
			return FalloTecnico("El servidor respondió algo que no se pudo leer.");
		}

		if (sobre.HayError)
		{
			var codigo = sobre.CodigoError ?? CodigosErrorJacob.ErrorTecnico;

			return ResultadoEnvioEvidencia.Rechazada(
				CodigosErrorJacob.FamiliaDe(codigo),
				codigo,
				sobre.MensajeError ?? "El CCO rechazó la evidencia.");
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

	private static ResultadoEnvioEvidencia FalloTecnico(string mensaje) =>
		ResultadoEnvioEvidencia.Rechazada(
			FamiliaErrorSincronizacion.Tecnico, CodigosErrorJacob.ErrorTecnico, mensaje);
}
