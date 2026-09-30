using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public sealed class ReglaNotaIncidenciaTests
{
	[Fact]
	public void SiElTipoNoExigeDescripcion_laNotaVaciaBasta()
	{
		Assert.True(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: false, nota: ""));
	}

	[Fact]
	public void SiElTipoExigeDescripcion_unaNotaCortaNoBasta()
	{
		Assert.False(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: true, nota: "corta"));
	}

	[Fact]
	public void SiElTipoExigeDescripcion_ochoCaracteresBastan()
	{
		// El mínimo es inclusivo, y el backend revalida con el mismo número.
		Assert.True(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: true, nota: "12345678"));
	}

	[Fact]
	public void LosEspaciosNoCuentanParaElMinimo()
	{
		Assert.False(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: true, nota: "        "));
	}

	[Fact]
	public void UnaNotaNulaSeTrataComoVacia()
	{
		Assert.False(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: true, nota: null));
		Assert.True(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: false, nota: null));
	}

	[Fact]
	public void PasarseDelTopeNoEsValidoAunqueElTipoNoExijaDescripcion()
	{
		var demasiado = new string('a', ReglaNotaIncidencia.MaximoCaracteres + 1);

		Assert.False(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: false, nota: demasiado));
	}

	[Fact]
	public void ElTopeExactoSiEsValido()
	{
		var justo = new string('a', ReglaNotaIncidencia.MaximoCaracteres);

		Assert.True(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: true, nota: justo));
	}
}
