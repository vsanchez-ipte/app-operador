using System.Net;
using System.Text;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Lo que contesta el proxy, no Jacob, al subir una evidencia (JTT-1398).
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe por un defecto real encontrado el 31 de agosto.</b> El nginx que va delante del API
/// en el <c>.215</c> y el <c>.230</c> está en su <c>client_max_body_size</c> por omisión de 1 MB
/// y devuelve <b>413 con cuerpo HTML</b> antes de que la petición llegue a Kestrel. El cliente no
/// miraba el código de estado: intentaba deserializar el HTML, reventaba con
/// <c>JsonException</c>, y el <c>catch</c> lo convertía en un fallo técnico genérico. La
/// evidencia se quedaba reintentando para siempre contra un servidor que nunca la iba a aceptar,
/// y el operador solo veía «no se pudo subir».
/// </para>
/// <para>
/// Se prueba con el HTML que nginx devuelve de verdad, no con un cuerpo inventado.
/// </para>
/// </remarks>
public sealed class ClienteEvidenciasJacobProxyTests : IDisposable
{
	/// <summary>Lo que responde nginx 1.24.0 al pasarse del tamaño.</summary>
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
		// Deliberado, con el mismo criterio que PlazaNoResuelta: no es culpa del operador ni de
		// su archivo, así que marcarlo funcional dejaría la evidencia parada para siempre
		// esperando que alguien la tocara a mano. Como técnico, el pendiente se conserva y sube
		// solo el día que infraestructura levante client_max_body_size.
		var resultado = await SubirCon(HttpStatusCode.RequestEntityTooLarge, HtmlDeNginx);

		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
		Assert.False(CodigosErrorJacob.EsFuncional(resultado.Codigo));
	}

	[Fact]
	public async Task El413NoCulpaAlOperadorDeSuArchivo()
	{
		// No se reutiliza el rechazo de Jacob por tamaño: aquél lo arregla el operador eligiendo
		// otro archivo, y su foto de 2 MB cabe de sobra en el límite publicado.
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
		// Tampoco son Envelope: antes acababan igual, en el catch de JsonException.
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
