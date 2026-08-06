using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Primer paso del acceso contra el canal móvil de Jacob CCO:
/// <c>POST ITS/AppLogin/Preauth</c>.
/// </summary>
/// <remarks>
/// <para>
/// El nombre está en inglés por la convención del documento de arquitectura, que fija así
/// los contratos de la capa de aplicación.
/// </para>
/// <para>
/// <b>Por qué es un contrato aparte de <see cref="IAuthenticationService"/>.</b> Aquel
/// describe el acceso completo —preauth, login, refresh y logout— y su implementación real
/// llega hasta JTT-1380. JTT-1378 solo cubre la preautenticación, así que agregarla allí
/// dejaría a dos implementaciones cumpliendo el contrato a medias. Al aislarla, el
/// simulador sigue siendo coherente y el adaptador real no finge capacidades que no tiene.
/// JTT-1380 puede plegar este contrato dentro del otro cuando exista el flujo entero.
/// </para>
/// <para>
/// La implementación es responsable de cifrar las credenciales antes de enviarlas: quien
/// llama entrega email y contraseña en claro y nunca ve el texto cifrado.
/// </para>
/// </remarks>
public interface IPreauthClient
{
	/// <summary>
	/// Valida credenciales contra Jacob CCO y solicita un desafío de acceso.
	/// </summary>
	/// <param name="email">
	/// Correo del operador. Jacob autentica por email, no por nombre de usuario.
	/// </param>
	/// <param name="contrasena">Contraseña en claro. No se guarda ni se registra.</param>
	/// <remarks>
	/// No lanza excepciones por un rechazo ni por un fallo de red: ambos son desenlaces
	/// esperados y viajan dentro de <see cref="ResultadoPreauth"/>.
	/// </remarks>
	Task<ResultadoPreauth> PreautenticarAsync(
		string email,
		string contrasena,
		CancellationToken cancelacion = default);
}
