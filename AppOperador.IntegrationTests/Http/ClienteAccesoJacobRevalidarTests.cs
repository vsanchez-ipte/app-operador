using System.Net;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Revalidación de la sesión: <c>POST ITS/AppLogin/Revalidar</c> (JTT-1383).
/// </summary>
/// <remarks>
/// Lo que estas pruebas protegen es la distinción entre «Jacob dijo que no» y «no se pudo
/// preguntar». Confundirlas sacaría al operador de una sesión offline vigente cada vez que
/// el enlace parpadee.
/// </remarks>
public class ClienteAccesoJacobRevalidarTests
{
	private const string Token = "jwt-de-la-sesion";

	private const string RevalidacionCorrecta = """
		{
		  "resultado": {
		    "sessionId": "9c1f0b2e-4d3a-4a55-9f01-2b7c8d9e0a11",
		    "estado": "activa",
		    "rol": { "id": 45, "nombre": "Operador Prueba" },
		    "unidad": { "id": "veh-1", "clave": "VEH-01", "descripcion": "Unidad local de prueba" },
		    "permisos": ["APP_OPERADOR_MOVIL"],
		    "lastValidatedAtUtc": "2026-08-11T03:00:00Z",
		    "offlineUntilUtc": "2026-08-11T11:00:00Z",
		    "serverTimeUtc": "2026-08-11T03:00:00Z"
		  },
		  "codigoError": null,
		  "mensajeError": null
		}
		""";

	private static (ClienteAccesoJacob Cliente, ManejadorHttpFalso Manejador) Construir(
		HttpResponseMessage respuesta)
	{
		var manejador = ManejadorHttpFalso.Siempre(respuesta);
		return (Nuevo(manejador), manejador);
	}

	private static ClienteAccesoJacob Nuevo(ManejadorHttpFalso manejador) =>
		new(new HttpClient(manejador), new ConfiguracionApi { UrlBase = "http://localhost:5231" }, new LectorClaimsToken());

	private static HttpResponseMessage ErrorFuncional(string codigo) =>
		ManejadorHttpFalso.Json(
			HttpStatusCode.BadRequest,
			$$"""{"resultado":null,"codigoError":"{{codigo}}","mensajeError":"..."}""");

	// ---------- Confirmación ----------

	[Fact]
	public async Task Adopta_la_ventana_que_devuelve_el_servidor()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, RevalidacionCorrecta));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.True(resultado.Exitoso);
		Assert.Equal(new DateTime(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc), resultado.Vigencia!.LastValidatedAtUtc);
		Assert.Equal(new DateTime(2026, 8, 11, 11, 0, 0, DateTimeKind.Utc), resultado.Vigencia.OfflineUntilUtc);
	}

	[Fact]
	public async Task Devuelve_la_unidad_y_los_permisos_vigentes()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, RevalidacionCorrecta));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.Equal("VEH-01", resultado.Unidad!.Clave);
		Assert.Equal("veh-1", resultado.Unidad.Id);
		Assert.Equal(["APP_OPERADOR_MOVIL"], resultado.Permisos!);
		Assert.Equal("Operador Prueba", resultado.Rol);
	}

	[Fact]
	public async Task Llama_a_la_ruta_de_revalidacion_con_el_token_en_la_cabecera()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, RevalidacionCorrecta));

		await cliente.RevalidarAsync(Token);

		Assert.Single(manejador.Peticiones);
		Assert.Contains("AppLogin/Revalidar", manejador.Peticiones[0].Url, StringComparison.OrdinalIgnoreCase);
		Assert.Equal($"Bearer {Token}", manejador.Peticiones[0].Autorizacion);
	}

	// ---------- Negativa firme ----------

	[Theory]
	[InlineData("appoperador.sesion.revocada", MotivoRechazoAcceso.SesionRevocada)]
	[InlineData("appoperador.sesion.invalida", MotivoRechazoAcceso.SesionRevocada)]
	[InlineData("appoperador.permiso.requerido", MotivoRechazoAcceso.SinPermiso)]
	[InlineData("appoperador.cuenta.inactiva", MotivoRechazoAcceso.CuentaInactiva)]
	[InlineData("appoperador.vehiculo.noautorizado", MotivoRechazoAcceso.UnidadNoAutorizada)]
	public async Task Un_codigo_funcional_es_una_negativa_firme(string codigo, MotivoRechazoAcceso esperado)
	{
		var (cliente, _) = Construir(ErrorFuncional(codigo));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.True(resultado.EsRechazoDefinitivo);
		Assert.Equal(esperado, resultado.Motivo);
	}

	[Fact]
	public async Task Un_401_significa_que_la_sesion_se_acabo()
	{
		// El token ya no autentica: para Jacob esa sesion no existe.
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.Unauthorized));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.True(resultado.EsRechazoDefinitivo);
		Assert.Equal(MotivoRechazoAcceso.SesionRevocada, resultado.Motivo);
	}

	[Fact]
	public async Task Sin_token_no_se_hace_ninguna_peticion()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, RevalidacionCorrecta));

		var resultado = await cliente.RevalidarAsync("");

		Assert.True(resultado.EsRechazoDefinitivo);
		Assert.Empty(manejador.Peticiones);
	}

	// ---------- No se pudo preguntar ----------

	[Fact]
	public async Task Sin_servidor_al_otro_lado_no_hay_negativa()
	{
		var cliente = Nuevo(ManejadorHttpFalso.ConexionRechazada());

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.False(resultado.Exitoso);
		Assert.False(resultado.EsRechazoDefinitivo);
		Assert.Equal("conexion.fallida", resultado.CodigoError);
	}

	[Fact]
	public async Task Si_el_servidor_no_contesta_a_tiempo_no_hay_negativa()
	{
		var cliente = Nuevo(ManejadorHttpFalso.TiempoAgotado());

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.False(resultado.EsRechazoDefinitivo);
		Assert.Equal("tiempo.agotado", resultado.CodigoError);
	}

	[Fact]
	public async Task Un_cuerpo_ilegible_no_es_una_negativa()
	{
		// Un fallo de integracion no puede costarle la sesion al operador.
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, "esto no es json"));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.False(resultado.EsRechazoDefinitivo);
	}

	[Fact]
	public async Task Una_respuesta_sin_fechas_no_es_una_negativa()
	{
		const string SinFechas = """
			{
			  "resultado": { "sessionId": "s-1", "unidad": { "id": "veh-1", "clave": "VEH-01" } },
			  "codigoError": null, "mensajeError": null
			}
			""";
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, SinFechas));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.False(resultado.Exitoso);
		Assert.False(resultado.EsRechazoDefinitivo);
		Assert.Equal("respuesta.incompleta", resultado.CodigoError);
	}

	[Fact]
	public async Task Una_ventana_que_termina_antes_de_empezar_no_se_adopta()
	{
		const string Invertida = """
			{
			  "resultado": {
			    "sessionId": "s-1",
			    "unidad": { "id": "veh-1", "clave": "VEH-01" },
			    "lastValidatedAtUtc": "2026-08-11T03:00:00Z",
			    "offlineUntilUtc": "2026-08-11T02:00:00Z"
			  },
			  "codigoError": null, "mensajeError": null
			}
			""";
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, Invertida));

		var resultado = await cliente.RevalidarAsync(Token);

		Assert.False(resultado.Exitoso);
		Assert.False(resultado.EsRechazoDefinitivo);
	}
}
