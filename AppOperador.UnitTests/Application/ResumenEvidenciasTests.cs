using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

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
		// Evita mandar al operador a la galería por un rechazo ya sabido.
		var resumen = new ResumenEvidencias(
			[Evidencia("a"), Evidencia("b"), Evidencia("c")], Limites);

		Assert.Equal(0, resumen.Faltantes);
		Assert.False(resumen.PuedeAdjuntar);
		Assert.Equal(MotivoEvidenciaRechazada.CupoLleno, resumen.MotivoParaNoAdjuntar);
	}

	[Fact]
	public void SinLimitesDescargados_noSeAdjuntaYSeDiceQueEsOtraCosa()
	{
		// Distinto del cupo lleno: a quien nunca ha conectado no se le manda a borrar archivos.
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
		// El servidor puede bajar el máximo por debajo de lo ya adjunto; un negativo en pantalla es un defecto.
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
		// Sin video/* en el catálogo el botón sale apagado aunque quepan más archivos.
		var resumen = new ResumenEvidencias([], Limites);

		Assert.True(resumen.PuedeAdjuntar);
		Assert.False(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.FormatoNoAdmitido, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void ElDiaQueElCatalogoPubliqueVideo_seEnciendeSolo()
	{
		// El servidor declara un video/* y el botón se enciende sin publicar otra versión.
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };

		var resumen = new ResumenEvidencias([], conVideo);

		Assert.True(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.Ninguno, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void ConElCupoLleno_elVideoDiceQueElCupoEstaLleno_noQueFaltaElFormato()
	{
		// Con las dos causas manda la del cupo: es la que el operador puede resolver.
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

	// ── El video se bloquea sin espacio ──────────────────────────────────────────────

	[Fact]
	public void SinEspacioParaUnVideoDelTamanoMaximo_seApagaElBotonYSeDicePorQue()
	{
		// Antes de grabar y con el máximo: el tamaño real no existe hasta que termina.
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };
		var apenas = ReglaEspacioParaEvidencia.MargenSeguridadBytes + conVideo.TamanoMaximoBytes - 1;

		var resumen = new ResumenEvidencias([], conVideo, BytesLibres: apenas);

		Assert.True(resumen.PuedeAdjuntar);
		Assert.False(resumen.HayEspacioParaVideo);
		Assert.False(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.SinEspacio, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void ConEspacioParaUnVideoDelTamanoMaximo_seEnciende()
	{
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };
		var justo = ReglaEspacioParaEvidencia.MargenSeguridadBytes + conVideo.TamanoMaximoBytes;

		var resumen = new ResumenEvidencias([], conVideo, BytesLibres: justo);

		Assert.True(resumen.PuedeAdjuntarVideo);
		Assert.Equal(MotivoEvidenciaRechazada.Ninguno, resumen.MotivoParaNoAdjuntarVideo);
	}

	[Fact]
	public void ConEspacioDesconocido_elVideoNoSeBloquea()
	{
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };

		var resumen = new ResumenEvidencias([], conVideo, BytesLibres: null);

		Assert.True(resumen.PuedeAdjuntarVideo);
	}

	// ── El tope con el que se graba, para no perder lo grabado ───────────────────────

	[Fact]
	public void SeGrabaConElTopeDelCatalogo()
	{
		// Sin tope, el video se pierde entero al pasarse; se pide antes, como el espacio.
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };

		var resumen = new ResumenEvidencias([], conVideo);

		Assert.Equal(conVideo.TamanoMaximoBytes, resumen.TopeParaGrabarVideo);
	}

	[Fact]
	public void SinLimitesDescargados_noSePideNingunTope()
	{
		// Cero es «no pidas tope», no «corta en cero».
		var resumen = new ResumenEvidencias([], LimitesEvidencia.Desconocidos);

		Assert.Equal(0, resumen.TopeParaGrabarVideo);
	}

	[Fact]
	public void ConElCupoLleno_noSePideTopePorqueNoSeVaAGrabar()
	{
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };
		var llenas = new[] { Evidencia("e1"), Evidencia("e2"), Evidencia("e3") };

		var resumen = new ResumenEvidencias(llenas, conVideo);

		Assert.False(resumen.PuedeAdjuntarVideo);
		Assert.Equal(0, resumen.TopeParaGrabarVideo);
	}

	[Fact]
	public void SinEspacioParaVideo_noSePideTope()
	{
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };
		var apenas = ReglaEspacioParaEvidencia.MargenSeguridadBytes + conVideo.TamanoMaximoBytes - 1;

		var resumen = new ResumenEvidencias([], conVideo, BytesLibres: apenas);

		Assert.Equal(0, resumen.TopeParaGrabarVideo);
	}

	[Fact]
	public void SinEspacio_laFotografiaSigueDisponible()
	{
		// Sin espacio para video se sigue con texto o foto; la foto se comprueba con su tamaño real.
		var conVideo = Limites with { FormatosPermitidos = ["image/jpeg", "video/mp4"] };

		var resumen = new ResumenEvidencias([], conVideo, BytesLibres: 0);

		Assert.True(resumen.PuedeAdjuntar);
		Assert.False(resumen.PuedeAdjuntarVideo);
	}
}
