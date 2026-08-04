namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Los cuatro indicadores que el operador ve de un vistazo en la pantalla de Inicio.
/// </summary>
/// <param name="PendientesSincronizar">Registros en cola esperando envío.</param>
/// <param name="KilometroActual">Último kilómetro conocido, o <see langword="null"/> si no hay lectura.</param>
/// <param name="Avisos">Avisos operativos sin atender.</param>
/// <param name="EvidenciaLocal">Archivos de evidencia guardados en el dispositivo.</param>
public sealed record ResumenOperativo(
	int PendientesSincronizar,
	string? KilometroActual,
	int Avisos,
	int EvidenciaLocal)
{
	/// <summary>Resumen en cero, para el estado inicial de la pantalla.</summary>
	public static ResumenOperativo Vacio { get; } = new(0, null, 0, 0);
}
