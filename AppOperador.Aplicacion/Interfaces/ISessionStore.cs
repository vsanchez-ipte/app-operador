using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface ISessionStore
{
	SesionOperador? Actual { get; }

	void Guardar(SesionOperador sesion);

	// No borra los registros pendientes.
	void Limpiar();
}
