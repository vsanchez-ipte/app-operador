using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Sesión guardada solo en memoria.
/// </summary>
/// <remarks>
/// Sirve para recorrer las pantallas, pero **no** cumple el requisito de que la sesión
/// sobreviva al cierre de la app: eso llega con SQLite y <c>SecureStorage</c> en
/// JTT-1345. Al reemplazarla, los tokens nunca deben quedar en SQLite.
/// </remarks>
public sealed class AlmacenSesionEnMemoria : ISessionStore
{
	public SesionOperador? Actual { get; private set; }

	public void Guardar(SesionOperador sesion) => Actual = sesion;

	public void Limpiar() => Actual = null;
}
