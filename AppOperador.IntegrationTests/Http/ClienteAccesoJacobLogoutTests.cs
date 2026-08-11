using System.Net;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Aviso de cierre de sesión: <c>POST ITS/AppLogin/Logout</c> (JTT-1390).
/// </summary>
/// <remarks>
/// A diferencia de los dos pasos del acceso, este endpoint exige el token en la cabecera y
/// no devuelve nada que interpretar: basta el código de estado.
/// </remarks>
public class ClienteAccesoJacobLogoutTests
{
	private const string Token = "jwt-de-la-sesion";

	private static (ClienteAccesoJacob Cliente, ManejadorHttpFalso Manejador) Construir(
		HttpResponseMessage respuesta)
	{
		var manejador = ManejadorHttpFalso.Siempre(respuesta);
		return (Nuevo(manejador), manejador);
	}

	private static ClienteAccesoJacob Nuevo(ManejadorHttpFalso manejador) =>
		new(new HttpClient(manejador), new ConfiguracionApi { UrlBase = "http://localhost:5231" });

	[Fact]
	public async Task Confirma_el_cierre_cuando_Jacob_responde_correctamente()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		Assert.True(await cliente.CerrarSesionAsync(Token));
	}

	[Fact]
	public async Task Llama_a_la_ruta_de_cierre_con_el_token_en_la_cabecera()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		await cliente.CerrarSesionAsync(Token);

		Assert.Single(manejador.Peticiones);
		Assert.Contains("AppLogin/Logout", manejador.Peticiones[0].Url, StringComparison.OrdinalIgnoreCase);
		Assert.Equal($"Bearer {Token}", manejador.Peticiones[0].Autorizacion);
	}

	[Fact]
	public async Task El_token_no_viaja_en_el_cuerpo()
	{
		// Va en la cabecera y en ningun otro sitio: un cuerpo con el token acabaria en
		// cualquier traza intermedia.
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		await cliente.CerrarSesionAsync(Token);

		Assert.True(string.IsNullOrEmpty(manejador.Peticiones[0].Cuerpo));
	}

	[Theory]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.BadRequest)]
	[InlineData(HttpStatusCode.InternalServerError)]
	public async Task No_confirma_el_cierre_si_Jacob_lo_rechaza(HttpStatusCode codigo)
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(codigo));

		Assert.False(await cliente.CerrarSesionAsync(Token));
	}

	[Fact]
	public async Task Sin_servidor_al_otro_lado_no_confirma_pero_no_lanza()
	{
		// El cierre local depende de que esto no explote: es el caso de campo sin señal.
		var cliente = Nuevo(ManejadorHttpFalso.ConexionRechazada());

		Assert.False(await cliente.CerrarSesionAsync(Token));
	}

	[Fact]
	public async Task Si_el_servidor_no_contesta_a_tiempo_no_confirma_pero_no_lanza()
	{
		var cliente = Nuevo(ManejadorHttpFalso.TiempoAgotado());

		Assert.False(await cliente.CerrarSesionAsync(Token));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Sin_token_no_se_hace_ninguna_peticion(string token)
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		Assert.False(await cliente.CerrarSesionAsync(token));
		Assert.Empty(manejador.Peticiones);
	}
}
