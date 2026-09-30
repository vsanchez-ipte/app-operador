namespace AppOperador.Aplicacion.Modelos;

public sealed record ResumenOperativo(
	int PendientesSincronizar,
	int Avisos,
	int EvidenciaLocal)
{
	public static ResumenOperativo Vacio { get; } = new(0, 0, 0);
}
