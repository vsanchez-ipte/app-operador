using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

public sealed class AlmacenSesionEnMemoria : ISessionStore
{
	public SesionOperador? Actual { get; private set; }

	public void Guardar(SesionOperador sesion) => Actual = sesion;

	public void Limpiar() => Actual = null;
}
