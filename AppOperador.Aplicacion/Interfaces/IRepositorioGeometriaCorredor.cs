using AppOperador.Domain.Entidades;

namespace AppOperador.Aplicacion.Interfaces;

public interface IRepositorioGeometriaCorredor
{
	// Nunca devuelve nulo: una implementación sin geometría debe fallar al construirse.
	Task<GeometriaCorredor> ObtenerAsync(CancellationToken cancelacion = default);
}
