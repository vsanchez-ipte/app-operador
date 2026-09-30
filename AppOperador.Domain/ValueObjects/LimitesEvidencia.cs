namespace AppOperador.Domain.ValueObjects;

// Los publica el catálogo del servidor; la app no los fija.
public sealed record LimitesEvidencia(
	IReadOnlyList<string> FormatosPermitidos,
	int TamanoMaximoMb,
	int MaximoArchivosPorIncidencia)
{
	// Sin catálogo no hay límites, y sin límites no se puede adjuntar.
	public static readonly LimitesEvidencia Desconocidos = new([], 0, 0);

	public bool EstanDefinidos =>
		FormatosPermitidos.Count > 0 && TamanoMaximoMb > 0 && MaximoArchivosPorIncidencia > 0;

	public long TamanoMaximoBytes => (long)TamanoMaximoMb * 1024 * 1024;

	public bool AdmiteFormato(string? tipoMime) =>
		!string.IsNullOrWhiteSpace(tipoMime)
		&& FormatosPermitidos.Any(f => string.Equals(f, tipoMime, StringComparison.OrdinalIgnoreCase));

	public bool AdmiteVideo =>
		FormatosPermitidos.Any(f => f.StartsWith("video/", StringComparison.OrdinalIgnoreCase));
}
