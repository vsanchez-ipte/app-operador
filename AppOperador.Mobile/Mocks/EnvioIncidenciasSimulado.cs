using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Envío simulado: nunca contacta a Jacob.
/// </summary>
/// <remarks>
/// El recorrido simulado se elimina completo en rama propia —decisión del 21-ago—: nadie lo
/// prueba y basta con que compile. <b>Rechaza como técnico en vez de fingir un folio</b>: un
/// folio inventado dejaría incidencias marcadas como sincronizadas que nunca llegaron a
/// ninguna parte, y eso es peor que no enviar.
/// </remarks>
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
