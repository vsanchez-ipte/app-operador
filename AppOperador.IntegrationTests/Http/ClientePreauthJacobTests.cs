using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Comprueba el flujo <c>GetPublicKey → cifrado → Preauth</c> y la traducción de cada
/// desenlace posible, contra un servidor simulado.
/// </summary>
public sealed class ClientePreauthJacobTests
{
	private const string PreauthCorrecto = """
		{
		  "resultado": {
		    "challengeId": "3f2a0c1e-9d44-4f6b-8f21-6c0d8b7a1e55",
		    "expiresAtUtc": "2026-08-05T19:05:00Z",
		    "unidades": [
		      { "id": "11111111-1111-1111-1111-111111111111", "clave": "VEH-01", "descripcion": "Unidad local de prueba" },
		      { "id": "22222222-2222-2222-2222-222222222222", "clave": "VEH-02", "descripcion": "Segunda unidad" }
		    ]
		  },
		  "codigoError": null,
		  "mensajeError": null
		}
		""";

	private static string LlavePublica()
	{
		using var rsa = RSA.Create(2048);
		return Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
	}

	private static (ClientePreauthJacob Cliente, ManejadorHttpFalso Manejador) Construir(
		HttpResponseMessage respuestaPreauth)
	{
		var manejador = ManejadorHttpFalso.ConLlaveY(LlavePublica(), respuestaPreauth);
		return (Nuevo(manejador), manejador);
	}

	private static ClientePreauthJacob Nuevo(ManejadorHttpFalso manejador) =>
		new(new HttpClient(manejador), new ConfiguracionApi { UrlBase = "http://localhost:5231" });

	private static Task<ResultadoPreauth> PreautenticarAsync(ClientePreauthJacob cliente) =>
		cliente.PreautenticarAsync("operador@ipte.com.mx", "Secreta123");

	private static HttpResponseMessage ErrorFuncional(string codigo, string mensaje = "…") =>
		ManejadorHttpFalso.Json(
			HttpStatusCode.BadRequest,
			$$"""{"resultado":null,"codigoError":"{{codigo}}","mensajeError":"{{mensaje}}"}""");

	// ---------- Camino correcto ----------

	[Fact]
	public async Task Devuelve_el_desafio_cuando_Jacob_acepta_las_credenciales()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, PreauthCorrecto));

		var resultado = await PreautenticarAsync(cliente);

		Assert.True(resultado.Exitoso);
		Assert.Equal("3f2a0c1e-9d44-4f6b-8f21-6c0d8b7a1e55", resultado.ChallengeId);
		Assert.Equal(new DateTime(2026, 8, 5, 19, 5, 0, DateTimeKind.Utc), resultado.ExpiraUtc!.Value.ToUniversalTime());
		Assert.Null(resultado.Motivo);
	}

	[Fact]
	public async Task Deserializa_las_unidades_aunque_esta_HUT_no_las_muestre()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, PreauthCorrecto));

		var resultado = await PreautenticarAsync(cliente);

		Assert.Equal(2, resultado.Unidades.Count);
		Assert.Equal("VEH-01", resultado.Unidades[0].Clave);
	}

	[Fact]
	public async Task Pide_la_llave_publica_antes_de_preautenticar()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, PreauthCorrecto));

		await PreautenticarAsync(cliente);

		Assert.Equal(2, manejador.Peticiones.Count);
		Assert.Contains("GetPublicKey", manejador.Peticiones[0].Url, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("AppLogin/Preauth", manejador.Peticiones[1].Url, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public async Task Envia_las_credenciales_cifradas_y_nunca_en_claro()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, PreauthCorrecto));

		await PreautenticarAsync(cliente);

		var cuerpo = manejador.Peticiones[1].Cuerpo!;

		// Ni el correo ni la contraseña pueden aparecer legibles en la petición.
		Assert.DoesNotContain("operador@ipte.com.mx", cuerpo, StringComparison.Ordinal);
		Assert.DoesNotContain("Secreta123", cuerpo, StringComparison.Ordinal);

		var enviado = JsonDocument.Parse(cuerpo).RootElement;
		var email = enviado.GetProperty("email").GetString()!;
		var password = enviado.GetProperty("password").GetString()!;

		// Base64 de un bloque RSA de 2048 bits: 256 bytes -> 344 caracteres.
		Assert.Equal(344, email.Length);
		Assert.Equal(344, password.Length);
		Assert.NotEqual(email, password);
		Assert.Equal("Android", enviado.GetProperty("plataforma").GetString());
	}

	// ---------- Errores funcionales del catálogo ----------

	[Theory]
	[InlineData("appoperador.credenciales.invalidas", MotivoRechazoAcceso.CredencialInvalida)]
	[InlineData("appoperador.permiso.requerido", MotivoRechazoAcceso.SinPermiso)]
	[InlineData("appoperador.cuenta.inactiva", MotivoRechazoAcceso.CuentaInactiva)]
	[InlineData("appoperador.cuenta.bloqueada", MotivoRechazoAcceso.CuentaBloqueada)]
	[InlineData("appoperador.sin.vehiculos", MotivoRechazoAcceso.SinUnidades)]
	public async Task Traduce_cada_codigo_del_catalogo_a_su_causa(string codigo, MotivoRechazoAcceso esperado)
	{
		var (cliente, _) = Construir(ErrorFuncional(codigo));

		var resultado = await PreautenticarAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal(esperado, resultado.Motivo);
		Assert.Equal(codigo, resultado.CodigoError);
		Assert.Null(resultado.ChallengeId);
	}

	[Fact]
	public async Task Un_codigo_desconocido_no_se_confunde_con_credencial_invalida()
	{
		var (cliente, _) = Construir(ErrorFuncional("appoperador.algo.que.no.existia"));

		var resultado = await PreautenticarAsync(cliente);

		// Decirle al operador que su contraseña está mal cuando el API reportó otra cosa
		// lo mandaría a corregir algo que no falla.
		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
	}

	// ---------- Respuestas fuera del catálogo ----------

	[Fact]
	public async Task Un_cuerpo_que_no_es_Envelope_se_reporta_como_fallo_del_servicio()
	{
		// Forma real que devuelve el API cuando el JSON no pasa la validación de modelo.
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(
			HttpStatusCode.BadRequest,
			"""{"codigo":"422","mensaje":"Revisa cada uno de los datos ingresados, por favor."}"""));

		var resultado = await PreautenticarAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
	}

	[Fact]
	public async Task Un_401_sin_cuerpo_se_reporta_como_fallo_del_servicio()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.Unauthorized));

		var resultado = await PreautenticarAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
		Assert.Equal("http.401", resultado.CodigoError);
	}

	[Fact]
	public async Task Un_JSON_ilegible_se_reporta_como_fallo_del_servicio()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, "<html>Proxy error</html>"));

		var resultado = await PreautenticarAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
	}

	[Fact]
	public async Task Un_200_sin_desafio_no_se_toma_por_exito()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(
			HttpStatusCode.OK,
			"""{"resultado":{"challengeId":null,"unidades":[]},"codigoError":null,"mensajeError":null}"""));

		var resultado = await PreautenticarAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
	}

	// ---------- Llave pública ----------

	[Fact]
	public async Task Una_llave_publica_invalida_no_se_confunde_con_credencial_mala()
	{
		var manejador = ManejadorHttpFalso.Siempre(ManejadorHttpFalso.Json(
			HttpStatusCode.OK,
			"""{"resultado":"esto-no-es-una-llave","codigoError":"","mensajeError":""}"""));

		var resultado = await PreautenticarAsync(Nuevo(manejador));

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
		Assert.Equal("llave.invalida", resultado.CodigoError);
	}

	// ---------- Red ----------

	[Fact]
	public async Task Una_conexion_rechazada_se_reporta_como_falta_de_comunicacion()
	{
		var resultado = await PreautenticarAsync(Nuevo(ManejadorHttpFalso.ConexionRechazada()));

		// Es lo que distingue "no hay servidor" de "tu contraseña está mal": con lo primero
		// el operador puede seguir sin conexión, con lo segundo no.
		Assert.Equal(MotivoRechazoAcceso.SinComunicacion, resultado.Motivo);
		Assert.Equal("conexion.fallida", resultado.CodigoError);
	}

	[Fact]
	public async Task Un_tiempo_de_espera_agotado_se_reporta_como_falta_de_comunicacion()
	{
		var resultado = await PreautenticarAsync(Nuevo(ManejadorHttpFalso.TiempoAgotado()));

		Assert.Equal(MotivoRechazoAcceso.SinComunicacion, resultado.Motivo);
		Assert.Equal("tiempo.agotado", resultado.CodigoError);
	}

	// ---------- Seguridad ----------

	[Fact]
	public async Task El_resultado_de_un_rechazo_no_arrastra_datos_sensibles()
	{
		var (cliente, _) = Construir(ErrorFuncional(
			"appoperador.credenciales.invalidas", "Usuario o contraseña no válidos."));

		var resultado = await PreautenticarAsync(cliente);

		// Solo viaja el código de catálogo. El mensaje del API es texto para desarrollador y
		// no debe llegar a la interfaz: el literal lo fija JTT-279.
		Assert.Equal("appoperador.credenciales.invalidas", resultado.CodigoError);
		Assert.Null(resultado.ChallengeId);
		Assert.Empty(resultado.Unidades);
	}
}
