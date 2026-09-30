using System.Net;
using System.Text;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

public sealed class ClienteCatalogosJacobLimitesTests
{
	private static string RespuestaCon(string? bloqueLimites) =>
		$$"""
		{
		  "resultado": {
		    "version": "2026-08-27",
		    "tipos": [ { "id": 11, "nombre": "Choque por alcance", "exigeDescripcion": false } ],
		    "severidades": [
		      { "id": "11111111-1111-1111-1111-111111111111", "nivel": "Crítico", "orden": 1,
		        "hexadecimal": "#EB1409" }
		    ],
		    "afectaciones": [ { "id": 1, "nombre": "Total" } ],
		    "cuerpos": [ { "clave": "A", "nombre": "Cuerpo A" } ]{{(bloqueLimites is null ? "" : "," + bloqueLimites)}}
		  },
		  "codigoError": null,
		  "mensajeError": null
		}
		""";

	private static ClienteCatalogosJacob Crear(string cuerpo) =>
		new(
			new HttpClient(ManejadorHttpFalso.Siempre(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
			})),
			new ConfiguracionApi { UrlBase = "http://localhost:5231" });

	[Fact]
	public async Task LosLimitesCompletos_seLeenTalCualLosDeclaraElServidor()
	{
		var cliente = Crear(RespuestaCon("""
			"limitesEvidencia": {
			  "formatosPermitidos": ["image/jpeg", "image/png", "image/bmp", "application/pdf"],
			  "tamanoMaximoMb": 5,
			  "maximoArchivosPorIncidencia": 3
			}
			"""));

		var catalogo = await cliente.ObtenerVigentesAsync(TokenDePrueba.Con("APP_OPERADOR_MOVIL"));

		var limites = catalogo!.LimitesEvidencia;
		Assert.True(limites.EstanDefinidos);
		Assert.Equal(5, limites.TamanoMaximoMb);
		Assert.Equal(5L * 1024 * 1024, limites.TamanoMaximoBytes);
		Assert.Equal(3, limites.MaximoArchivosPorIncidencia);
		Assert.True(limites.AdmiteFormato("application/pdf"));
		Assert.False(limites.AdmiteFormato("video/mp4"));
	}

	[Fact]
	public async Task SinBloqueDeLimites_elCatalogoSigueSirviendoParaCapturar()
	{
		var cliente = Crear(RespuestaCon(bloqueLimites: null));

		var catalogo = await cliente.ObtenerVigentesAsync(TokenDePrueba.Con("APP_OPERADOR_MOVIL"));

		Assert.NotNull(catalogo);
		Assert.True(catalogo.EsUtilizable);
		Assert.False(catalogo.LimitesEvidencia.EstanDefinidos);
	}

	[Theory]
	// Sin formatos no se sabe qué aceptar.
	[InlineData("""
		"limitesEvidencia": { "formatosPermitidos": [], "tamanoMaximoMb": 5,
		  "maximoArchivosPorIncidencia": 3 }
		""")]
	// Sin tope de tamaño, la app tendría que inventarse uno.
	[InlineData("""
		"limitesEvidencia": { "formatosPermitidos": ["image/jpeg"], "tamanoMaximoMb": 0,
		  "maximoArchivosPorIncidencia": 3 }
		""")]
	// Sin tope de cantidad, el operador descubriría el rechazo después de subir.
	[InlineData("""
		"limitesEvidencia": { "formatosPermitidos": ["image/jpeg"], "tamanoMaximoMb": 5 }
		""")]
	public async Task UnosLimitesAMedias_seTratanComoAusentes(string bloque)
	{
		var catalogo = await Crear(RespuestaCon(bloque)).ObtenerVigentesAsync(TokenDePrueba.Con("APP_OPERADOR_MOVIL"));

		Assert.False(catalogo!.LimitesEvidencia.EstanDefinidos);
	}

	[Fact]
	public async Task ElFormatoSeCompara_sinDistinguirMayusculas()
	{
		var cliente = Crear(RespuestaCon("""
			"limitesEvidencia": {
			  "formatosPermitidos": ["IMAGE/JPEG"],
			  "tamanoMaximoMb": 5,
			  "maximoArchivosPorIncidencia": 3
			}
			"""));

		var catalogo = await cliente.ObtenerVigentesAsync(TokenDePrueba.Con("APP_OPERADOR_MOVIL"));

		Assert.True(catalogo!.LimitesEvidencia.AdmiteFormato("image/jpeg"));
	}
}
