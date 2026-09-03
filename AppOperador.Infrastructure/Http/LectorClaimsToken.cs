using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Adaptador que expone a la capa de aplicación los módulos que firma el token.
/// </summary>
/// <remarks>
/// Toda la mecánica —que el token sea un JWT, que la carga vaya en Base64Url, que el claim
/// se llame <c>module</c>— se queda de este lado. La aplicación solo pregunta qué módulos
/// hay y los coteja contra los permisos.
/// </remarks>
public sealed class LectorClaimsToken : ITokenClaims
{
	/// <inheritdoc />
	public IReadOnlyList<string> ModulosDe(string? token) => ModulosDelToken.Leer(token);
}
