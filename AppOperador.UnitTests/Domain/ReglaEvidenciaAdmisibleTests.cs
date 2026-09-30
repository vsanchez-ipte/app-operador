using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Domain;

public sealed class ReglaEvidenciaAdmisibleTests
{
	private static readonly LimitesEvidencia Limites =
		new(["image/jpeg", "image/png", "image/bmp", "application/pdf"], 5, 3);

	private const long UnMegabyte = 1024 * 1024;

	[Fact]
	public void UnArchivoDentroDeTodosLosLimites_seAdmite()
	{
		Assert.True(ReglaEvidenciaAdmisible.EsAdmisible(
			Limites, "image/jpeg", bytes: UnMegabyte, yaAdjuntas: 0));
	}

	[Fact]
	public void SinLimitesDescargados_noSeAdmiteNada()
	{
		Assert.Equal(
			MotivoEvidenciaRechazada.LimitesDesconocidos,
			ReglaEvidenciaAdmisible.Comprobar(
				LimitesEvidencia.Desconocidos, "image/jpeg", UnMegabyte, 0));
	}

	[Fact]
	public void ConElCupoLleno_seDiceEsoYNoOtraCosa()
	{
		Assert.Equal(
			MotivoEvidenciaRechazada.CupoLleno,
			ReglaEvidenciaAdmisible.Comprobar(Limites, "image/jpeg", UnMegabyte, yaAdjuntas: 3));
	}

	[Fact]
	public void ElCupoSeComprueba_antesQueElFormato()
	{
		Assert.Equal(
			MotivoEvidenciaRechazada.CupoLleno,
			ReglaEvidenciaAdmisible.Comprobar(Limites, "video/mp4", UnMegabyte, yaAdjuntas: 3));
	}

	[Theory]
	[InlineData("video/mp4")]
	[InlineData("text/plain")]
	[InlineData("")]
	[InlineData(null)]
	public void UnFormatoQueElServidorNoAdmite_seRechaza(string? tipoMime)
	{
		Assert.Equal(
			MotivoEvidenciaRechazada.FormatoNoAdmitido,
			ReglaEvidenciaAdmisible.Comprobar(Limites, tipoMime, UnMegabyte, 0));
	}

	[Fact]
	public void ElFormatoNoDistingueMayusculas()
	{
		Assert.True(ReglaEvidenciaAdmisible.EsAdmisible(Limites, "IMAGE/JPEG", UnMegabyte, 0));
	}

	[Fact]
	public void ElTopeDeTamanoEsInclusivo()
	{
		Assert.True(ReglaEvidenciaAdmisible.EsAdmisible(
			Limites, "image/jpeg", bytes: 5 * UnMegabyte, yaAdjuntas: 0));

		Assert.Equal(
			MotivoEvidenciaRechazada.DemasiadoGrande,
			ReglaEvidenciaAdmisible.Comprobar(Limites, "image/jpeg", 5 * UnMegabyte + 1, 0));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void UnArchivoVacio_seRechaza(long bytes)
	{
		Assert.Equal(
			MotivoEvidenciaRechazada.ArchivoVacio,
			ReglaEvidenciaAdmisible.Comprobar(Limites, "image/jpeg", bytes, 0));
	}

	[Fact]
	public void LosLimitesDeJTT289_entranSinTocarLaRegla()
	{
		// Los límites vigentes: 8 archivos de 15 MB, con video.
		var deJtt289 = new LimitesEvidencia(
			["image/jpeg", "image/png", "image/bmp", "application/pdf", "video/mp4"], 15, 8);

		Assert.True(ReglaEvidenciaAdmisible.EsAdmisible(
			deJtt289, "video/mp4", bytes: 12 * UnMegabyte, yaAdjuntas: 7));

		Assert.Equal(
			MotivoEvidenciaRechazada.CupoLleno,
			ReglaEvidenciaAdmisible.Comprobar(deJtt289, "video/mp4", UnMegabyte, yaAdjuntas: 8));
	}

	// ── Si el servidor admite video ───────────────────────────────────────────────────

	[Fact]
	public void ElCatalogoDeHoyNoAdmiteVideo()
	{
		// Imagen y PDF, ningún video/*. Es lo que mantiene apagado el botón de grabar.
		var deHoy = new LimitesEvidencia(
			["image/jpeg", "image/png", "image/bmp", "application/pdf"], 5, 3);

		Assert.False(deHoy.AdmiteVideo);
	}

	[Fact]
	public void UnFormatoDeVideoEnElCatalogoLoAdmite()
	{
		Assert.True(new LimitesEvidencia(["image/jpeg", "video/mp4"], 15, 8).AdmiteVideo);
	}

	[Fact]
	public void LaCajaDelTipoNoImporta()
	{
		Assert.True(new LimitesEvidencia(["VIDEO/MP4"], 15, 8).AdmiteVideo);
	}

	[Fact]
	public void SinLimitesDescargados_noSeAsumeQueAdmiteVideo()
	{
		Assert.False(LimitesEvidencia.Desconocidos.AdmiteVideo);
	}
}
