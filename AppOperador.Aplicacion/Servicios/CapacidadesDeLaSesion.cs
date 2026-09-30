using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

public sealed class CapacidadesDeLaSesion
{
	private readonly ISessionStore _sesiones;

	public CapacidadesDeLaSesion(ISessionStore sesiones)
	{
		_sesiones = sesiones;
	}

	// Se consulta cada vez: la sesión puede vencer con la pantalla abierta.
	public bool Puede(CapacidadOperador capacidad) =>
		ReglaCapacidades.Concede(_sesiones.Actual?.Permisos, capacidad);
}
