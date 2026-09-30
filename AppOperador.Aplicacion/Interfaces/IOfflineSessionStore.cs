using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// Sin token: la base local viaja en los respaldos del dispositivo; el token vive en ITokenProvider.
public interface IOfflineSessionStore
{
	Task GuardarAsync(SesionOfflinePersistida sesion, CancellationToken cancelacion = default);

	Task<SesionOfflinePersistida?> ObtenerAsync(CancellationToken cancelacion = default);

	// No borra los registros pendientes.
	Task LimpiarAsync(CancellationToken cancelacion = default);
}
