namespace AppOperador.Aplicacion.Interfaces;

public interface ITokenClaims
{
	IReadOnlyList<string> ModulosDe(string? token);
}
