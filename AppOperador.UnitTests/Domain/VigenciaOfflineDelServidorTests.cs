using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public class VigenciaOfflineDelServidorTests
{
	private static readonly DateTime Validacion = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void DelServidor_AdoptaLasDosFechasTalComoLlegan()
	{
		var hasta = Validacion.AddHours(8);

		var vigencia = VigenciaOffline.DelServidor(Validacion, hasta);

		Assert.Equal(Validacion, vigencia.LastValidatedAtUtc);
		Assert.Equal(hasta, vigencia.OfflineUntilUtc);
	}

	[Fact]
	public void DelServidor_NoRecalculaLaVentanaConLaDuracionDeLaApp()
	{
		var hasta = Validacion.AddHours(6);

		var vigencia = VigenciaOffline.DelServidor(Validacion, hasta);

		Assert.Equal(hasta, vigencia.OfflineUntilUtc);
		Assert.NotEqual(Validacion + VigenciaOffline.Duracion, vigencia.OfflineUntilUtc);
	}

	[Fact]
	public void DelServidor_ConUnaVentanaMasLargaQueLaDeLaApp_TambienLaRespeta()
	{
		var hasta = Validacion.AddHours(12);

		var vigencia = VigenciaOffline.DelServidor(Validacion, hasta);

		Assert.Equal(hasta, vigencia.OfflineUntilUtc);
	}

	[Fact]
	public void DelServidor_LaVigenciaSeEvaluaContraLaFechaDelServidor()
	{
		var vigencia = VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(6));

		Assert.True(vigencia.EstaVigenteEn(Validacion.AddHours(5)));
		Assert.False(vigencia.EstaVigenteEn(Validacion.AddHours(6)));
		Assert.False(vigencia.EstaVigenteEn(Validacion.AddHours(7)));
	}

	[Fact]
	public void DelServidor_ConVentanaDeDuracionCero_EsValidaPeroYaVencida()
	{
		var vigencia = VigenciaOffline.DelServidor(Validacion, Validacion);

		Assert.Equal(Validacion, vigencia.OfflineUntilUtc);
		Assert.False(vigencia.EstaVigenteEn(Validacion));
	}

	[Fact]
	public void DelServidor_ConLaVentanaTerminandoAntesDeLaValidacion_Lanza()
	{
		var excepcion = Assert.Throws<ArgumentException>(
			() => VigenciaOffline.DelServidor(Validacion, Validacion.AddSeconds(-1)));

		Assert.Contains("antes de la validación", excepcion.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void DelServidor_ConLaValidacionFueraDeUtc_Lanza(DateTimeKind kind)
	{
		var validacion = new DateTime(2026, 8, 10, 12, 0, 0, kind);

		Assert.Throws<ArgumentException>(
			() => VigenciaOffline.DelServidor(validacion, Validacion.AddHours(8)));
	}

	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void DelServidor_ConElFinDeVentanaFueraDeUtc_Lanza(DateTimeKind kind)
	{
		var hasta = new DateTime(2026, 8, 10, 20, 0, 0, kind);

		Assert.Throws<ArgumentException>(() => VigenciaOffline.DelServidor(Validacion, hasta));
	}

	[Fact]
	public void DelServidor_ConservaElKindUtcEnLasDosFechas()
	{
		var vigencia = VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8));

		Assert.Equal(DateTimeKind.Utc, vigencia.LastValidatedAtUtc.Kind);
		Assert.Equal(DateTimeKind.Utc, vigencia.OfflineUntilUtc.Kind);
	}

	[Fact]
	public void Validada_SigueCalculandoOchoHoras_ParaElCaminoSimulado()
	{
		// La via anterior no cambia: el simulador y las reglas locales la siguen usando.
		var vigencia = VigenciaOffline.Validada(Validacion);

		Assert.Equal(Validacion.AddHours(8), vigencia.OfflineUntilUtc);
	}
}
