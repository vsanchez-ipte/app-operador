using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Dispositivo;

namespace AppOperador.IntegrationTests.Dispositivo;

/// <summary>
/// La geometría embebida del corredor, contra el archivo real (JTT-1395).
/// </summary>
/// <remarks>
/// <para>
/// <b>Esto prueba el dato, no el algoritmo.</b> La aritmética de proyección la cubre
/// <c>GeometriaCorredorTests</c> con trazas de juguete; aquí lo que se verifica es que el recurso
/// que viaja dentro del paquete sea el que se generó del KMZ y siga diciendo lo mismo.
/// </para>
/// <para>
/// <b>Por qué las 27 paletas y no una.</b> Una sola comprobación pasa igual si el archivo se
/// regeneró mal, se truncó a la mitad o se reordenó: basta con que ese punto siga bien. Las
/// paletas son la única verdad independiente que existe —son los letreros que el operador lee en
/// la carretera, y vienen del mismo KMZ pero de otra carpeta que la traza—, así que exigirlas
/// todas es lo único que delata un archivo corrompido.
/// </para>
/// </remarks>
public sealed class RepositorioGeometriaCorredorEmbebidoTests
{
	/// <summary>
	/// Cuánto se admite que se aparte una paleta de su propio kilómetro.
	/// </summary>
	/// <remarks>
	/// Al generar el archivo se midió <b>0.2 m de error medio y 1 m en el peor caso</b>. Se deja
	/// margen hasta 5 m para no romper la prueba por un redondeo, pero <b>no más</b>: con una
	/// tolerancia holgada esto dejaría de detectar justo lo que busca.
	/// </remarks>
	private const int MargenMetros = 5;

	[Fact]
	public async Task ElRecursoTraeLaTrazaCompletaDelKmz()
	{
		var geometria = await new RepositorioGeometriaCorredorEmbebido().ObtenerAsync();

		Assert.Equal(724, geometria.Vertices);
		Assert.Equal(120_764, geometria.MetrosIniciales);
		Assert.Equal(147_716, geometria.MetrosFinales);
	}

	[Theory]
	[InlineData(121, 32.5552211, -116.6047772)]
	[InlineData(122, 32.5532119, -116.6147813)]
	[InlineData(123, 32.5509209, -116.6249983)]
	[InlineData(124, 32.5501747, -116.6355303)]
	[InlineData(125, 32.5476395, -116.6455871)]
	[InlineData(126, 32.5472615, -116.6561758)]
	[InlineData(127, 32.5460424, -116.6666541)]
	[InlineData(128, 32.5434190, -116.6766807)]
	[InlineData(129, 32.5358589, -116.6818897)]
	[InlineData(130, 32.5275769, -116.6861878)]
	[InlineData(131, 32.5262917, -116.6965455)]
	[InlineData(132, 32.5273244, -116.7070735)]
	[InlineData(133, 32.5263814, -116.7173644)]
	[InlineData(134, 32.5261792, -116.7279430)]
	[InlineData(135, 32.5285079, -116.7377696)]
	[InlineData(136, 32.5368819, -116.7417003)]
	[InlineData(137, 32.5412404, -116.7500788)]
	[InlineData(138, 32.5428304, -116.7605499)]
	[InlineData(139, 32.5447066, -116.7709546)]
	[InlineData(140, 32.5455391, -116.7815125)]
	[InlineData(141, 32.5459114, -116.7921341)]
	[InlineData(142, 32.5496349, -116.8017264)]
	[InlineData(143, 32.5564739, -116.8071866)]
	[InlineData(144, 32.5577294, -116.8174708)]
	[InlineData(145, 32.5560704, -116.8275304)]
	[InlineData(146, 32.5521473, -116.8364067)]
	[InlineData(147, 32.5501084, -116.8463974)]
	public async Task CadaPaletaDelKmzCaeEnSuPropioKilometro(int paleta, double latitud, double longitud)
	{
		var geometria = await new RepositorioGeometriaCorredorEmbebido().ObtenerAsync();

		var proyeccion = geometria.Proyectar(latitud, longitud);

		Assert.InRange(
			proyeccion.Metros,
			(paleta * 1000) - MargenMetros,
			(paleta * 1000) + MargenMetros);

		// Y la paleta tiene que caer dentro de la tolerancia, o el operador que esté justo al lado
		// del letrero recibiría «fuera del corredor» estando donde más claro tiene su kilómetro.
		Assert.True(
			ReglaToleranciaCorredor.DentroDelCorredor(proyeccion.DesviacionMetros),
			$"La paleta PK {paleta} queda a {proyeccion.DesviacionMetros:F0} m de la traza, por " +
			$"encima de los {ReglaToleranciaCorredor.ToleranciaLateralMetros:F0} m de tolerancia.");

		Assert.False(proyeccion.MasAllaDeLaTraza);
	}

	[Theory]
	[InlineData(120, 32.5545576, -116.5944766)]
	[InlineData(148, 32.5431474, -116.8535952)]
	public async Task LasDosPaletasDeLosExtremosQuedanFueraDelTramoCubierto(
		int paleta,
		double latitud,
		double longitud)
	{
		// Es el límite conocido y se fija aquí a propósito: el KMZ trae 29 paletas pero su traza
		// termina antes de la primera y de la última —764 m antes del PK 120 y 284 m antes del
		// PK 148—. Ahí no se inventa geometría: se responde «tramo sin geometría», y esta prueba
		// avisa si alguna vez el archivo pasa a cubrirlos, que sería una mejora que hay que
		// reflejar en la documentación en vez de descubrirla en campo.
		var geometria = await new RepositorioGeometriaCorredorEmbebido().ObtenerAsync();

		var proyeccion = geometria.Proyectar(latitud, longitud);

		Assert.False(
			ReglaToleranciaCorredor.DentroDelCorredor(proyeccion.DesviacionMetros),
			$"La paleta PK {paleta} ya cae dentro de la traza. Revisa el recurso y la nota de " +
			"cobertura del corredor.");

		// Y se distingue de estar al lado de la carretera: más allá de una punta de la traza.
		Assert.True(proyeccion.MasAllaDeLaTraza);
	}

	[Fact]
	public async Task LaGeometriaSeLeeUnaSolaVezYSeReutiliza()
	{
		// Son 22 KB y 724 vértices que no cambian mientras la app esté abierta. Releerlos en cada
		// lectura de GPS —que ocurre al abrir el formulario y en cada recálculo— sería gasto puro.
		var repositorio = new RepositorioGeometriaCorredorEmbebido();

		var primera = await repositorio.ObtenerAsync();
		var segunda = await repositorio.ObtenerAsync();

		Assert.Same(primera, segunda);
	}

	[Fact]
	public async Task UnaLecturaSobreLaTrazaSeSituaConPrecisionDeMetros()
	{
		// El vértice 0 de la traza, tal como está en el recurso: si el parseo cambiara la cultura
		// o desordenara las columnas, esto se iría a otro continente antes que a otro kilómetro.
		var geometria = await new RepositorioGeometriaCorredorEmbebido().ObtenerAsync();
		var posicion = PosicionDispositivo.Crear(
			32.5547288,
			-116.6024039,
			8,
			new DateTime(2026, 8, 24, 20, 0, 0, DateTimeKind.Utc));

		var proyeccion = geometria.Proyectar(posicion);

		Assert.Equal(120_764, proyeccion.Metros);
		Assert.InRange(proyeccion.DesviacionMetros, 0, 1);
	}
}
