using System.Net;
using System.Text;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

// Con el HTML que nginx devuelve de verdad: su 413 se reportaba como fallo genérico y se reintentaba para siempre.
public sealed class ClienteEvidenciasJacobProxyTests : IDisposable
{
	// Lo que responde nginx 1.24.0 al pasarse del tamaño.
	private const string HtmlDeNginx =
		"<html>\r\n<head><title>413 Request Entity Too Large</title></head>\r\n" +
		"<body>\r\n<center><h1>413 Request Entity Too Large</h1></center>\r\n" +
		"<hr><center>nginx/1.24.0 (Ubuntu)</center>\r\n</body>\r\n</html>\r\n";

	private readonly string _archivo = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

	public ClienteEvidenciasJacobProxyTests() =>
		File.WriteAllText(_archivo, "contenido de prueba");

	public void Dispose()
	{
		if (File.Exists(_archivo))
		{
			File.Delete(_archivo);
		}
	}

	[Fact]
	public async Task El413DelProxy_noSeConfundeConUnFalloGenerico()
	{
		var resultado = await SubirCon(HttpStatusCode.RequestEntityTooLarge, HtmlDeNginx);

		Assert.False(resultado.Exito);
		Assert.Equal(CodigosErrorJacob.EvidenciaRechazadaPorProxy, resultado.Codigo);
		Assert.NotEqual(CodigosErrorJacob.ErrorTecnico, resultado.Codigo);
	}

	[Fact]
	public async Task El413SigueSiendoTecnico_paraQueSubaSolaCuandoLevantenElLimite()
	{
		// Técnico a propósito: no es culpa del operador, y sube solo cuando infraestructura suba el límite.
		var resultado = await SubirCon(HttpStatusCode.RequestEntityTooLarge, HtmlDeNginx);

		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
		Assert.False(CodigosErrorJacob.EsFuncional(resultado.Codigo));
	}

	[Fact]
	public async Task El413NoCulpaAlOperadorDeSuArchivo()
	{
		// No es el rechazo de Jacob por tamaño: ese lo arregla el operador eligiendo otro archivo.
		var resultado = await SubirCon(HttpStatusCode.RequestEntityTooLarge, HtmlDeNginx);

		Assert.NotEqual("appevidencias.archivo.demasiadogrande", resultado.Codigo);
		Assert.Contains("servidor", resultado.Mensaje!, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	[InlineData(HttpStatusCode.GatewayTimeout)]
	public async Task LosDemasErroresDelProxy_seReintentanSinReventar(HttpStatusCode estado)
	{
		// Tampoco son Envelope.
		var resultado = await SubirCon(estado, "<html><body>502 Bad Gateway</body></html>");

		Assert.False(resultado.Exito);
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
	}

	[Fact]
	public async Task UnCuerpoVacio_diceElCodigoEnVezDeReventar()
	{
		// Un 401 del middleware de JWT llega sin cuerpo.
		var resultado = await SubirCon(HttpStatusCode.Unauthorized, string.Empty);

		Assert.False(resultado.Exito);
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
		Assert.Contains("401", resultado.Mensaje!);
	}

	[Fact]
	public async Task UnRechazoDeJacobSigueLeyendoseDelSobre()
	{
		// El camino que ya funcionaba no se toca: con Envelope manda el servidor.
		var resultado = await SubirCon(
			HttpStatusCode.BadRequest,
			"""{"resultado":null,"codigoError":"appevidencias.formato.nopermitido","mensajeError":"Formato no admitido"}""");

		Assert.Equal("appevidencias.formato.nopermitido", resultado.Codigo);
		Assert.Equal(FamiliaErrorSincronizacion.Funcional, resultado.Familia);
	}

	[Fact]
	public async Task UnaSubidaAceptadaSigueSiendoExito()
	{
		var resultado = await SubirCon(
			HttpStatusCode.OK,
			"""{"resultado":{"idEvidencia":"ev-1","tipoMime":"image/jpeg","hashSha256":"abc","yaExistia":false},"codigoError":"","mensajeError":""}""");

		Assert.True(resultado.Exito);
		Assert.Equal("ev-1", resultado.Registrada!.IdEvidencia);
	}

	[Fact]
	public async Task UnErrorDelApiConPaginaHtml_diceSuCodigoHttp()
	{
		var resultado = await SubirCon(
			HttpStatusCode.InternalServerError, "<html><body>Internal Server Error</body></html>");

		Assert.False(resultado.Exito);
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
		Assert.Contains("HTTP 500", resultado.Mensaje);
	}

	[Fact]
	public async Task UnErrorSinSobre_noSeLeeComoSiLoHubieraAceptado()
	{
		var resultado = await SubirCon(
			HttpStatusCode.NotFound,
			"""{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.5","title":"Not Found","status":404}""");

		Assert.False(resultado.Exito);
		Assert.Contains("HTTP 404", resultado.Mensaje);
		Assert.DoesNotContain("aceptó", resultado.Mensaje);
	}

	private Task<ResultadoEnvioEvidencia> SubirCon(HttpStatusCode estado, string cuerpo)
	{
		var respuesta = new HttpResponseMessage(estado)
		{
			Content = new StringContent(cuerpo, Encoding.UTF8, "text/html"),
		};

		var http = new HttpClient(ManejadorHttpFalso.Siempre(respuesta))
		{
			BaseAddress = new Uri("http://localhost:5231/"),
		};

		var cliente = new ClienteEvidenciasJacob(
			http,
			new ConfiguracionApi { UrlBase = "http://localhost:5231/" });

		return cliente.SubirAsync(
			Guid.NewGuid().ToString(),
			_archivo,
			"LOC-000123-181409.jpg",
			TokenDePrueba.Con("APP_OPERADOR_CAPTURA"));
	}
}
