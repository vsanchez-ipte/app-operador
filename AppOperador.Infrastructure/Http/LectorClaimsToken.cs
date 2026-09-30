using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Http;

public sealed class LectorClaimsToken : ITokenClaims
{
	public IReadOnlyList<string> ModulosDe(string? token) => ModulosDelToken.Leer(token);
}
