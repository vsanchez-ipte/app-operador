using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Reloj real del dispositivo.
/// </summary>
/// <remarks>
/// No es un simulador: es la implementación definitiva y el único punto de la app que
/// consulta <see cref="DateTime.UtcNow"/>. Vive aquí de forma temporal porque
/// <c>Infrastructure</c> todavía no tiene su composición.
/// </remarks>
public sealed class RelojSistema : IClock
{
	public DateTime UtcAhora => DateTime.UtcNow;
}
