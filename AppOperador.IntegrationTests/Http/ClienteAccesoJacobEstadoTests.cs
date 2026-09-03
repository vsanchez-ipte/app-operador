using System.Net;
using AppOperador.Aplicacion.Modelos;
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

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.True(sondeo.HayEnlace);
		Assert.Equal(CausaSinEnlace.Ninguna, sondeo.Causa);
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
		// no se puede operar contra Jacob. La causa si los separa.
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(codigo));

		Assert.False((await cliente.ComprobarEnlaceAsync(Token)).HayEnlace);
	}

	[Fact]
	public async Task Sin_servidor_al_otro_lado_no_hay_enlace()
	{
		var cliente = Nuevo(ManejadorHttpFalso.ConexionRechazada());

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.False(sondeo.HayEnlace);
		Assert.Equal(CausaSinEnlace.SinTransporte, sondeo.Causa);
		Assert.False(sondeo.ServidorRespondio);
	}

	[Fact]
	public async Task Si_el_servidor_no_contesta_a_tiempo_no_hay_enlace()
	{
		// El caso que separa "tengo WiFi" de "alcanzo a Jacob" (CA 2): hay red, pero el
		// servidor no responde.
		var cliente = Nuevo(ManejadorHttpFalso.TiempoAgotado());

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.False(sondeo.HayEnlace);
		Assert.Equal(CausaSinEnlace.SinTransporte, sondeo.Causa);
	}

	// ---------- Regresión: un 404 no es falta de conexión ----------

	[Theory]
	[InlineData(HttpStatusCode.NotFound)]
	[InlineData(HttpStatusCode.InternalServerError)]
	[InlineData(HttpStatusCode.BadGateway)]
	[InlineData(HttpStatusCode.ServiceUnavailable)]
	public async Task Un_error_del_servidor_no_se_confunde_con_falta_de_comunicacion(HttpStatusCode codigo)
	{
		// El fallo que esto fija: la app apuntaba a un servidor que no publicaba
		// ITS/AppLogin/Estado, respondia 404, y la pantalla decia "Sin conexion / Modo
		// offline". Mando a revisar la red durante una sesion entera cuando el problema era
		// de despliegue. Si el servidor contesto, hubo comunicacion.
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(codigo));

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.False(sondeo.HayEnlace);
		Assert.True(sondeo.ServidorRespondio);
		Assert.Equal(CausaSinEnlace.RespuestaDeError, sondeo.Causa);
		Assert.Equal((int)codigo, sondeo.CodigoHttp);
		Assert.NotEqual(CausaSinEnlace.SinTransporte, sondeo.Causa);
	}

	[Theory]
	[InlineData(HttpStatusCode.Unauthorized)]
	[InlineData(HttpStatusCode.Forbidden)]
	public async Task Que_Jacob_niegue_la_sesion_se_distingue_de_un_fallo_del_servidor(HttpStatusCode codigo)
	{
		// Tambien hubo comunicacion, pero el remedio es otro: volver a autenticarse.
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(codigo));

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.False(sondeo.HayEnlace);
		Assert.True(sondeo.ServidorRespondio);
		Assert.Equal(CausaSinEnlace.SesionRechazada, sondeo.Causa);
	}

	[Fact]
	public async Task El_detalle_tecnico_lleva_el_codigo_para_el_registro()
	{
		// No se muestra en pantalla: es lo que se escribe en el log para poder diagnosticar.
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.NotFound));

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.Contains("404", sondeo.Detalle);
	}

	// ---------- Regresión: la petición no se libera con el envío en vuelo ----------

	[Fact]
	public async Task La_peticion_sigue_viva_mientras_el_envio_esta_en_curso()
	{
		// El fallo que esto fija: el metodo devolvia la tarea de SendAsync sin esperarla, asi
		// que el using desechaba la peticion con el envio todavia en vuelo. Con red estable
		// no se notaba; al reconectar, el camino lento perdia la carrera y salia
		// "Cannot access a disposed object" al pulsar Reintentar.
		var manejador = ManejadorHttpFalso.Lento(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));
		var cliente = Nuevo(manejador);

		var sondeo = await cliente.ComprobarEnlaceAsync(Token);

		Assert.False(manejador.PeticionLiberadaEnVuelo);
		Assert.True(sondeo.HayEnlace);
	}

	[Fact]
	public async Task El_cierre_de_sesion_tampoco_libera_la_peticion_antes_de_tiempo()
	{
		// Comparte el mismo metodo de envio, asi que compartia el mismo fallo.
		var manejador = ManejadorHttpFalso.Lento(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		await Nuevo(manejador).CerrarSesionAsync(Token);

		Assert.False(manejador.PeticionLiberadaEnVuelo);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Sin_token_no_se_hace_ninguna_peticion(string token)
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.OK));

		var sondeo = await cliente.ComprobarEnlaceAsync(token);

		Assert.False(sondeo.HayEnlace);
		Assert.Equal(CausaSinEnlace.SinSesion, sondeo.Causa);
		Assert.Empty(manejador.Peticiones);
	}
}
