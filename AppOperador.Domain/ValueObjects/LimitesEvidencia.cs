namespace AppOperador.Domain.ValueObjects;

/// <summary>
/// Lo que el servidor admite como evidencia: formatos, tamaño y cuántos archivos (JTT-1398).
/// </summary>
/// <remarks>
/// <para>
/// <b>Los tres valores los publica el catálogo; la app no los codifica.</b> Producto no contestó
/// DA-14, así que hoy están asumidos —cuatro formatos, 5 MB y 3 archivos— y el servidor los
/// declara en cada descarga. Leerlos de ahí es lo que hace que corregirlos sea tocar un
/// <c>appsettings</c> del API y no publicar una versión nueva en las tiendas.
/// </para>
/// <para>
/// Es también lo que abarata <b>JTT-289</b>, que ya fijó ocho archivos de 15 MB con video: con
/// los límites leídos, es cambiar dos números del lado del servidor.
/// </para>
/// <para>
/// <b>Se valida con los mismos números que el servidor</b>, no con unos parecidos. Un archivo
/// que la app acepta y el servidor rechaza gasta datos móviles del operador para acabar en un
/// error que se podía haber dicho antes de subirlo.
/// </para>
/// </remarks>
/// <param name="FormatosPermitidos">Tipos MIME admitidos, tal como los nombra el servidor.</param>
/// <param name="TamanoMaximoMb">Tope por archivo, en megabytes.</param>
/// <param name="MaximoArchivosPorIncidencia">Cuántos archivos admite una incidencia.</param>
public sealed record LimitesEvidencia(
	IReadOnlyList<string> FormatosPermitidos,
	int TamanoMaximoMb,
	int MaximoArchivosPorIncidencia)
{
	/// <summary>Límites desconocidos, cuando todavía no se ha descargado el catálogo.</summary>
	/// <remarks>
	/// <b>No son «sin límite» ni un valor de reserva</b>: son la ausencia del dato. Mientras
	/// estén así no se puede adjuntar, igual que sin catálogo no se puede capturar. Inventar un
	/// tope aquí sería justo lo que este diseño evita.
	/// </remarks>
	public static readonly LimitesEvidencia Desconocidos = new([], 0, 0);

	/// <summary>Indica si el servidor declaró unos límites con los que se pueda validar.</summary>
	public bool EstanDefinidos =>
		FormatosPermitidos.Count > 0 && TamanoMaximoMb > 0 && MaximoArchivosPorIncidencia > 0;

	/// <summary>Tope por archivo en bytes, que es la unidad en la que se mide un archivo.</summary>
	public long TamanoMaximoBytes => (long)TamanoMaximoMb * 1024 * 1024;

	/// <summary>Indica si el servidor admite ese tipo MIME.</summary>
    /// <remarks>
    /// Se compara sin distinguir mayúsculas: el tipo lo determina el servidor por contenido y
    /// no hay garantía de con qué caja lo escriba.
    /// </remarks>
	public bool AdmiteFormato(string? tipoMime) =>
		!string.IsNullOrWhiteSpace(tipoMime)
		&& FormatosPermitidos.Any(f => string.Equals(f, tipoMime, StringComparison.OrdinalIgnoreCase));
}
