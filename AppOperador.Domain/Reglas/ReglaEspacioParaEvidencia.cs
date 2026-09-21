namespace AppOperador.Domain.Reglas;

/// <summary>
/// Decide si en el dispositivo cabe una evidencia más (JTT-289 CA 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>Se exige un margen, no solo que quepa el archivo.</b> Un dispositivo que se queda sin
/// espacio deja de poder escribir la base local, y con ella la cola entera: la incidencia que
/// se estaba capturando, su evidencia y todo lo pendiente. Llenarlo hasta el último byte por
/// una fotografía sería perder mucho más que la fotografía.
/// </para>
/// <para>
/// Con el espacio <b>desconocido</b> no se bloquea nada: el sistema puede negarse a medirlo, y
/// castigar al operador por eso sería tratar una incertidumbre como un disco lleno. La copia
/// sigue teniendo su propia comprobación al fallar.
/// </para>
/// </remarks>
public static class ReglaEspacioParaEvidencia
{
	/// <summary>Lo que debe quedar libre después de guardar la evidencia.</summary>
	public const long MargenSeguridadBytes = 50L * 1024 * 1024;

	/// <param name="bytesLibres">Espacio libre medido, o <see langword="null"/> si no se pudo medir.</param>
	/// <param name="bytesNecesarios">Tamaño del archivo, o el máximo admitido si aún no existe.</param>
	public static bool Cabe(long? bytesLibres, long bytesNecesarios) =>
		bytesLibres is not { } libres || libres - bytesNecesarios >= MargenSeguridadBytes;
}
