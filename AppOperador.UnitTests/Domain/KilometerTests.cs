using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Domain;

public class KilometerTests
{
	[Theory]
	[InlineData("000+000", 0, 0)]
	[InlineData("012+345", 12, 345)]
	[InlineData("999+999", 999, 999)]
	[InlineData("100+000", 100, 0)]
	[InlineData("000+999", 0, 999)]
	public void Crear_ConFormaCanonica_ConstruyeYDescomponeElValor(string entrada, int kilometros, int metros)
	{
		var kilometro = Kilometer.Crear(entrada);

		Assert.Equal(entrada, kilometro.Valor);
		Assert.Equal(kilometros, kilometro.Kilometros);
		Assert.Equal(metros, kilometro.Metros);
	}

	[Theory]
	// Longitud incorrecta en alguna de las dos partes
	[InlineData("12+345")]
	[InlineData("1234+345")]
	[InlineData("123+45")]
	[InlineData("123+4567")]
	// Separador ausente o distinto
	[InlineData("123456")]
	[InlineData("123-456")]
	[InlineData("123 456")]
	[InlineData("123++456")]
	// Caracteres no numéricos
	[InlineData("12a+345")]
	[InlineData("123+45b")]
	[InlineData("abc+def")]
	// Signos y separadores decimales
	[InlineData("+123+456")]
	[InlineData("-123+456")]
	[InlineData("123+456.7")]
	[InlineData("123,456")]
	// Espacios y saltos de línea sobrantes
	[InlineData(" 123+456")]
	[InlineData("123+456 ")]
	[InlineData("123+456\n")]
	[InlineData("\t123+456")]
	// Vacíos
	[InlineData("")]
	[InlineData("   ")]
	public void Crear_ConFormatoInvalido_LanzaArgumentException(string entrada)
	{
		var excepcion = Assert.Throws<ArgumentException>(() => Kilometer.Crear(entrada));

		Assert.Contains(Kilometer.FormatoCanonico, excepcion.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void Crear_ConNulo_LanzaArgumentException()
	{
		var excepcion = Assert.Throws<ArgumentException>(() => Kilometer.Crear(null));

		Assert.Contains(Kilometer.FormatoCanonico, excepcion.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void IntentarCrear_ConFormaCanonica_DevuelveVerdaderoYElValor()
	{
		var resultado = Kilometer.IntentarCrear("045+678", out var kilometro);

		Assert.True(resultado);
		Assert.NotNull(kilometro);
		Assert.Equal("045+678", kilometro.Valor);
	}

	[Theory]
	[InlineData("12+345")]
	[InlineData("abc+def")]
	[InlineData("123+456 ")]
	[InlineData("")]
	[InlineData(null)]
	public void IntentarCrear_ConFormatoInvalido_DevuelveFalsoYNoLanza(string? entrada)
	{
		var resultado = Kilometer.IntentarCrear(entrada, out var kilometro);

		Assert.False(resultado);
		Assert.Null(kilometro);
	}

	[Fact]
	public void DosKilometrosConElMismoValor_SonIguales()
	{
		var primero = Kilometer.Crear("012+345");
		var segundo = Kilometer.Crear("012+345");

		// Instancias distintas: la igualdad no puede venir de la referencia.
		Assert.NotSame(primero, segundo);
		Assert.True(primero.Equals(segundo));
		Assert.True(primero == segundo);
		Assert.False(primero != segundo);
		Assert.Equal(primero.GetHashCode(), segundo.GetHashCode());
	}

	[Fact]
	public void DosKilometrosConValorDistinto_NoSonIguales()
	{
		var primero = Kilometer.Crear("012+345");
		var segundo = Kilometer.Crear("012+346");

		Assert.False(primero.Equals(segundo));
		Assert.False(primero == segundo);
		Assert.True(primero != segundo);
	}

	[Fact]
	public void ComparacionConNulo_NoLanzaYEsFalsa()
	{
		var kilometro = Kilometer.Crear("012+345");
		Kilometer? nulo = null;

		Assert.False(kilometro == nulo);
		Assert.True(kilometro != nulo);
		Assert.False(kilometro.Equals(nulo));
		Assert.False(kilometro.Equals("012+345"));
	}

	[Fact]
	public void DosNulos_SonIguales()
	{
		Kilometer? primero = null;
		Kilometer? segundo = null;

		Assert.True(primero == segundo);
	}

	[Fact]
	public void SePuedeUsarComoClaveDeDiccionarioYEnConjuntos()
	{
		var conjunto = new HashSet<Kilometer>
		{
			Kilometer.Crear("012+345"),
			Kilometer.Crear("012+345"),
			Kilometer.Crear("012+346"),
		};

		Assert.Equal(2, conjunto.Count);
	}

	[Fact]
	public void ToString_DevuelveLaFormaCanonica()
	{
		Assert.Equal("012+345", Kilometer.Crear("012+345").ToString());
	}
}
