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
}
