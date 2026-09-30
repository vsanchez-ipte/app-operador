using System.Net;
using System.Text;

namespace AppOperador.IntegrationTests.Http;

internal sealed class ManejadorHttpFalso : HttpMessageHandler
{
	private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
	private readonly bool _lento;

	private ManejadorHttpFalso(Func<HttpRequestMessage, HttpResponseMessage> responder, bool lento = false)
	{
		_responder = responder;
		_lento = lento;
	}

	public List<(string Url, string? Cuerpo, string? Autorizacion)> Peticiones { get; } = [];

	public static ManejadorHttpFalso ConLlaveY(string llavePublicaBase64, HttpResponseMessage respuestaPreauth) =>
		new(peticion => peticion.RequestUri!.AbsolutePath.Contains("GetPublicKey", StringComparison.OrdinalIgnoreCase)
			? Json(HttpStatusCode.OK, $$"""{"resultado":"{{llavePublicaBase64}}","codigoError":"","mensajeError":""}""")
			: respuestaPreauth);

	public static ManejadorHttpFalso Siempre(HttpResponseMessage respuesta) => new(_ => respuesta);

	public static ManejadorHttpFalso ConexionRechazada() =>
		new(_ => throw new HttpRequestException("Connection refused"));

	public static ManejadorHttpFalso SocketCerradoEnAndroid() =>
		new(_ => throw new WebException("Socket closed", WebExceptionStatus.ConnectFailure));

	public static ManejadorHttpFalso TiempoAgotado() =>
		new(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

	public static ManejadorHttpFalso Lento(HttpResponseMessage respuesta) =>
		new(_ => respuesta, lento: true);

	public bool PeticionLiberadaEnVuelo { get; private set; }

	public static HttpResponseMessage Json(HttpStatusCode codigo, string cuerpo) =>
		new(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

	public static HttpResponseMessage SinCuerpo(HttpStatusCode codigo) => new(codigo);

	protected override async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage peticion,
		CancellationToken cancelacion)
	{
		var cuerpo = peticion.Content is null ? null : await peticion.Content.ReadAsStringAsync(cancelacion);
		Peticiones.Add((peticion.RequestUri!.ToString(), cuerpo, peticion.Headers.Authorization?.ToString()));

		if (_lento)
		{
			await VigilarLiberacionAsync(peticion, cancelacion);
		}

		return _responder(peticion);
	}

	private async Task VigilarLiberacionAsync(HttpRequestMessage peticion, CancellationToken cancelacion)
	{
		var testigo = new ContenidoTestigo();
		peticion.Content = testigo;

		await Task.Delay(20, cancelacion);

		PeticionLiberadaEnVuelo = testigo.Liberado;
	}

	private sealed class ContenidoTestigo : HttpContent
	{
		public bool Liberado { get; private set; }

		protected override Task SerializeToStreamAsync(Stream destino, TransportContext? contexto) =>
			Task.CompletedTask;

		protected override bool TryComputeLength(out long longitud)
		{
			longitud = 0;
			return true;
		}

		protected override void Dispose(bool liberando)
		{
			Liberado = true;
			base.Dispose(liberando);
		}
	}
}
