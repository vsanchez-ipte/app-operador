using System.Net;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Segundo paso del acceso: <c>POST ITS/AppLogin</c>, que consume el desafío y crea la
/// sesión móvil (JTT-1382).
/// </summary>
/// <remarks>
/// A diferencia del paso 1, este endpoint es anónimo y no cifra nada: el desafío es la
/// credencial. Por eso aquí se responde a una sola petición y no a dos.
/// </remarks>
public class ClienteAccesoJacobLoginTests
{
	/// <summary>Token con el permiso funcional firmado, como el que emite Jacob.</summary>
	private static readonly string TokenConPermiso = TokenDePrueba.Con("APP_OPERADOR_MOVIL");

	private static readonly string LoginCorrecto =
		CuerpoLogin(TokenConPermiso, """["APP_OPERADOR_MOVIL"]""");

	/// <summary>
	/// Respuesta correcta de <c>AppLogin</c>, con el token y los permisos que se le indiquen.
	/// </summary>
	/// <remarks>
	/// Los dos son parámetros porque el cotejo de JTT-1379 CA 8 se prueba desalineándolos: un
	/// cuerpo que conceda algo que el token no respalda no debe abrir sesión.
	/// </remarks>
	private static string CuerpoLogin(string token, string permisosJson) => $$"""
		{
		  "resultado": {
		    "accessToken": "{{token}}",
		    "tokenExpiresAtUtc": "2026-08-11T03:00:00Z",
		    "sessionId": "9c1f0b2e-4d3a-4a55-9f01-2b7c8d9e0a11",
		    "operador": { "id": "op-1", "email": "operador@ipte.com.mx", "nombre": "Juan Pérez" },
		    "rol": { "id": 45, "nombre": "Operador Prueba" },
		    "unidad": { "id": "veh-1", "clave": "VEH-01", "descripcion": "Unidad local de prueba" },
		    "permisos": {{permisosJson}},
		    "lastValidatedAtUtc": "2026-08-10T19:00:00Z",
		    "offlineUntilUtc": "2026-08-11T03:00:00Z",
		    "serverTimeUtc": "2026-08-10T19:00:00Z"
		  },
		  "codigoError": null,
		  "mensajeError": null
		}
		""";

	private static (ClienteAccesoJacob Cliente, ManejadorHttpFalso Manejador) Construir(HttpResponseMessage respuesta)
	{
		var manejador = ManejadorHttpFalso.Siempre(respuesta);
		return (Nuevo(manejador), manejador);
	}

	private static ClienteAccesoJacob Nuevo(ManejadorHttpFalso manejador) =>
		new(new HttpClient(manejador), new ConfiguracionApi { UrlBase = "http://localhost:5231" }, new LectorClaimsToken());

	private static Task<ResultadoLogin> AbrirAsync(ClienteAccesoJacob cliente) =>
		cliente.CompletarAccesoAsync("d-1", "veh-1");

	private static HttpResponseMessage ErrorFuncional(string codigo) =>
		ManejadorHttpFalso.Json(
			HttpStatusCode.BadRequest,
			$$"""{"resultado":null,"codigoError":"{{codigo}}","mensajeError":"..."}""");

	// ---------- Camino correcto ----------

	[Fact]
	public async Task Crea_la_sesion_con_los_datos_que_devuelve_Jacob()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, LoginCorrecto));

		var resultado = await AbrirAsync(cliente);

		Assert.True(resultado.Exitoso);
		var sesion = resultado.Sesion!;
		Assert.Equal(TokenConPermiso, sesion.AccessToken);
		Assert.Equal("9c1f0b2e-4d3a-4a55-9f01-2b7c8d9e0a11", sesion.SessionId);
		Assert.Equal("Juan Pérez", sesion.Operador);
		Assert.Equal("Operador Prueba", sesion.Rol);
		Assert.Equal("VEH-01", sesion.Unidad.Clave);
		Assert.Equal("veh-1", sesion.Unidad.Id);
		Assert.Equal(["APP_OPERADOR_MOVIL"], sesion.Permisos);
	}

	[Fact]
	public async Task Envia_el_desafio_y_el_identificador_de_la_unidad()
	{
		var (cliente, manejador) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, LoginCorrecto));

		await cliente.CompletarAccesoAsync("desafio-42", "veh-9");

		Assert.Single(manejador.Peticiones);
		Assert.Contains("AppLogin", manejador.Peticiones[0].Url, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("desafio-42", manejador.Peticiones[0].Cuerpo!, StringComparison.Ordinal);
		Assert.Contains("veh-9", manejador.Peticiones[0].Cuerpo!, StringComparison.Ordinal);
	}

	[Fact]
	public async Task No_pide_la_llave_publica_porque_el_endpoint_es_anonimo()
	{
		// Nada que cifrar: por aquí no viaja ningún dato del operador.
		var (cliente, manejador) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, LoginCorrecto));

		await AbrirAsync(cliente);

		Assert.DoesNotContain(manejador.Peticiones, p =>
			p.Url.Contains("GetPublicKey", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Adopta_la_ventana_offline_del_servidor_sin_recalcularla()
	{
		// CA 3 y CA 4. Las fechas del ejemplo son las del contrato: 19:00 → 03:00 del día
		// siguiente. La app no vuelve a sumar ocho horas sobre su propio reloj.
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, LoginCorrecto));

		var resultado = await AbrirAsync(cliente);

		var vigencia = resultado.Sesion!.Vigencia;
		Assert.Equal(new DateTime(2026, 8, 10, 19, 0, 0, DateTimeKind.Utc), vigencia.LastValidatedAtUtc);
		Assert.Equal(new DateTime(2026, 8, 11, 3, 0, 0, DateTimeKind.Utc), vigencia.OfflineUntilUtc);
	}

	[Fact]
	public async Task Las_fechas_quedan_en_UTC_aunque_el_equipo_este_en_otro_huso()
	{
		// System.Text.Json convierte a hora local un instante con zona. Si eso llegara sin
		// normalizar, la regla de vigencia lanzaría por exigir Kind.Utc.
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, LoginCorrecto));

		var resultado = await AbrirAsync(cliente);

		Assert.Equal(DateTimeKind.Utc, resultado.Sesion!.Vigencia.LastValidatedAtUtc.Kind);
		Assert.Equal(DateTimeKind.Utc, resultado.Sesion.Vigencia.OfflineUntilUtc.Kind);
		Assert.Equal(DateTimeKind.Utc, resultado.Sesion.HoraServidorUtc.Kind);
		Assert.Equal(DateTimeKind.Utc, resultado.Sesion.TokenExpiraUtc.Kind);
	}

	// ---------- Rechazos del catálogo ----------

	[Theory]
	[InlineData("appoperador.desafio.noexiste", MotivoRechazoAcceso.DesafioNoValido)]
	[InlineData("appoperador.desafio.expirado", MotivoRechazoAcceso.DesafioNoValido)]
	[InlineData("appoperador.desafio.consumido", MotivoRechazoAcceso.DesafioNoValido)]
	[InlineData("appoperador.vehiculo.noautorizado", MotivoRechazoAcceso.UnidadNoAutorizada)]
	[InlineData("appoperador.permiso.requerido", MotivoRechazoAcceso.SinPermiso)]
	public async Task Traduce_cada_codigo_del_catalogo(string codigo, MotivoRechazoAcceso esperado)
	{
		var (cliente, _) = Construir(ErrorFuncional(codigo));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal(esperado, resultado.Motivo);
		Assert.Equal(codigo, resultado.CodigoError);
	}

	[Fact]
	public async Task Un_codigo_desconocido_no_se_presenta_como_credencial_invalida()
	{
		// Mismo criterio que JTT-1378 CA 11.
		var (cliente, _) = Construir(ErrorFuncional("appoperador.algo.nuevo"));

		var resultado = await AbrirAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
	}

	// ---------- Respuestas que no cumplen el contrato ----------

	[Fact]
	public async Task Una_respuesta_sin_token_no_produce_sesion()
	{
		const string SinToken = """
			{
			  "resultado": {
			    "sessionId": "s-1",
			    "unidad": { "id": "veh-1", "clave": "VEH-01" },
			    "lastValidatedAtUtc": "2026-08-10T19:00:00Z",
			    "offlineUntilUtc": "2026-08-11T03:00:00Z"
			  },
			  "codigoError": null, "mensajeError": null
			}
			""";
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, SinToken));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
		Assert.Equal("respuesta.incompleta", resultado.CodigoError);
	}

	[Fact]
	public async Task Una_respuesta_sin_las_fechas_de_vigencia_no_produce_sesion()
	{
		// Sin ellas no se sabe hasta cuándo vale sin conexión, y calcularlas por cuenta
		// propia es justo lo que prohíbe el CA 4.
		const string SinFechas = """
			{
			  "resultado": {
			    "accessToken": "token",
			    "sessionId": "s-1",
			    "unidad": { "id": "veh-1", "clave": "VEH-01" }
			  },
			  "codigoError": null, "mensajeError": null
			}
			""";
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, SinFechas));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal("respuesta.incompleta", resultado.CodigoError);
	}

	[Fact]
	public async Task Una_respuesta_sin_unidad_no_produce_sesion()
	{
		// La unidad es lo que la pantalla de inicio tiene que mostrar (JTT-1381 CA 13).
		const string SinUnidad = """
			{
			  "resultado": {
			    "accessToken": "token",
			    "sessionId": "s-1",
			    "lastValidatedAtUtc": "2026-08-10T19:00:00Z",
			    "offlineUntilUtc": "2026-08-11T03:00:00Z"
			  },
			  "codigoError": null, "mensajeError": null
			}
			""";
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, SinUnidad));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal("respuesta.incompleta", resultado.CodigoError);
	}

	[Fact]
	public async Task Una_ventana_offline_que_termina_antes_de_empezar_no_produce_sesion()
	{
		const string VentanaInvertida = """
			{
			  "resultado": {
			    "accessToken": "token",
			    "sessionId": "s-1",
			    "unidad": { "id": "veh-1", "clave": "VEH-01" },
			    "lastValidatedAtUtc": "2026-08-10T19:00:00Z",
			    "offlineUntilUtc": "2026-08-10T18:00:00Z"
			  },
			  "codigoError": null, "mensajeError": null
			}
			""";
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, VentanaInvertida));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal("respuesta.incompleta", resultado.CodigoError);
	}

	[Fact]
	public async Task Un_cuerpo_que_no_es_Envelope_se_trata_como_fallo_del_servicio()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(
			HttpStatusCode.BadRequest,
			"""{"codigo":"422","mensaje":"Revisa cada uno de los datos ingresados, por favor."}"""));

		var resultado = await AbrirAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
	}

	[Fact]
	public async Task Un_401_sin_cuerpo_se_trata_como_fallo_del_servicio()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.SinCuerpo(HttpStatusCode.Unauthorized));

		var resultado = await AbrirAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
		Assert.Equal("http.401", resultado.CodigoError);
	}

	// ---------- Cotejo de permisos contra el token (JTT-1379 CA 8) ----------

	[Fact]
	public async Task Acepta_los_permisos_que_el_token_respalda()
	{
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, LoginCorrecto));

		var resultado = await AbrirAsync(cliente);

		Assert.True(resultado.Exitoso);
		Assert.Equal(["APP_OPERADOR_MOVIL"], resultado.Sesion!.Permisos);
	}

	[Fact]
	public async Task Un_cuerpo_que_concede_mas_que_el_token_no_produce_sesion()
	{
		// El caso que el criterio persigue: alguien altera la lista de permisos de la
		// respuesta. El token sigue firmado con lo que Jacob concedió de verdad, así que la
		// diferencia se nota y el acceso no se completa.
		var cuerpo = CuerpoLogin(TokenConPermiso, """["APP_OPERADOR_MOVIL", "ADMINISTRAR"]""");
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, cuerpo));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
		Assert.Equal(MotivoRechazoAcceso.ErrorDelServicio, resultado.Motivo);
		Assert.Equal("respuesta.incompleta", resultado.CodigoError);
	}

	[Fact]
	public async Task Un_token_sin_el_claim_del_modulo_no_respalda_ningun_permiso()
	{
		var cuerpo = CuerpoLogin(TokenDePrueba.SinModulo(), """["APP_OPERADOR_MOVIL"]""");
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, cuerpo));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
	}

	[Fact]
	public async Task Un_token_ilegible_no_respalda_ningun_permiso()
	{
		// Ilegible no es lo mismo que ausente: bloquear es la salida correcta ante algo que
		// no se puede interpretar.
		var cuerpo = CuerpoLogin("no.es-un.jwt", """["APP_OPERADOR_MOVIL"]""");
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, cuerpo));

		var resultado = await AbrirAsync(cliente);

		Assert.False(resultado.Exitoso);
	}

	[Fact]
	public async Task Un_token_con_varios_modulos_respalda_el_permiso()
	{
		// El claim admite arreglo además de cadena. Hoy Jacob manda uno, pero el formato
		// permite varios y no debe romper el acceso el día que los mande.
		var token = TokenDePrueba.Con("APP_OPERADOR_MOVIL", "OTRO_MODULO");
		var cuerpo = CuerpoLogin(token, """["APP_OPERADOR_MOVIL"]""");
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, cuerpo));

		var resultado = await AbrirAsync(cliente);

		Assert.True(resultado.Exitoso);
	}

	[Fact]
	public async Task Una_sesion_sin_permisos_se_acepta_porque_no_concede_nada()
	{
		// No hay nada que respaldar. El acceso queda abierto pero sin capacidades, y qué se
		// habilita con ellas es de JTT-1385.
		var cuerpo = CuerpoLogin(TokenDePrueba.SinModulo(), "[]");
		var (cliente, _) = Construir(ManejadorHttpFalso.Json(HttpStatusCode.OK, cuerpo));

		var resultado = await AbrirAsync(cliente);

		Assert.True(resultado.Exitoso);
		Assert.Empty(resultado.Sesion!.Permisos);
	}

	// ---------- Red ----------

	[Fact]
	public async Task Sin_servidor_al_otro_lado_se_informa_falta_de_comunicacion()
	{
		var cliente = Nuevo(ManejadorHttpFalso.ConexionRechazada());

		var resultado = await AbrirAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.SinComunicacion, resultado.Motivo);
		Assert.Equal("conexion.fallida", resultado.CodigoError);
	}

	[Fact]
	public async Task Si_el_servidor_no_contesta_a_tiempo_se_informa_falta_de_comunicacion()
	{
		var cliente = Nuevo(ManejadorHttpFalso.TiempoAgotado());

		var resultado = await AbrirAsync(cliente);

		Assert.Equal(MotivoRechazoAcceso.SinComunicacion, resultado.Motivo);
		Assert.Equal("tiempo.agotado", resultado.CodigoError);
	}

	// ---------- Seguridad ----------

	[Fact]
	public async Task El_resultado_de_un_rechazo_no_arrastra_sesion_ni_token()
	{
		var (cliente, _) = Construir(ErrorFuncional("appoperador.desafio.expirado"));

		var resultado = await AbrirAsync(cliente);

		Assert.Null(resultado.Sesion);
		Assert.Equal("appoperador.desafio.expirado", resultado.CodigoError);
	}
}
