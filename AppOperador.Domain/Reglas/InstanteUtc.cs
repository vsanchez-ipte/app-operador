namespace AppOperador.Domain.Reglas;

/// <summary>
/// Comprobación común de que un instante viene expresado en UTC.
/// </summary>
/// <remarks>
/// <para>
/// Toda la vigencia de sesión se compara en UTC. Aceptar <see cref="DateTimeKind.Unspecified"/>
/// dejaría entrar horas locales sin que nada avise, y el desfase se manifestaría como una
/// ventana offline más larga o más corta de lo debido, según la zona.
/// </para>
/// <para>
/// Vive aparte porque la exigen varias reglas: tenerla repetida en cada una hacía que el
/// mensaje de error pudiera divergir con el tiempo.
/// </para>
/// </remarks>
internal static class InstanteUtc
{
	/// <exception cref="ArgumentException">El instante no está expresado en UTC.</exception>
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
