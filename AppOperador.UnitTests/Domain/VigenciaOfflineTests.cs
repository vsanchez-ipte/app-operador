using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public class VigenciaOfflineTests
{
	// Instante fijo: ninguna prueba consulta el reloj del sistema.
	private static readonly DateTime Login = new(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Validada_CalculaOfflineUntilUtcComoOchoHorasDespues()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.Equal(Login, vigencia.LastValidatedAtUtc);
		Assert.Equal(new DateTime(2026, 8, 3, 18, 0, 0, DateTimeKind.Utc), vigencia.OfflineUntilUtc);
		Assert.Equal(TimeSpan.FromHours(8), vigencia.OfflineUntilUtc - vigencia.LastValidatedAtUtc);
	}

	[Fact]
	public void Duracion_EsDeOchoHoras()
	{
		Assert.Equal(TimeSpan.FromHours(8), VigenciaOffline.Duracion);
	}

	[Fact]
	public void OfflineUntilUtc_SeMantieneEnUtc()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.Equal(DateTimeKind.Utc, vigencia.LastValidatedAtUtc.Kind);
		Assert.Equal(DateTimeKind.Utc, vigencia.OfflineUntilUtc.Kind);
	}

	[Fact]
	public void EstaVigenteEn_JustoDespuesDelLogin_EsVerdadero()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.True(vigencia.EstaVigenteEn(Login));
		Assert.True(vigencia.EstaVigenteEn(Login.AddMinutes(1)));
	}

	[Fact]
	public void EstaVigenteEn_UnInstanteAntesDelLimite_EsVerdadero()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.True(vigencia.EstaVigenteEn(vigencia.OfflineUntilUtc.AddTicks(-1)));
	}

	[Fact]
	public void EstaVigenteEn_ExactamenteEnElLimite_EsFalso()
	{
		// Límite excluyente: justo en OfflineUntilUtc la ventana ya venció.
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.False(vigencia.EstaVigenteEn(vigencia.OfflineUntilUtc));
	}

	[Fact]
	public void EstaVigenteEn_DespuesDelLimite_EsFalso()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.False(vigencia.EstaVigenteEn(vigencia.OfflineUntilUtc.AddTicks(1)));
		Assert.False(vigencia.EstaVigenteEn(Login.AddHours(9)));
	}

	[Fact]
	public void RenovarConExito_DesplazaLasDosFechas()
	{
		var vigencia = VigenciaOffline.Validada(Login);
		var refresh = Login.AddHours(3);

		var renovada = vigencia.RenovarConExito(refresh);

		Assert.Equal(refresh, renovada.LastValidatedAtUtc);
		Assert.Equal(refresh.AddHours(8), renovada.OfflineUntilUtc);
	}

	[Fact]
	public void RenovarConExito_NoMutaLaVigenciaOriginal()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		vigencia.RenovarConExito(Login.AddHours(3));

		Assert.Equal(Login, vigencia.LastValidatedAtUtc);
		Assert.Equal(Login.AddHours(8), vigencia.OfflineUntilUtc);
	}

	[Fact]
	public void RenovarConFallo_NoModificaNingunaFecha()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		var trasElFallo = vigencia.RenovarConFallo();

		Assert.Equal(vigencia.LastValidatedAtUtc, trasElFallo.LastValidatedAtUtc);
		Assert.Equal(vigencia.OfflineUntilUtc, trasElFallo.OfflineUntilUtc);
	}

	[Fact]
	public void RefreshFallidoTrasUnoExitoso_ConservaLaVentanaDelExitoso()
	{
		var vigencia = VigenciaOffline.Validada(Login)
			.RenovarConExito(Login.AddHours(3))
			.RenovarConFallo();

		Assert.Equal(Login.AddHours(3), vigencia.LastValidatedAtUtc);
		Assert.Equal(Login.AddHours(11), vigencia.OfflineUntilUtc);
	}

	[Fact]
	public void RefreshFallidoNoProlongaLaVentana_AunqueSeIntenteMasTarde()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		var trasElFallo = vigencia.RenovarConFallo();

		// Pasadas 8 horas sigue vencida: el fallo no compró tiempo extra.
		Assert.False(trasElFallo.EstaVigenteEn(Login.AddHours(8)));
	}

	[Fact]
	public void RestanteEn_DevuelveLoQueQuedaDeVentana()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.Equal(TimeSpan.FromHours(8), vigencia.RestanteEn(Login));
		Assert.Equal(TimeSpan.FromHours(5), vigencia.RestanteEn(Login.AddHours(3)));
	}

	[Fact]
	public void RestanteEn_UnaVezVencida_EsCero()
	{
		var vigencia = VigenciaOffline.Validada(Login);

		Assert.Equal(TimeSpan.Zero, vigencia.RestanteEn(vigencia.OfflineUntilUtc));
		Assert.Equal(TimeSpan.Zero, vigencia.RestanteEn(Login.AddHours(20)));
	}

	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void Validada_ConInstanteQueNoEsUtc_LanzaArgumentException(DateTimeKind kind)
	{
		var instante = new DateTime(2026, 8, 3, 10, 0, 0, kind);

		var excepcion = Assert.Throws<ArgumentException>(() => VigenciaOffline.Validada(instante));

		Assert.Contains("UTC", excepcion.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void EstaVigenteEn_ConInstanteQueNoEsUtc_LanzaArgumentException(DateTimeKind kind)
	{
		var vigencia = VigenciaOffline.Validada(Login);
		var instante = new DateTime(2026, 8, 3, 12, 0, 0, kind);

		Assert.Throws<ArgumentException>(() => vigencia.EstaVigenteEn(instante));
	}

	[Theory]
	[InlineData(DateTimeKind.Local)]
	[InlineData(DateTimeKind.Unspecified)]
	public void RenovarConExito_ConInstanteQueNoEsUtc_LanzaArgumentException(DateTimeKind kind)
	{
		var vigencia = VigenciaOffline.Validada(Login);
		var instante = new DateTime(2026, 8, 3, 12, 0, 0, kind);

		Assert.Throws<ArgumentException>(() => vigencia.RenovarConExito(instante));
	}

	[Fact]
	public void LaVentana_CruzaElCambioDeDiaSinPerderPrecision()
	{
		var loginNocturno = new DateTime(2026, 8, 3, 20, 30, 0, DateTimeKind.Utc);

		var vigencia = VigenciaOffline.Validada(loginNocturno);

		Assert.Equal(new DateTime(2026, 8, 4, 4, 30, 0, DateTimeKind.Utc), vigencia.OfflineUntilUtc);
		Assert.True(vigencia.EstaVigenteEn(new DateTime(2026, 8, 4, 4, 29, 59, DateTimeKind.Utc)));
	}
}
