namespace AppOperador.Domain.Reglas;

// Clave local y no folio: el folio no existe hasta sincronizar. Hora en UTC, como fch_alta en el servidor.
public static class NombreEvidencia
{
	private const int LargoMaximoExtension = 10;

	// Sin clave se devuelve el nombre del sistema: un nombre feo es mejor que ninguno.
	public static string Componer(
		string? claveLocal,
		DateTime instanteUtc,
		string? nombreDelSistema)
	{
		var clave = claveLocal?.Trim();

		if (string.IsNullOrEmpty(clave))
		{
			return nombreDelSistema ?? string.Empty;
		}

		return $"{clave}-{instanteUtc:HHmmss}{ExtensionDe(nombreDelSistema)}";
	}

	private static string ExtensionDe(string? nombre)
	{
		if (string.IsNullOrWhiteSpace(nombre))
		{
			return string.Empty;
		}

		var punto = nombre.LastIndexOf('.');

		// Un punto al final no es extensión, y uno al principio es un archivo oculto.
		if (punto <= 0 || punto == nombre.Length - 1)
		{
			return string.Empty;
		}

		var extension = nombre[punto..];

		if (extension.Length > LargoMaximoExtension)
		{
			return string.Empty;
		}

		return extension[1..].All(char.IsLetterOrDigit)
			? extension.ToLowerInvariant()
			: string.Empty;
	}
}
