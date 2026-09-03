using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Lo que la pantalla puede hacer con las evidencias de una incidencia (JTT-1398 CA 1).
/// </summary>
/// <remarks>
/// Este archivo existe por lo que pasó el 26 de agosto: la pantalla de la Cola se contradijo dos
/// veces en un día porque sus decisiones vivían en el ViewModel, donde no llega ninguna prueba.
/// Aquí las decisiones están en Aplicación y por eso se pueden fijar.
/// </remarks>
public sealed class ResumenEvidenciasTests
{
	private static readonly LimitesEvidencia Limites =
		new(["image/jpeg"], TamanoMaximoMb: 5, MaximoArchivosPorIncidencia: 3);

	private static EvidenciaAdjunta Evidencia(string uuid, long bytes = 1000) =>
		new(uuid, "inc-1", $"{uuid}.jpg", "image/jpeg", bytes, $"/privado/{uuid}.jpg",
			EstadoSincronizacion.Pendiente);

	[Fact]
	public void SinEvidencias_seAdmitenTodasLasDelCupo()
	{
		var resumen = new ResumenEvidencias([], Limites);

		Assert.Equal(0, resumen.Cuantas);
		Assert.Equal(3, resumen.Faltantes);
		Assert.True(resumen.PuedeAdjuntar);
	}

	[Fact]
	public void ConElCupoLleno_seApagaElBoton()
	{
		// Lo que evita mandar al operador a la galería, esperar la copia y leerle un rechazo
		// que se sabía desde antes de abrirla.
		var resumen = new ResumenEvidencias(
			[Evidencia("a"), Evidencia("b"), Evidencia("c")], Limites);

		Assert.Equal(0, resumen.Faltantes);
		Assert.False(resumen.PuedeAdjuntar);
		Assert.Equal(MotivoEvidenciaRechazada.CupoLleno, resumen.MotivoParaNoAdjuntar);
	}

	[Fact]
	public void SinLimitesDescargados_noSeAdjuntaYSeDiceQueEsOtraCosa()
	{
		// Distinguirlo del cupo lleno importa: decirle «ya no caben más» a quien nunca ha
		// conectado la app lo manda a borrar archivos que no existen.
		var resumen = new ResumenEvidencias([], LimitesEvidencia.Desconocidos);

		Assert.False(resumen.PuedeAdjuntar);
		Assert.Equal(MotivoEvidenciaRechazada.LimitesDesconocidos, resumen.MotivoParaNoAdjuntar);
	}

	[Fact]
	public void SinLimites_faltantesEsCeroYNoInfinito()
	{
		// No saber el tope no es tener tope infinito.
		Assert.Equal(0, new ResumenEvidencias([], LimitesEvidencia.Desconocidos).Faltantes);
	}

	[Fact]
	public void SiElServidorBajaElCupo_faltantesNoSeVaANegativo()
	{
		// Pasa de verdad: el operador adjuntó tres, el servidor baja el máximo a dos y la
		// siguiente descarga del catálogo lo trae. Un número negativo en pantalla es un defecto.
		var masEstrictos = Limites with { MaximoArchivosPorIncidencia = 2 };

		var resumen = new ResumenEvidencias(
			[Evidencia("a"), Evidencia("b"), Evidencia("c")], masEstrictos);

		Assert.Equal(0, resumen.Faltantes);
		Assert.False(resumen.PuedeAdjuntar);
	}

	[Fact]
	public void LoQueOcupanSeSuma()
	{
		var resumen = new ResumenEvidencias(
			[Evidencia("a", 1000), Evidencia("b", 2500)], Limites);

		Assert.Equal(3500, resumen.BytesTotales);
	}

	[Fact]
	public void LosOchoArchivosDeJTT289_entranSinTocarNada()
	{
		var deJtt289 = new LimitesEvidencia(["image/jpeg", "video/mp4"], 15, 8);

		var resumen = new ResumenEvidencias([Evidencia("a"), Evidencia("b")], deJtt289);

		Assert.Equal(6, resumen.Faltantes);
		Assert.True(resumen.PuedeAdjuntar);
	}

	// ── El video se habilita desde el catálogo ────────────────────────────────────────

	[Fact]
	public void HoyNoSePuedeGrabarVideo_porqueElServidorNoLoAdmite()
	{
		// El catálogo publica imagen y PDF, ningún video/*. El botón sale apagado aunque
		// quepan más archivos: grabar quince megabytes para que los rechace el formato es
		// justo lo que se evita.
		var resumen = new ResumenEvidencias([], Limites);

		Assert.True(resumen.PuedeAdjuntar);
		Assert.False(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.FormatoNoAdmitido, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void ElDiaQueElCatalogoPubliqueVideo_seEnciendeSolo()
	{
		// Es la comprobación de que JTT-289 no deja trabajo de app: el servidor declara un
		// video/* y el botón se enciende sin recompilar ni publicar en las tiendas.
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };

		var resumen = new ResumenEvidencias([], conVideo);

		Assert.True(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.Ninguno, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void ConElCupoLleno_elVideoDiceQueElCupoEstaLleno_noQueFaltaElFormato()
	{
		// Con las dos causas presentes manda la del cupo: mandar a habilitar el video cuando
		// lo que hay que hacer es quitar un archivo deja al operador sin salida.
		var conVideo = Limites with { FormatosPermitidos = ["video/mp4"] };

		var resumen = new ResumenEvidencias(
			[Evidencia("a"), Evidencia("b"), Evidencia("c")], conVideo);

		Assert.False(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.CupoLleno, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void SinCatalogo_elVideoNoCulpaAlFormato()
	{
		// Sin límites descargados no se sabe si admite video: la causa es que no hay catálogo.
		var resumen = new ResumenEvidencias([], LimitesEvidencia.Desconocidos);

		Assert.False(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.LimitesDesconocidos, resumen.MotivoParaNoAdjuntarVideo);
	}
}
