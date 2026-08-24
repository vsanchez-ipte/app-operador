using System.Net;
using System.Text;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Lectura de las respuestas del endpoint de creación de incidencias (JTT-1401).
/// </summary>
/// <remarks>
/// <b>Estas pruebas nacen de un defecto real</b>, encontrado en el emulador el 21-ago: el API
/// creaba la incidencia con su folio y respondía 200, y la app la marcaba fallida. El contrato
/// afirmaba que el éxito llegaba sin sobre y llega envuelto.
/// </remarks>
public sealed class ClienteIncidenciasJacobTests
{
	private static readonly EnvioIncidencia Envio = new(
		Uuid: "11111111-1111-1111-1111-111111111111",
		IdTipoIncidencia: 11,
		IdGravedad: Guid.NewGuid(),
		IdAfectacion: 1,
		Km: 138.300m,
		FuenteKilometro: "GPS",
		Cuerpo: "A",
		Nota: "nota de prueba",
		FchCapturaCampo: new DateTime(2026, 8, 21, 19, 19, 0, DateTimeKind.Utc),
		IdSesionOrigen: Guid.NewGuid());

	private static ClienteIncidenciasJacob Crear(HttpStatusCode codigo, string cuerpo) =>
		new(
			new HttpClient(ManejadorHttpFalso.Siempre(new HttpResponseMessage(codigo)
			{
				Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
			})),
			new ConfiguracionApi { UrlBase = "http://localhost:5231" });

	[Fact]
	public async Task LeeElFolioAunqueLaRespuestaVengaEnvuelta()
	{
		// Es el cuerpo exacto que devolvió el API el 21-ago, con el Envelope de BaseController.
		const string Cuerpo = """
			{"resultado":{"folio":"INC-APK-2026-0001",
			"fchCapturaCampo":"2026-08-21T19:19:00Z",
			"fchRecepcionCentral":"2026-08-21T19:19:22Z",
			"yaExistia":false},"codigoError":null,"mensajeError":null}
			""";

		var resultado = await Crear(HttpStatusCode.OK, Cuerpo).RegistrarAsync(Envio, "token");

		Assert.True(resultado.Exito);
		Assert.Equal("INC-APK-2026-0001", resultado.Registrada!.Folio);
	}

	[Fact]
	public async Task LeeElFolioTambienSiAlgunDiaLlegaSinSobre()
	{
		// El contrato describe esta forma. Se acepta para que alinearlo no rompa la app.
		const string Cuerpo = """
			{"folio":"INC-APK-2026-0002","fchRecepcionCentral":"2026-08-21T19:19:22Z","yaExistia":true}
			""";

		var resultado = await Crear(HttpStatusCode.OK, Cuerpo).RegistrarAsync(Envio, "token");

		Assert.True(resultado.Exito);
		Assert.Equal("INC-APK-2026-0002", resultado.Registrada!.Folio);
		Assert.True(resultado.Registrada.YaExistia);
	}

	[Fact]
	public async Task Un200SinFolioSeTrataComoFalloTecnico()
	{
		// Sin folio la incidencia quedaría marcada como sincronizada y sin la referencia que el
		// operador necesita para dictarla por radio.
		var resultado = await Crear(HttpStatusCode.OK, """{"resultado":null}""")
			.RegistrarAsync(Envio, "token");

		Assert.False(resultado.Exito);
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
	}

	[Fact]
	public async Task UnRechazoConservaElCodigoYElMensajeDeJacob()
	{
		const string Cuerpo = """
			{"resultado":null,"codigoError":"appincidencias.km.fueradecorredor",
			"mensajeError":"El kilómetro 119.999 está fuera del corredor (120.000 a 148.000)."}
			""";

		var resultado = await Crear(HttpStatusCode.BadRequest, Cuerpo).RegistrarAsync(Envio, "token");

		Assert.False(resultado.Exito);
		Assert.Equal(FamiliaErrorSincronizacion.Funcional, resultado.Familia);
		Assert.Equal("appincidencias.km.fueradecorredor", resultado.Codigo);
		Assert.Contains("fuera del corredor", resultado.Mensaje);
	}

	[Fact]
	public async Task SinTokenNoSeLlamaAlServidor()
	{
		var resultado = await Crear(HttpStatusCode.OK, "{}").RegistrarAsync(Envio, "  ");

		Assert.False(resultado.Exito);
		Assert.Equal(FamiliaErrorSincronizacion.Tecnico, resultado.Familia);
	}
}
