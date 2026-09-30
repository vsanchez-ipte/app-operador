using AppOperador.Domain.Entidades;

namespace AppOperador.UnitTests.Domain;

public sealed class GeometriaCorredorTests
{
	private static GeometriaCorredor Recta() => GeometriaCorredor.Crear(
		[
			new VerticeCorredor(-116.70, 32.50, 130_000),
			new VerticeCorredor(-116.69, 32.50, 131_000),
		],
		"Prueba");

	private static GeometriaCorredor Escuadra() => GeometriaCorredor.Crear(
		[
			new VerticeCorredor(-116.70, 32.50, 130_000),
			new VerticeCorredor(-116.69, 32.50, 131_000),
			new VerticeCorredor(-116.69, 32.51, 132_000),
		],
		"Prueba");

	[Fact]
	public void ProyectarInterpolaElKilometrajeDelSegmentoMasCercano()
	{
		var resultado = Recta().Proyectar(32.50, -116.6975);

		Assert.Equal(130_250, resultado.Metros);
		Assert.InRange(resultado.DesviacionMetros, 0, 0.001);
		Assert.False(resultado.MasAllaDeLaTraza);
	}

	[Fact]
	public void UnaPosicionAlLadoDeLaTrazaNoEstaMasAllaDeElla()
	{
		// Al norte del punto medio: el pie de la perpendicular cae dentro del segmento.
		var resultado = Recta().Proyectar(32.505, -116.695);

		Assert.Equal(130_500, resultado.Metros);
		Assert.InRange(resultado.DesviacionMetros, 500, 600);

		// Es «fuera del corredor», no «tramo sin geometría»: la traza pasa justo al lado.
		Assert.False(resultado.MasAllaDeLaTraza);
	}

	[Fact]
	public void MasAllaDelPrimerVerticeSeMarcaComoFueraDeLaTraza()
	{
		var resultado = Recta().Proyectar(32.50, -116.75);

		Assert.Equal(130_000, resultado.Metros);
		Assert.True(resultado.MasAllaDeLaTraza);
	}

	[Fact]
	public void MasAllaDelUltimoVerticeSeMarcaComoFueraDeLaTraza()
	{
		var resultado = Recta().Proyectar(32.50, -116.60);

		Assert.Equal(131_000, resultado.Metros);
		Assert.True(resultado.MasAllaDeLaTraza);
	}

	[Fact]
	public void PorFueraDeUnaCurvaLaTrazaNoSeHaAcabado()
	{
		// El punto cae en la cuña exterior de la esquina: única forma de forzar el recorte en un vértice intermedio.
		var resultado = Escuadra().Proyectar(32.495, -116.685);

		Assert.False(resultado.MasAllaDeLaTraza);

		// Y el kilómetro es el de la esquina, no el de ninguno de los dos extremos.
		Assert.Equal(131_000, resultado.Metros);
	}

	[Fact]
	public void CrearRechazaKilometrajeQueRetrocede()
	{
		Assert.Throws<ArgumentException>(() => GeometriaCorredor.Crear(
			[
				new VerticeCorredor(-116.70, 32.50, 131_000),
				new VerticeCorredor(-116.69, 32.50, 130_000),
			],
			"Inválida"));
	}

	[Fact]
	public void CrearRechazaUnaTrazaSinSegmentos()
	{
		Assert.Throws<ArgumentException>(() => GeometriaCorredor.Crear(
			[new VerticeCorredor(-116.70, 32.50, 130_000)],
			"Inválida"));
	}

	[Fact]
	public void CrearRechazaDosVerticesEnElMismoKilometro()
	{
		Assert.Throws<ArgumentException>(() => GeometriaCorredor.Crear(
			[
				new VerticeCorredor(-116.70, 32.50, 130_000),
				new VerticeCorredor(-116.69, 32.50, 130_000),
			],
			"Inválida"));
	}
}
