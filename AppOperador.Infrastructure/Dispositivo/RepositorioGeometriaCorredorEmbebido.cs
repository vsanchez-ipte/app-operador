using System.Globalization;
using System.Reflection;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Entidades;

namespace AppOperador.Infrastructure.Dispositivo;

// Generada desde el KMZ del larguillo; cubre solo Tijuana–Tecate. Se lee una vez y se conserva.
public sealed class RepositorioGeometriaCorredorEmbebido : IRepositorioGeometriaCorredor
{
	private const string SufijoRecurso = "corredor-tijuana-tecate.csv";

	public const string NombreTramo = "Tijuana-Tecate";

	private readonly Lazy<Task<GeometriaCorredor>> _geometria;

	public RepositorioGeometriaCorredorEmbebido()
	{
		_geometria = new Lazy<Task<GeometriaCorredor>>(
			() => Task.FromResult(Leer()),
			LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public Task<GeometriaCorredor> ObtenerAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();
		return _geometria.Value;
	}

	// Falla de inmediato ante un recurso roto: una geometría a medias daría kilómetros falsos.
	private static GeometriaCorredor Leer()
	{
		var ensamblado = Assembly.GetExecutingAssembly();
		var nombre = Array.Find(
			ensamblado.GetManifestResourceNames(),
			n => n.EndsWith(SufijoRecurso, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException(
				$"No se encontró el recurso incrustado '{SufijoRecurso}' en " +
				$"{ensamblado.GetName().Name}. Comprueba que siga declarado como EmbeddedResource " +
				"en AppOperador.Infrastructure.csproj.");

		using var flujo = ensamblado.GetManifestResourceStream(nombre)
			?? throw new InvalidOperationException($"El recurso '{nombre}' no se pudo abrir.");
		using var lector = new StreamReader(flujo);

		var vertices = new List<VerticeCorredor>(750);
		var numeroLinea = 0;

		while (lector.ReadLine() is { } linea)
		{
			numeroLinea++;

			// Los comentarios del recurso documentan su origen, por si alguien lo abre suelto.
			if (linea.Length == 0 || linea[0] == '#')
			{
				continue;
			}

			var partes = linea.Split(';');
			if (partes.Length != 3
				// Cultura invariante: con coma decimal la traza acabaría en otro continente.
				|| !double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitud)
				|| !double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitud)
				|| !int.TryParse(partes[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var metros))
			{
				throw new InvalidOperationException(
					$"La línea {numeroLinea} de '{SufijoRecurso}' no tiene la forma " +
					$"'longitud;latitud;metros': «{linea}».");
			}

			vertices.Add(new VerticeCorredor(longitud, latitud, metros));
		}

		return GeometriaCorredor.Crear(vertices, NombreTramo);
	}
}
