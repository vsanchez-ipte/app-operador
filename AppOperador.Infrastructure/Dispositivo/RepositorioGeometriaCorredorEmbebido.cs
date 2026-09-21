using System.Globalization;
using System.Reflection;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Entidades;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Geometría del corredor leída del recurso incrustado en el ensamblado (JTT-1395).
/// </summary>
/// <remarks>
/// <para>
/// <b>El archivo lo genera el KMZ, no se escribe a mano.</b> Sale de
/// <c>200217-IT-B Larguillo ITS Tijuana - Tecate.kmz</c>, que entregó el líder el 20-ago-2026:
/// la traza de 724 vértices, con el kilometraje de cada vértice ya resuelto contra las 29 paletas
/// del mismo archivo. El anclaje se hizo al generarlo para que la app no cargue con esa
/// aritmética; el procedimiento y las mediciones están en <c>JTT-280/08-kmz-corredor.md</c>.
/// </para>
/// <para>
/// <b>Se lee una vez y se conserva.</b> Son 22 KB y 724 vértices que no cambian mientras la app
/// esté abierta; releerlos en cada lectura de GPS gastaría por gastar. La caché es perezosa y
/// segura entre hilos porque la pantalla puede pedir el kilómetro y recalcularlo a la vez.
/// </para>
/// <para>
/// ⚠️ <b>Cubre Tijuana–Tecate, del PK 120.764 al 147.716</b>, que es lo que el KMZ trae. El
/// corredor del CA 3 de JTT-1395 es Tijuana–Mexicali: fuera de ese tramo no hay geometría y el
/// caso de uso lo dice con un motivo propio, en vez de fingir que el operador está fuera del
/// corredor.
/// </para>
/// </remarks>
public sealed class RepositorioGeometriaCorredorEmbebido : IRepositorioGeometriaCorredor
{
	/// <summary>Final del nombre del recurso incrustado, sin el prefijo del espacio de nombres.</summary>
	private const string SufijoRecurso = "corredor-tijuana-tecate.csv";

	/// <summary>Nombre del tramo, para avisos y bitácora.</summary>
	public const string NombreTramo = "Tijuana-Tecate";

	private readonly Lazy<Task<GeometriaCorredor>> _geometria;

	public RepositorioGeometriaCorredorEmbebido()
	{
		_geometria = new Lazy<Task<GeometriaCorredor>>(
			() => Task.FromResult(Leer()),
			LazyThreadSafetyMode.ExecutionAndPublication);
	}

	/// <inheritdoc />
	public Task<GeometriaCorredor> ObtenerAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();
		return _geometria.Value;
	}

	/// <summary>
	/// Lee y valida el recurso.
	/// </summary>
	/// <remarks>
	/// <b>Falla ruidosamente y de una vez.</b> Un recurso que no se empaquetó, o una línea rota
	/// por una edición a mano, tienen que tumbar la app al primer intento con el motivo escrito:
	/// devolver una geometría a medias daría kilómetros equivocados con toda la pinta de ser
	/// buenos, y eso no se descubre nunca.
	/// </remarks>
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

			// Las líneas de comentario documentan de dónde salió el archivo y qué tramo cubre.
			// Van dentro del propio recurso para que sigan ahí si alguien lo abre suelto.
			if (linea.Length == 0 || linea[0] == '#')
			{
				continue;
			}

			var partes = linea.Split(';');
			if (partes.Length != 3
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

		// Cultura invariante en el parseo, no la del dispositivo: con una cultura de coma
		// decimal, "-116.6024039" se leería como -1166024039 y la traza acabaría en otro
		// continente. Es el mismo cuidado que ya tiene el envío del kilómetro al API.
		return GeometriaCorredor.Crear(vertices, NombreTramo);
	}
}
