using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface ILocationPermissionService
{
	Task<EstadoUbicacion> ConsultarEstadoAsync(CancellationToken cancelacion = default);

	// Devuelve el estado reevaluado: con el permiso concedido, el servicio puede seguir apagado.
	Task<EstadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default);

	Task<bool> AbrirAjustesDeLaAppAsync(CancellationToken cancelacion = default);

	Task<bool> AbrirAjustesDeUbicacionAsync(CancellationToken cancelacion = default);
}
