using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// Qué archivo puede adjuntarse a una incidencia (JTT-1398 CA 6, 7 y 8).
/// </summary>
/// <remarks>
/// La regla vive en el dominio y no en la pantalla justamente para que exista este archivo: lo
/// que decide si el operador puede documentar un hecho no puede quedar donde el proyecto de
/// pruebas no llega.
/// </remarks>
public sealed class ReglaEvidenciaAdmisibleTests
{
	/// <summary>Los que publica el servidor hoy. Cambiarán, y por eso se leen del catálogo.</summary>
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
		// No es «sin límite»: es que no se sabe con qué validar. Adjuntar a ciegas produciría un
		// rechazo al sincronizar, cuando el operador ya no está frente al hecho.
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
		// Decirle «el formato no sirve» a quien ya llenó el cupo lo manda a buscar otro archivo
		// que tampoco va a entrar. El orden de las comprobaciones es parte de la regla.
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
		// Exactamente el máximo entra. El servidor valida con el mismo número, y dejar fuera el
		// borde produciría un archivo que la app rechaza y el CCO habría aceptado.
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
		// Una captura que salió mal. Gastaría una plaza del cupo para no mostrar nada, y el
		// operador creería que documentó el hecho.
		Assert.Equal(
			MotivoEvidenciaRechazada.ArchivoVacio,
			ReglaEvidenciaAdmisible.Comprobar(Limites, "image/jpeg", bytes, 0));
	}

	[Fact]
	public void LosLimitesDeJTT289_entranSinTocarLaRegla()
	{
		// El PO fijó 8 archivos de 15 MB con video el 15-ago. Es la comprobación de que subirlos
		// será cambiar dos números del lado del servidor y nada más.
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
		// El tipo lo determina el servidor por contenido y no hay garantía de con qué caja lo
		// escriba, igual que en AdmiteFormato.
		Assert.True(new LimitesEvidencia(["VIDEO/MP4"], 15, 8).AdmiteVideo);
	}

	[Fact]
	public void SinLimitesDescargados_noSeAsumeQueAdmiteVideo()
	{
		Assert.False(LimitesEvidencia.Desconocidos.AdmiteVideo);
	}
}
