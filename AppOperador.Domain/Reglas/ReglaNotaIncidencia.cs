namespace AppOperador.Domain.Reglas;

public static class ReglaNotaIncidencia
{
	// El API valida con el mismo mínimo: si cambia, cambia en los dos lados.
	public const int MinimoCaracteres = 8;

	public const int MaximoCaracteres = 1000;

	// Los espacios no cuentan para el mínimo, pero sí para el máximo, que protege la columna.
	public static bool EsSuficiente(bool tipoExigeDescripcion, string? nota)
	{
		var texto = nota?.Trim() ?? string.Empty;

		if (texto.Length > MaximoCaracteres)
		{
			return false;
		}

		return !tipoExigeDescripcion || texto.Length >= MinimoCaracteres;
	}
}
