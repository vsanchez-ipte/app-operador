namespace AppOperador.Domain.Reglas;

internal static class InstanteUtc
{
	public static void Exigir(DateTime instante, string nombreParametro)
	{
		if (instante.Kind != DateTimeKind.Utc)
		{
			throw new ArgumentException(
				$"El instante debe estar expresado en UTC (DateTimeKind.Utc); se recibió {instante.Kind}.",
				nombreParametro);
		}
	}
}
