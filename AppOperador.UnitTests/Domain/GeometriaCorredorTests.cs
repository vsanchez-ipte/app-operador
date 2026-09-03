using AppOperador.Domain.Entidades;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// La aritmética de situar una posición sobre la traza (JTT-1395).
/// </summary>
/// <remarks>
/// Trazas de juguete a propósito: aquí se comprueba el <b>algoritmo</b>, con geometrías donde el
/// resultado se puede calcular a mano. Que el archivo real diga lo que debe es otra prueba,
/// <c>RepositorioGeometriaCorredorEmbebidoTests</c>, y son cosas distintas: una falla si alguien
/// rompe la proyección, la otra si alguien rompe el dato.
/// </remarks>
public sealed class GeometriaCorredorTests
{
	/// <summary>Tramo recto de este a oeste, con dos kilómetros consecutivos.</summary>
	private static GeometriaCorredor Recta() => GeometriaCorredor.Crear(
		[
			new VerticeCorredor(-116.70, 32.50, 130_000),
			new VerticeCorredor(-116.69, 32.50, 131_000),
		],
		"Prueba");

	/// <summary>
	/// Tramo en escuadra: un kilómetro al este y otro al norte.
	/// </summary>
	/// <remarks>
	/// Existe para el caso que más fácil se rompe: un punto por fuera de una curva. El pie de la
	/// perpendicular se sale de los dos segmentos y queda recortado en el vértice que comparten,
	/// que es exactamente la señal que usa <c>MasAllaDeLaTraza</c> — pero ahí la traza no se ha
	/// acabado, solo dobla.
	/// </remarks>
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
		// El defecto que busca: tratar el recorte en un vértice intermedio como final de traza.
		// Daría «este tramo no tiene geometría» a un operador parado en cada curva del corredor,
		// y el corredor real tiene 724 vértices, casi todos en curva.
		//
		// El punto va al sureste de la esquina, en la cuña que queda por fuera de los dos
		// segmentos: en el primero el pie de la perpendicular se recorta al final y en el segundo
		// al principio, así que los dos acaban en el vértice compartido. Es la única forma de
		// forzar el recorte en un vértice intermedio.
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
		// Con un solo vértice no hay nada sobre lo que proyectar, y la clase devolvería siempre
		// ese kilómetro con desviación gigante en vez de fallar.
		Assert.Throws<ArgumentException>(() => GeometriaCorredor.Crear(
			[new VerticeCorredor(-116.70, 32.50, 130_000)],
			"Inválida"));
	}

	[Fact]
	public void CrearRechazaDosVerticesEnElMismoKilometro()
	{
		// Kilometraje que no avanza tampoco sirve: la interpolación se queda plana y dos puntos
		// separados en la carretera devolverían el mismo kilómetro.
		Assert.Throws<ArgumentException>(() => GeometriaCorredor.Crear(
			[
				new VerticeCorredor(-116.70, 32.50, 130_000),
				new VerticeCorredor(-116.69, 32.50, 130_000),
			],
			"Inválida"));
	}
}
