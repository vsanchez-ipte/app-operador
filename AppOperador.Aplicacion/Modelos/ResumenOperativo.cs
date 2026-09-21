namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Los contadores que el operador ve de un vistazo en la pantalla de Inicio.
/// </summary>
/// <remarks>
/// El kilómetro actual no va aquí: no es un contador del estado local sino una lectura del
/// GPS, que se pide aparte y tarda lo que tarde el dispositivo en fijar posición.
/// </remarks>
/// <param name="PendientesSincronizar">Registros en cola esperando envío.</param>
/// <param name="Avisos">Avisos operativos sin atender.</param>
/// <param name="EvidenciaLocal">Archivos de evidencia guardados en el dispositivo.</param>
public sealed record ResumenOperativo(
	int PendientesSincronizar,
	int Avisos,
	int EvidenciaLocal)
{
	/// <summary>Resumen en cero, para el estado inicial de la pantalla.</summary>
	public static ResumenOperativo Vacio { get; } = new(0, 0, 0);
}
