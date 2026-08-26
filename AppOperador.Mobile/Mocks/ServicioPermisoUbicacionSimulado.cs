using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Ubicación simulada para los destinos sin API de permisos (escritorio) y para demostrar
/// los estados sin depender de la configuración real del dispositivo.
/// </summary>
/// <remarks>
/// <para>
/// El estado arranca en <see cref="EstadoUbicacion.Concedido"/> para que la app siga siendo
/// recorrible de punta a punta en Windows, que es donde se demuestran las cinco pantallas.
/// Cambiando <see cref="Estado"/> se ejercita cualquiera de los seis casos de JTT-1380 sin
/// tocar el teléfono.
/// </para>
/// <para>
/// No confundir con <see cref="ServicioUbicacionSimulado"/>: aquel simula la lectura de GPS
/// que alimenta el kilómetro, este el permiso que condiciona el acceso.
/// </para>
/// </remarks>
public sealed class ServicioPermisoUbicacionSimulado : ILocationPermissionService
{
	/// <summary>Estado que se devuelve al consultar.</summary>
	public EstadoUbicacion Estado { get; set; } = EstadoUbicacion.Concedido;

	/// <summary>Estado al que pasa cuando la app solicita el permiso.</summary>
	/// <remarks>
	/// Ponerlo en <see cref="EstadoUbicacion.Rechazado"/> reproduce la negativa del operador.
	/// </remarks>
	public EstadoUbicacion EstadoTrasSolicitar { get; set; } = EstadoUbicacion.Concedido;

	public Task<EstadoUbicacion> ConsultarEstadoAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(Estado);

	public Task<EstadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default)
	{
		Estado = EstadoTrasSolicitar;
		return Task.FromResult(Estado);
	}

	// En escritorio no hay configuración del sistema que abrir. Se responde que no se pudo
	// en vez de fingir que sí: la pantalla avisa y el operador no se queda esperando.
	public Task<bool> AbrirAjustesDeLaAppAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(false);

	public Task<bool> AbrirAjustesDeUbicacionAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(false);
}
