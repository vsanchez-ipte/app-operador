using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// Con qué nombre se guarda una evidencia capturada (JTT-1398).
/// </summary>
/// <remarks>
/// Existe por lo que se vio en la base el 31 de agosto: las cuatro evidencias enviadas llegaron
/// al CCO llamándose <c>6441d2f9fb6f4cea9b48fc0bae7dbad6.jpg</c>, porque el sistema nombra así el
/// temporal de una fotografía recién tomada y la app lo propagaba tal cual.
/// </remarks>
public sealed class NombreEvidenciaTests
{
	private static readonly DateTime Instante = new(2026, 8, 31, 18, 14, 9, DateTimeKind.Utc);

	[Fact]
	public void SeNombraConLaClaveLocalYLaHora()
	{
		Assert.Equal(
			"LOC-000123-181409.jpg",
			NombreEvidencia.Componer("LOC-000123", Instante, "6441d2f9fb6f4cea9b48fc0bae7dbad6.jpg"));
	}

	[Fact]
	public void DosCapturasDelMismoSegundoSonLoUnicoQueChoca()
	{
		// La hora da unicidad sin llevar cuenta de nada, que es lo que se le pedía: con un
		// consecutivo, quitar la segunda de tres y adjuntar otra repetiría el número.
		var primera = NombreEvidencia.Componer("LOC-000123", Instante, "a.jpg");
		var segunda = NombreEvidencia.Componer("LOC-000123", Instante.AddSeconds(1), "b.jpg");

		Assert.NotEqual(primera, segunda);
		Assert.Equal("LOC-000123-181410.jpg", segunda);
	}

	[Fact]
	public void LaExtensionSeConservaEnMinusculas()
	{
		Assert.Equal("LOC-1-181409.pdf", NombreEvidencia.Componer("LOC-1", Instante, "ACTA.PDF"));
	}

	[Fact]
	public void SinExtensionNoSeInventaNinguna()
	{
		Assert.Equal("LOC-1-181409", NombreEvidencia.Componer("LOC-1", Instante, "sinpunto"));
	}

	[Theory]
	[InlineData("termina.en.punto.")]
	[InlineData(".oculto")]
	[InlineData("raro.jp g")]
	[InlineData("largo.extensionquenoloes")]
	public void LoQueNoParezcaUnaExtensionSeDescarta(string nombre)
	{
		Assert.Equal("LOC-1-181409", NombreEvidencia.Componer("LOC-1", Instante, nombre));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void SinClaveLocalSeConservaElNombreDelSistema(string? clave)
	{
		// Un nombre feo es mejor que ninguno: quedarse sin nombre rompería la lista de la app.
		Assert.Equal(
			"6441d2f9.jpg",
			NombreEvidencia.Componer(clave, Instante, "6441d2f9.jpg"));
	}

	[Fact]
	public void LaClaveSeLimpiaDeEspacios()
	{
		Assert.Equal("LOC-9-181409.png", NombreEvidencia.Componer("  LOC-9  ", Instante, "x.png"));
	}
}
