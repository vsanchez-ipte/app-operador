using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Un archivo adjunto tal como se lee en la lista, antes de guardar (JTT-1398 CA 1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Nombre, tipo y tamaño.</b> Los tres los pidió el PO explícitamente en JTT-289: son los
/// datos que se muestran en la lista de archivos adjuntos visible antes de enviar.
/// </para>
/// <para>
/// Aquí solo hay <b>presentación</b>: formatear bytes y acortar un tipo MIME no es una decisión
/// de negocio. Lo que sí lo es —si se puede adjuntar otro, cuántos faltan, por qué no— vive en
/// <c>ResumenEvidencias</c>, en Aplicación, donde se puede probar.
/// </para>
/// </remarks>
public sealed record EvidenciaVista(
	string Uuid,
	string Nombre,
	string Tipo,
	string Tamano,
	bool YaEnviada)
{
	public static EvidenciaVista Desde(EvidenciaAdjunta adjunta) => new(
		adjunta.Uuid,
		adjunta.NombreOriginal,
		TipoLegible(adjunta.TipoMime),
		TamanoLegible(adjunta.Bytes),
		adjunta.Estado == EstadoSincronizacion.Sincronizado);

	/// <summary>
	/// «image/jpeg» se lee «JPEG».
	/// </summary>
	/// <remarks>
	/// El tipo MIME es contrato entre máquinas. Al operador, parado en carretera, le sirve saber
	/// si es una foto o un documento, no la cadena que viaja por el cable.
	/// </remarks>
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

	/// <summary>
	/// Bytes en la unidad que se entiende de un vistazo.
	/// </summary>
	/// <remarks>
	/// Se usan múltiplos de 1024, los mismos con los que se compara contra el tope: mostrar
	/// «5.2 MB» junto a un límite de 5 MB que sí lo admite sería contradecirse en pantalla.
	/// </remarks>
	private static string TamanoLegible(long bytes)
	{
		const long Kilo = 1024;
		const long Mega = Kilo * 1024;

		return bytes >= Mega
			? $"{bytes / (double)Mega:0.#} MB"
			: $"{Math.Max(1, bytes / Kilo)} KB";
	}
}
