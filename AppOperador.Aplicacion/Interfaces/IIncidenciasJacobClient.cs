using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface IIncidenciasJacobClient
{
	// Idempotente por uuid: YaExistia es éxito, así que no hace falta evitar el reenvío.
	Task<ResultadoEnvio> RegistrarAsync(
		EnvioIncidencia incidencia,
		string accessToken,
		CancellationToken cancelacion = default);
}
