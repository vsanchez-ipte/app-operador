using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// La ventana offline cuando la calcula Jacob CCO y la app solo la adopta (JTT-1382 CA 3 y CA 4).
/// </summary>
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
		// El corazón del CA: si el servidor dijera seis horas, la app respeta seis. Volver a
		// sumar ocho sobre el reloj del telefono daria una vigencia que el servidor no
		// reconoce, y el operador seguiria capturando creyendose dentro de plazo.
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
		// El servidor manda. Una ventana nula es rara, pero no es incoherente: significa que
		// no hay margen offline, y eso la app lo tiene que poder representar.
		var vigencia = VigenciaOffline.DelServidor(Validacion, Validacion);

		Assert.Equal(Validacion, vigencia.OfflineUntilUtc);
		Assert.False(vigencia.EstaVigenteEn(Validacion));
	}

	[Fact]
	public void DelServidor_ConLaVentanaTerminandoAntesDeLaValidacion_Lanza()
	{
		// Solo puede ser un error de integracion. Aceptarlo daria una sesion nacida vencida
		// sin que nadie se entere.
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
