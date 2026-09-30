namespace AppOperador.Infrastructure.Http;

// El mensaje nunca incluye la llave ni el texto en claro o cifrado.
public sealed class ErrorDeCifradoException : Exception
{
	public ErrorDeCifradoException(string mensaje)
		: base(mensaje)
	{
	}

	public ErrorDeCifradoException(string mensaje, Exception interna)
		: base(mensaje, interna)
	{
	}
}
