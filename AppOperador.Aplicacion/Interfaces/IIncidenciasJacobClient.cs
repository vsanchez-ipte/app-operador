using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Canal con el endpoint de creación de incidencias del canal móvil de Jacob.
/// </summary>
/// <remarks>
/// <para>
/// El nombre sigue la convención en inglés de <c>Aplicacion\Interfaces</c>, fijada por el
/// documento de arquitectura, igual que <see cref="IAccesoJacobClient"/>.
/// </para>
/// <para>
/// <b>Nunca lanza por un rechazo de Jacob.</b> Un registro rechazado es la mitad del trabajo de
/// sincronizar, no una excepción, y devolverlo como <see cref="ResultadoEnvio"/> es lo que
/// permite que el CA 13 —una falla no bloquea a las demás— no dependa de acordarse de envolver
/// cada envío en un <c>try</c>. Los fallos de red y los tiempos agotados también salen por aquí,
/// como familia técnica.
/// </para>
/// </remarks>
public interface IIncidenciasJacobClient
{
	/// <summary>
	/// Registra una incidencia en Jacob.
	/// </summary>
	/// <remarks>
	/// <b>Es idempotente por el <c>uuid</c> del envío.</b> Reenviar el mismo devuelve el mismo
	/// folio con <see cref="IncidenciaRegistrada.YaExistia"/> en <see langword="true"/>, y eso es
	/// un éxito: significa que la respuesta anterior se perdió en el camino. <b>No hay que
	/// construir lógica para evitar el reenvío</b>, el servidor lo cubre.
	/// </remarks>
	Task<ResultadoEnvio> RegistrarAsync(
		EnvioIncidencia incidencia,
		string accessToken,
		CancellationToken cancelacion = default);
}
