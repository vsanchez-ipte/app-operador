using AppOperador.Domain.ValueObjects;

namespace AppOperador.Domain.Entidades;

public readonly record struct VerticeCorredor(double Longitud, double Latitud, int Metros);

// MasAllaDeLaTraza: el punto más cercano es un extremo, así que la posición cae fuera del tramo con geometría.
public readonly record struct ProyeccionEnCorredor(
	int Metros,
	double DesviacionMetros,
	bool MasAllaDeLaTraza);

// Cada vértice ya trae su kilómetro. Proyección plana: en 27 km el error es menor a un centímetro.
public sealed class GeometriaCorredor
{
	private const double RadioTierraMetros = 6371008.8;

	private readonly VerticeCorredor[] _vertices;
	private readonly double _factorLongitud;

	private GeometriaCorredor(VerticeCorredor[] vertices, string nombre)
	{
		_vertices = vertices;
		Nombre = nombre;

		// Un solo factor para toda la traza, para que las desviaciones se midan con la misma regla.
		var latitudMedia = vertices.Average(v => v.Latitud);
		_factorLongitud = Math.Cos(latitudMedia * Math.PI / 180);

		MetrosIniciales = vertices[0].Metros;
		MetrosFinales = vertices[^1].Metros;
	}

	public string Nombre { get; }

	public int MetrosIniciales { get; }

	public int MetrosFinales { get; }

	public int Vertices => _vertices.Length;

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

	public ProyeccionEnCorredor Proyectar(PosicionDispositivo posicion)
	{
		ArgumentNullException.ThrowIfNull(posicion);
		return Proyectar(posicion.Latitud, posicion.Longitud);
	}

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

			// El recorte a [0, 1] es lo que detecta los extremos.
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

			// Solo las dos puntas de la polilínea entera cuentan como extremo.
			mejorEnExtremo = (i == 0 && t == 0)
				|| (i == _vertices.Length - 2 && t == 1);
		}

		return new ProyeccionEnCorredor(
			(int)Math.Round(mejorMetros, MidpointRounding.AwayFromZero),
			mejorDistancia,
			mejorEnExtremo);
	}

	// Sin origen: solo se comparan distancias convertidas con el mismo factor.
	private (double X, double Y) APlano(double longitud, double latitud) => (
		longitud * Math.PI / 180 * RadioTierraMetros * _factorLongitud,
		latitud * Math.PI / 180 * RadioTierraMetros);
}
