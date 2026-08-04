using AppOperador.Domain.Enums;

namespace AppOperador.UnitTests.Domain;

public class KilometerSourceTests
{
	[Fact]
	public void KilometerSource_TieneExactamenteLosDosValoresDelDominio()
	{
		var valores = Enum.GetNames<KilometerSource>().OrderBy(n => n, StringComparer.Ordinal).ToArray();

		Assert.Equal(["GPS", "Manual"], valores);
	}

	[Theory]
	[InlineData(KilometerSource.GPS)]
	[InlineData(KilometerSource.Manual)]
	public void CadaValor_EstaDefinidoEnLaEnumeracion(KilometerSource origen)
	{
		Assert.True(Enum.IsDefined(origen));
	}

	[Fact]
	public void ValorPorDefecto_NoEsUnOrigenValido()
	{
		// Ningún valor vale 0 a propósito: un default(KilometerSource) sin asignar
		// tiene que ser detectable como dato incompleto.
		Assert.False(Enum.IsDefined(default(KilometerSource)));
	}
}
