using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// Regla de la nota obligatoria (JTT-1397), extraída del ViewModel en JTT-1399.
/// </summary>
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
		// El mínimo es inclusivo, y el backend revalida con el mismo número (JTT-1397 CA 5).
		Assert.True(ReglaNotaIncidencia.EsSuficiente(tipoExigeDescripcion: true, nota: "12345678"));
	}

	[Fact]
	public void LosEspaciosNoCuentanParaElMinimo()
	{
		// Ocho espacios no son una descripción, y sin recortar pasarían la comprobación de
		// longitud dejando llegar al CCO una incidencia sin explicar.
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
