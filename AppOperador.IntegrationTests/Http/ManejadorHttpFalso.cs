using System.Net;
using System.Text;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Servidor HTTP de mentira: responde lo que la prueba le indique, sin red.
/// </summary>
/// <remarks>
/// Permite ejercitar el cliente de preautenticación contra respuestas que un servidor real
/// no produce a voluntad —un <c>401</c> sin cuerpo, un JSON que no es un <c>Envelope</c>, una
/// conexión rechazada— y comprobar que cada una se traduce a la causa correcta.
/// </remarks>
internal sealed class ManejadorHttpFalso : HttpMessageHandler
{
	private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

	private ManejadorHttpFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
		_responder = responder;

	/// <summary>Peticiones recibidas, en orden, para poder inspeccionar lo que se envió.</summary>
	public List<(string Url, string? Cuerpo)> Peticiones { get; } = [];

	/// <summary>Responde con la llave pública y luego con lo que se indique para el Preauth.</summary>
	public static ManejadorHttpFalso ConLlaveY(string llavePublicaBase64, HttpResponseMessage respuestaPreauth) =>
		new(peticion => peticion.RequestUri!.AbsolutePath.Contains("GetPublicKey", StringComparison.OrdinalIgnoreCase)
			? Json(HttpStatusCode.OK, $$"""{"resultado":"{{llavePublicaBase64}}","codigoError":"","mensajeError":""}""")
			: respuestaPreauth);

	/// <summary>Responde siempre lo mismo, sea cual sea la ruta.</summary>
	public static ManejadorHttpFalso Siempre(HttpResponseMessage respuesta) => new(_ => respuesta);

	/// <summary>Simula que no hay servidor al otro lado.</summary>
	public static ManejadorHttpFalso ConexionRechazada() =>
		new(_ => throw new HttpRequestException("Connection refused"));

	/// <summary>Simula que el servidor nunca contesta.</summary>
	public static ManejadorHttpFalso TiempoAgotado() =>
		new(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

	public static HttpResponseMessage Json(HttpStatusCode codigo, string cuerpo) =>
		new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

	public static HttpResponseMessage SinCuerpo(HttpStatusCode codigo) => new(codigo);

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage peticion,
		CancellationToken cancelacion)
	{
		var cuerpo = peticion.Content is null ? null : await peticion.Content.ReadAsStringAsync(cancelacion);
		Peticiones.Add((peticion.RequestUri!.ToString(), cuerpo));

		return _responder(peticion);
	}
}
