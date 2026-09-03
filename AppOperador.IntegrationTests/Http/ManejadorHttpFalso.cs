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
	private readonly bool _lento;

	private ManejadorHttpFalso(Func<HttpRequestMessage, HttpResponseMessage> responder, bool lento = false)
	{
		_responder = responder;
		_lento = lento;
	}

	/// <summary>Peticiones recibidas, en orden, para poder inspeccionar lo que se envió.</summary>
	/// <remarks>
	/// <c>Autorizacion</c> guarda la cabecera <c>Authorization</c> tal cual, para comprobar
	/// que el cierre de sesión viaja con el token (JTT-1390).
	/// </remarks>
	public List<(string Url, string? Cuerpo, string? Autorizacion)> Peticiones { get; } = [];

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

	/// <summary>
	/// Responde tras ceder el hilo, y deja constancia de si la petición se liberó mientras
	/// el envío seguía en vuelo.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Reproduce el camino lento —socket nuevo, DNS— que aparece al recuperar la red. Quien
	/// envía la petición debe mantenerla viva hasta que el envío termine; si la desecha
	/// antes, la capa HTTP falla con «Cannot access a disposed object».
	/// </para>
	/// <para>
	/// El testigo es un contenido propio que se adjunta a la petición: al desecharse esta,
	/// se desecha él, y así se sabe si ocurrió antes de tiempo. Es la única forma de
	/// observarlo, porque <see cref="HttpRequestMessage"/> no publica su estado.
	/// </para>
	/// </remarks>
	public static ManejadorHttpFalso Lento(HttpResponseMessage respuesta) =>
		new(_ => respuesta, lento: true);

	/// <summary>
	/// Indica si la petición ya estaba liberada cuando el envío seguía en curso.
	/// </summary>
	/// <remarks>Solo lo llena <see cref="Lento"/>.</remarks>
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

	/// <summary>
	/// Adjunta el testigo, cede el hilo y comprueba si la petición se liberó mientras tanto.
	/// </summary>
	/// <remarks>
	/// Ceder el hilo es lo que reproduce la carrera: si quien envía devolvió la tarea sin
	/// esperarla, su <c>using</c> ya desechó la petición cuando se vuelve aquí.
	/// </remarks>
	private async Task VigilarLiberacionAsync(HttpRequestMessage peticion, CancellationToken cancelacion)
	{
		var testigo = new ContenidoTestigo();
		peticion.Content = testigo;

		await Task.Delay(20, cancelacion);

		PeticionLiberadaEnVuelo = testigo.Liberado;
	}

	/// <summary>Contenido que solo sirve para saber cuándo lo desechan.</summary>
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
