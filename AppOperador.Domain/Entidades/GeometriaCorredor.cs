using AppOperador.Domain.ValueObjects;

namespace AppOperador.Domain.Entidades;

/// <summary>
/// Un vértice de la traza del corredor: dónde está y qué punto kilométrico le corresponde.
/// </summary>
/// <param name="Longitud">Grados decimales.</param>
/// <param name="Latitud">Grados decimales.</param>
/// <param name="Metros">
/// Punto kilométrico normalizado en metros (JTT-1395 CA 8). <c>147716</c> es el 147+716.
/// </param>
public readonly record struct VerticeCorredor(double Longitud, double Latitud, int Metros);

/// <summary>
/// Resultado de proyectar una posición sobre la traza del corredor.
/// </summary>
/// <param name="Metros">Punto kilométrico normalizado en metros del punto proyectado.</param>
/// <param name="DesviacionMetros">Distancia de la posición a la traza.</param>
/// <param name="MasAllaDeLaTraza">
/// <see langword="true"/> si el punto más cercano es uno de los dos extremos de la traza, lo que
/// indica que la posición queda **más allá del tramo con geometría** y no al lado de él.
/// </param>
public readonly record struct ProyeccionEnCorredor(
	int Metros,
	double DesviacionMetros,
	bool MasAllaDeLaTraza);

/// <summary>
/// La traza del corredor con su kilometraje, y la aritmética para situar una posición sobre ella.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué es.</b> Una polilínea en la que <b>cada vértice ya trae su kilómetro</b>. El anclaje a
/// las paletas de kilometraje se resolvió al generar el archivo, fuera de la app: aquí no hay
/// interpolación entre paletas ni distancias acumuladas, solo proyectar y mezclar los dos
/// kilómetros del segmento más cercano. Es lo que hace que el cálculo quepa en treinta líneas y
/// se pueda probar entero.
/// </para>
/// <para>
/// <b>Por qué proyección plana y no haversine.</b> El corredor mide 27 km y ocupa cuatro
/// centésimas de grado de latitud. A esa escala, convertir a metros con un factor fijo de coseno
/// introduce un error por debajo del centímetro, mientras que hacer haversine contra 723
/// segmentos en cada lectura gasta trigonometría sin comprar nada. La comprobación está en las
/// pruebas: el algoritmo devuelve las 27 paletas del tramo cubierto con <b>0.2 m de error medio y
/// 1 m como peor caso</b>.
/// </para>
/// <para>
/// <b>Esta clase no decide nada.</b> No sabe qué tolerancia es aceptable ni qué hacer con una
/// posición lejana: solo mide. Quien decide es <c>ReglaToleranciaCorredor</c>, y quien ordena el
/// flujo es el caso de uso.
/// </para>
/// </remarks>
public sealed class GeometriaCorredor
{
	/// <summary>Radio medio de la Tierra, en metros (IUGG).</summary>
	private const double RadioTierraMetros = 6371008.8;

	private readonly VerticeCorredor[] _vertices;
	private readonly double _factorLongitud;

	private GeometriaCorredor(VerticeCorredor[] vertices, string nombre)
	{
		_vertices = vertices;
		Nombre = nombre;

		// El factor que convierte grados de longitud en metros depende de la latitud. Se fija con
		// la latitud media de la traza en vez de recalcularlo por lectura: así dos posiciones
		// distintas se miden con la misma regla, que es lo que permite comparar desviaciones.
		var latitudMedia = vertices.Average(v => v.Latitud);
		_factorLongitud = Math.Cos(latitudMedia * Math.PI / 180);

		MetrosIniciales = vertices[0].Metros;
		MetrosFinales = vertices[^1].Metros;
	}

	/// <summary>De qué tramo es esta geometría, para poder decirlo en un aviso o una bitácora.</summary>
	public string Nombre { get; }

	/// <summary>Punto kilométrico, en metros, donde empieza el tramo con geometría.</summary>
	public int MetrosIniciales { get; }

	/// <summary>Punto kilométrico, en metros, donde termina el tramo con geometría.</summary>
	public int MetrosFinales { get; }

	/// <summary>Cuántos vértices tiene la traza.</summary>
	public int Vertices => _vertices.Length;

	/// <summary>
	/// Crea la geometría a partir de sus vértices.
	/// </summary>
	/// <remarks>
	/// <b>Exige al menos dos vértices y kilometraje creciente.</b> Con uno solo no hay segmento
	/// sobre el que proyectar, y con el kilometraje desordenado la interpolación devolvería
	/// kilómetros que retroceden a mitad del corredor — un defecto que en pantalla se leería como
	/// «el GPS está loco» y costaría días encontrar.
	/// </remarks>
	/// <exception cref="ArgumentException">La traza no sirve para proyectar.</exception>
	public static GeometriaCorredor Crear(IReadOnlyList<VerticeCorredor> vertices, string nombre)
	{
		ArgumentNullException.ThrowIfNull(vertices);

		if (vertices.Count < 2)
		{
			throw new ArgumentException(
				$"La traza del corredor necesita al menos dos vértices y trae {vertices.Count}.",
				nameof(vertices));
		}

		for (var i = 1; i < vertices.Count; i++)
		{
			if (vertices[i].Metros <= vertices[i - 1].Metros)
			{
				throw new ArgumentException(
					$"El kilometraje de la traza tiene que crecer siempre: el vértice {i} dice " +
					$"{vertices[i].Metros} m y el anterior {vertices[i - 1].Metros} m.",
					nameof(vertices));
			}
		}

		return new GeometriaCorredor([.. vertices], nombre);
	}

	/// <summary>
	/// Sitúa una posición sobre la traza y devuelve su kilometraje y cuánto se aparta.
	/// </summary>
	/// <remarks>
	/// Recorre los segmentos y se queda con el más cercano. Son 723 proyecciones de un punto
	/// sobre un segmento —dos restas, dos multiplicaciones y una raíz—, así que no hace falta
	/// índice espacial ninguno para lo que es una lectura cada vez que se abre el formulario.
	/// </remarks>
	public ProyeccionEnCorredor Proyectar(PosicionDispositivo posicion)
	{
		ArgumentNullException.ThrowIfNull(posicion);
		return Proyectar(posicion.Latitud, posicion.Longitud);
	}

	/// <inheritdoc cref="Proyectar(PosicionDispositivo)" />
	public ProyeccionEnCorredor Proyectar(double latitud, double longitud)
	{
		var (px, py) = APlano(longitud, latitud);

		var mejorDistancia = double.MaxValue;
		var mejorMetros = 0d;
		var mejorEnExtremo = false;

		for (var i = 0; i < _vertices.Length - 1; i++)
		{
			var inicio = _vertices[i];
			var fin = _vertices[i + 1];

			var (ax, ay) = APlano(inicio.Longitud, inicio.Latitud);
			var (bx, by) = APlano(fin.Longitud, fin.Latitud);

			var dx = bx - ax;
			var dy = by - ay;
			var largoAlCuadrado = (dx * dx) + (dy * dy);

			// Posición del pie de la perpendicular sobre el segmento, recortada a [0, 1] para que
			// no se salga por los lados. Ese recorte es justo lo que detecta los extremos.
			var t = largoAlCuadrado == 0
				? 0
				: Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / largoAlCuadrado, 0, 1);

			var cx = ax + (t * dx);
			var cy = ay + (t * dy);
			var distancia = Math.Sqrt(((px - cx) * (px - cx)) + ((py - cy) * (py - cy)));

			if (distancia >= mejorDistancia)
			{
				continue;
			}

			mejorDistancia = distancia;
			mejorMetros = inicio.Metros + (t * (fin.Metros - inicio.Metros));

			// Solo cuenta como "más allá de la traza" si el punto más cercano es una de las dos
			// puntas de la polilínea entera. Un recorte en un segmento intermedio es normal:
			// significa que el pie de la perpendicular cae en el vértice que comparten dos
			// segmentos, y ahí la traza sigue.
			mejorEnExtremo = (i == 0 && t == 0)
				|| (i == _vertices.Length - 2 && t == 1);
		}

		return new ProyeccionEnCorredor(
			(int)Math.Round(mejorMetros, MidpointRounding.AwayFromZero),
			mejorDistancia,
			mejorEnExtremo);
	}

	/// <summary>
	/// Pasa de grados a un plano local en metros.
	/// </summary>
	/// <remarks>
	/// El origen es irrelevante —solo se comparan distancias entre puntos convertidos con el
	/// mismo factor—, así que no se resta ninguno y se ahorra el arrastre de un punto de
	/// referencia por toda la clase.
	/// </remarks>
	private (double X, double Y) APlano(double longitud, double latitud) => (
		longitud * Math.PI / 180 * RadioTierraMetros * _factorLongitud,
		latitud * Math.PI / 180 * RadioTierraMetros);
}
