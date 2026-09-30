using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public sealed class ReglaEspacioParaEvidenciaTests
{
	private const long Megabyte = 1024 * 1024;

	[Fact]
	public void ExigeElMargenAdemasDelArchivo()
	{
		var libres = ReglaEspacioParaEvidencia.MargenSeguridadBytes + 10 * Megabyte;

		Assert.True(ReglaEspacioParaEvidencia.Cabe(libres, 10 * Megabyte));
		Assert.False(ReglaEspacioParaEvidencia.Cabe(libres, 10 * Megabyte + 1));
	}

	[Fact]
	public void ConEspacioDesconocido_cabe()
	{
		Assert.True(ReglaEspacioParaEvidencia.Cabe(null, 15 * Megabyte));
	}

	[Fact]
	public void ConDiscoLleno_noCabeNiUnByte()
	{
		Assert.False(ReglaEspacioParaEvidencia.Cabe(0, 1));
	}
}
