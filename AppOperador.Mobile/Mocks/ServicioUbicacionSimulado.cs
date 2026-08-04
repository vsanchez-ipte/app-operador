using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Ubicación simulada.
/// </summary>
/// <remarks>
/// Devuelve permiso concedido y una lectura fija dentro del corredor. La implementación
/// real usará <c>Geolocation</c> de MAUI y necesitará la geometría del corredor, que
/// sigue abierta (DA-12); mientras tanto, esto permite ejercitar la ruta de GPS
/// disponible y, alternando <see cref="DevuelveLectura"/>, también la de captura manual.
/// </remarks>
public sealed class ServicioUbicacionSimulado : ILocationService
{
	/// <summary>Permite forzar el caso "sin lectura válida" para probar la captura manual.</summary>
	public bool DevuelveLectura { get; set; } = true;

	/// <summary>Permite forzar el rechazo por ubicación no autorizada.</summary>
	public bool PermisoConcedido { get; set; } = true;

	public Task<bool> HayPermisoAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(PermisoConcedido);

	public Task<Kilometer?> ObtenerKilometroAsync(CancellationToken cancelacion = default)
	{
		if (!DevuelveLectura || !PermisoConcedido)
		{
			return Task.FromResult<Kilometer?>(null);
		}

		return Task.FromResult<Kilometer?>(Kilometer.Crear("130+200"));
	}
}
