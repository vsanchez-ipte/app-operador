using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface IAuthenticationService
{
	Task<IReadOnlyList<UnidadVehicular>> ObtenerUnidadesAsync(CancellationToken cancelacion = default);

	Task<ResultadoAcceso> IngresarAsync(
		string usuario,
		string contrasena,
		UnidadVehicular unidad,
		CancellationToken cancelacion = default);

	// Solo reanuda una sesión validada antes en línea; el primer ingreso siempre exige enlace.
	Task<ResultadoAcceso> ContinuarSinConexionAsync(CancellationToken cancelacion = default);
}
