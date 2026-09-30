using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

public sealed class EnvioIncidenciasSimulado : IIncidenciasJacobClient
{
	public Task<ResultadoEnvio> RegistrarAsync(
		EnvioIncidencia incidencia,
		string accessToken,
		CancellationToken cancelacion = default) =>
		Task.FromResult(ResultadoEnvio.Rechazada(
			FamiliaErrorSincronizacion.Tecnico,
			"app.simulado.sin.envio",
			"El recorrido simulado no envía a Jacob."));
}
