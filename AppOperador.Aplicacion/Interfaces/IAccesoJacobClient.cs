using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Los dos pasos del acceso contra el canal móvil de Jacob CCO:
/// <c>POST ITS/AppLogin/Preauth</c> y <c>POST ITS/AppLogin</c>.
/// </summary>
/// <remarks>
/// <para>
/// El nombre está en inglés por la convención del documento de arquitectura, que fija así
/// los contratos de la capa de aplicación.
/// </para>
/// <para>
/// <b>Sustituye a <c>IPreauthClient</c>.</b> Aquel cubría solo la preautenticación porque en
/// JTT-1378 no existía el segundo paso, y su propia documentación anunciaba que se plegaría
/// cuando el flujo estuviera completo. Ese momento es JTT-1382: mantener dos contratos para
/// el mismo endpoint y el mismo cliente solo obligaría a inyectar dos cosas donde hay una.
/// </para>
/// <para>
/// Sigue separado de <see cref="IAuthenticationService"/>, que describe el acceso como una
/// sola operación con usuario y contraseña. Contra Jacob son dos llamadas con un desafío en
/// medio, y forzarlas dentro de aquella forma obligaría a guardar el desafío en un sitio
/// donde no debe estar.
/// </para>
/// <para>
/// Ninguno de los dos métodos lanza excepciones por un rechazo ni por un fallo de red: ambos
/// son desenlaces esperados y viajan dentro del resultado.
/// </para>
/// </remarks>
public interface IAccesoJacobClient
{
	/// <summary>
	/// Paso 1: valida credenciales contra Jacob CCO y solicita un desafío de acceso.
	/// </summary>
	/// <param name="email">
	/// Correo del operador. Jacob autentica por email, no por nombre de usuario.
	/// </param>
	/// <param name="contrasena">Contraseña en claro. No se guarda ni se registra.</param>
	/// <remarks>
	/// La implementación es responsable de cifrar las credenciales antes de enviarlas: quien
	/// llama entrega email y contraseña en claro y nunca ve el texto cifrado.
	/// </remarks>
	Task<ResultadoPreauth> PreautenticarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Paso 2: consume el desafío junto con la unidad elegida y abre la sesión móvil.
	/// </summary>
	/// <param name="challengeId">
	/// Desafío emitido por el paso 1. Es la credencial de esta llamada: el endpoint es
	/// anónimo y no vuelve a pedir contraseña.
	/// </param>
	/// <param name="unidadId">
	/// Identificador técnico de la unidad, tomado del catálogo que devolvió el paso 1.
	/// </param>
	/// <remarks>
	/// El desafío es de un solo uso y dura cinco minutos. Si Jacob lo rechaza, no sirve
	/// reintentar con el mismo: hay que repetir la preautenticación.
	/// </remarks>
	Task<ResultadoLogin> CompletarAccesoAsync(
		string challengeId,
		string unidadId,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Avisa a Jacob CCO que la sesión terminó, para que revoque su token (JTT-1390).
	/// </summary>
	/// <param name="accessToken">Token de la sesión que se está cerrando.</param>
	/// <returns>
	/// <see langword="true"/> si Jacob confirmó la revocación; <see langword="false"/> si no
	/// se le pudo avisar.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>No avisar no es un fallo.</b> El criterio pide que el cierre local ocurra de todas
	/// formas: en campo lo normal es quedarse sin señal, y dejar al operador con la sesión
	/// abierta porque el servidor no contesta sería justo lo contrario de lo que se busca.
	/// Por eso devuelve un booleano en vez de lanzar.
	/// </para>
	/// <para>
	/// La llamada es idempotente del lado del servidor.
	/// </para>
	/// </remarks>
	Task<bool> CerrarSesionAsync(string accessToken, CancellationToken cancelacion = default);
}
