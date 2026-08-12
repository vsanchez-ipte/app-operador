using System.Net;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Sonda de comunicación: <c>GET ITS/AppLogin/Estado</c> (JTT-1391 CA 3).
/// </summary>
/// <remarks>
/// Es la comprobación liviana del indicador de enlace. No devuelve datos del operador ni de
/// la unidad, y está pensada para invocarse a menudo: por eso basta el código de estado y no
/// se interpreta el cuerpo.
/// </remarks>
public class ClienteAccesoJacobEstadoTests
{
	private const string Token = "jwt-de-la-sesion";

	private static (ClienteAccesoJacob Cliente, ManejadorHttpFalso Manejador) Construir(
		HttpResponseMessage respuesta)
	{
		var manejador = ManejadorHttpFalso.Siempre(respuesta);
		return (Nuevo(manejador), manejador);
	}

	private static ClienteAccesoJacob Nuevo(ManejadorHttpFalso manejador) =>
		new(new HttpClient(manejador),
			new ConfiguracionApi { UrlBase = "http://localhost:5231" },
			new LectorClaimsToken());

	[Fact]
	public async Task Confirma_el_enlace_cuando_Jacob_responde_correctamente()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		Assert.True(await cliente.ComprobarEnlaceAsync(Token));
	}

	[Fact]
	public async Task Consulta_la_ruta_de_estado_con_el_token_en_la_cabecera()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		await cliente.ComprobarEnlaceAsync(Token);

		Assert.Single(manejador.Peticiones);
		Assert.Contains("AppLogin/Estado", manejador.Peticiones[0].Url, StringComparison.OrdinalIgnoreCase);
		Assert.Equal($"Bearer {Token}", manejador.Peticiones[0].Autorizacion);
	}

	[Fact]
	public async Task La_sonda_no_lleva_cuerpo()
	{
		// Es la comprobacion liviana: se invoca a menudo y no debe cargar nada.
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		await cliente.ComprobarEnlaceAsync(Token);

		Assert.True(string.IsNullOrEmpty(manejador.Peticiones[0].Cuerpo));
	}

	[Theory]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.NotFound)]
	public async Task Cualquier_respuesta_que_no_sea_correcta_es_falta_de_enlace(HttpStatusCode codigo)
	{
		// Para el indicador, "la sesion se acabo" y "el servidor fallo" significan lo mismo:
		// no se puede operar contra Jacob.
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(codigo));

		Assert.False(await cliente.ComprobarEnlaceAsync(Token));
	}

	[Fact]
	public async Task Sin_servidor_al_otro_lado_no_hay_enlace()
	{
		var cliente = Nuevo(ManejadorHttpFalso.ConexionRechazada());

		Assert.False(await cliente.ComprobarEnlaceAsync(Token));
	}

	[Fact]
	public async Task Si_el_servidor_no_contesta_a_tiempo_no_hay_enlace()
	{
		// El caso que separa "tengo WiFi" de "alcanzo a Jacob" (CA 2): hay red, pero el
		// servidor no responde.
		var cliente = Nuevo(ManejadorHttpFalso.TiempoAgotado());

		Assert.False(await cliente.ComprobarEnlaceAsync(Token));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Sin_token_no_se_hace_ninguna_peticion(string token)
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		Assert.False(await cliente.ComprobarEnlaceAsync(token));
		Assert.Empty(manejador.Peticiones);
	}
}
