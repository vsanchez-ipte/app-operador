using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

public sealed class ServicioPermisoUbicacionSimulado : ILocationPermissionService
{
	public EstadoUbicacion Estado { get; set; } = EstadoUbicacion.Concedido;

	public EstadoUbicacion EstadoTrasSolicitar { get; set; } = EstadoUbicacion.Concedido;

	public Task<EstadoUbicacion> ConsultarEstadoAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(Estado);

	public Task<EstadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default)
	{
		Estado = EstadoTrasSolicitar;
		return Task.FromResult(Estado);
	}

	public Task<bool> AbrirAjustesDeLaAppAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(false);

	public Task<bool> AbrirAjustesDeUbicacionAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(false);
}
