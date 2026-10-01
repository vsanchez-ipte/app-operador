using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.ViewModels;

// Solo presentación: lo que decide si se puede adjuntar vive en ResumenEvidencias.
public sealed record EvidenciaVista(
	string Uuid,
	string Nombre,
	string Tipo,
	string Tamano,
	bool YaEnviada,
	string Ruta,
	bool EsImagen,
	string TipoMime = "")
{
	public static EvidenciaVista Desde(EvidenciaAdjunta adjunta) => new(
		adjunta.Uuid,
		adjunta.NombreOriginal,
		TipoLegible(adjunta.TipoMime),
		TamanoLegible(adjunta.Bytes),
		adjunta.Estado == EstadoSincronizacion.Sincronizado,
		adjunta.RutaArchivo,
		EsImagen: adjunta.TipoMime.StartsWith("image/", StringComparison.OrdinalIgnoreCase),
		// El tipo crudo decide con qué aplicación se abre; el legible es para el operador.
		TipoMime: adjunta.TipoMime);

	// Sin miniatura para lo que no es imagen: decodificar un video para 72 píxeles no compra nada.
	public string Etiqueta => EsImagen ? string.Empty : Tipo;

	private static string TipoLegible(string tipoMime)
	{
		if (string.IsNullOrWhiteSpace(tipoMime))
		{
			return "Archivo";
		}

		var barra = tipoMime.LastIndexOf('/');

		return barra >= 0 && barra < tipoMime.Length - 1
			? tipoMime[(barra + 1)..].ToUpperInvariant()
			: tipoMime.ToUpperInvariant();
	}

	// Múltiplos de 1024, los mismos del tope, para no contradecirse en pantalla.
	private static string TamanoLegible(long bytes)
	{
		const long Kilo = 1024;
		const long Mega = Kilo * 1024;

		return bytes >= Mega
			? $"{bytes / (double)Mega:0.#} MB"
			: $"{Math.Max(1, bytes / Kilo)} KB";
	}
}
