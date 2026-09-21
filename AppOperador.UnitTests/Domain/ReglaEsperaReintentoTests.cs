using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// Espera creciente entre reintentos (JTT-1401 CA 7).
/// </summary>
public sealed class ReglaEsperaReintentoTests
{
	[Theory]
	[InlineData(0, 1)]
	[InlineData(1, 1)]
	[InlineData(2, 2)]
	[InlineData(3, 4)]
	[InlineData(4, 8)]
	[InlineData(5, 16)]
	public void LaEsperaSeDuplicaEnCadaFallo(int intentos, int minutosEsperados)
	{
		Assert.Equal(TimeSpan.FromMinutes(minutosEsperados), ReglaEsperaReintento.Para(intentos));
	}

	[Theory]
	[InlineData(6)]
	[InlineData(12)]
	[InlineData(400)]
	public void LaEsperaSeDetieneEnElTope(int intentos)
	{
		// Sin tope, un turno entero sin cobertura dejaría la siguiente espera en más de un día
		// y lo capturado no saldría hasta la jornada siguiente.
		Assert.Equal(ReglaEsperaReintento.EsperaMaxima, ReglaEsperaReintento.Para(intentos));
	}

	[Fact]
	public void UnaRachaLarguisimaNoDesbordaNiDaEsperaNegativa()
	{
		// El desplazamiento de bits desbordaría con intentos altos si no se cortara antes.
		var espera = ReglaEsperaReintento.Para(int.MaxValue);

		Assert.Equal(ReglaEsperaReintento.EsperaMaxima, espera);
		Assert.True(espera > TimeSpan.Zero);
	}

	[Fact]
	public void NoSeReintentaAntesDeQueVenzaLaEspera()
	{
		Assert.False(ReglaEsperaReintento.YaPuedeReintentarse(3, TimeSpan.FromMinutes(3)));
	}

	[Fact]
	public void SeReintentaAlVencerLaEspera()
	{
		// Justo en el límite ya cuenta: esperar un tick más no aporta nada y complica la prueba.
		Assert.True(ReglaEsperaReintento.YaPuedeReintentarse(3, TimeSpan.FromMinutes(4)));
	}

	[Fact]
	public void UnRegistroQueNuncaSeHaIntentadoEsperaLoMinimo()
	{
		Assert.Equal(ReglaEsperaReintento.EsperaInicial, ReglaEsperaReintento.Para(0));
	}
}
