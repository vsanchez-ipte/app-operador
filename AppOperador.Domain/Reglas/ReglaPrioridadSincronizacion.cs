using AppOperador.Domain.Enums;

namespace AppOperador.Domain.Reglas;

public static class ReglaPrioridadSincronizacion
{
	// El catálogo ordena de más grave a menos grave, empezando en 1.
	private const int OrdenDelNivelCritico = 1;

	// Con <= un catálogo que numere desde 0 sigue marcando crítico su nivel más grave.
	public static SyncPriority Para(int ordenDeSeveridad) =>
		ordenDeSeveridad <= OrdenDelNivelCritico ? SyncPriority.Critica : SyncPriority.Normal;
}
