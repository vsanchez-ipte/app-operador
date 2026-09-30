namespace AppOperador.Domain.Reglas;

public static class ReglaEspacioParaEvidencia
{
	// Un disco lleno deja sin lugar a la base local, y con ella a toda la cola.
	public const long MargenSeguridadBytes = 50L * 1024 * 1024;

	// Si el sistema no deja medir el espacio, no se bloquea al operador.
	public static bool Cabe(long? bytesLibres, long bytesNecesarios) =>
		bytesLibres is not { } libres || libres - bytesNecesarios >= MargenSeguridadBytes;
}
