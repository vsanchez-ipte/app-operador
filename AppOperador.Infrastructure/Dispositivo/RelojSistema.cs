using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Dispositivo;

// Único punto que lee DateTime.UtcNow.
public sealed class RelojSistema : IClock
{
	public DateTime UtcAhora => DateTime.UtcNow;
}
