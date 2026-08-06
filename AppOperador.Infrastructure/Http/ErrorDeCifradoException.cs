namespace AppOperador.Infrastructure.Http;

/// <summary>
/// La llave pública de Jacob CCO no se pudo interpretar o el cifrado falló.
/// </summary>
/// <remarks>
/// <para>
/// Es una condición excepcional de verdad, no un desenlace esperado: significa que el API
/// devolvió algo que no es una llave RSA utilizable. El cliente de preautenticación la
/// captura y la traduce a <c>MotivoRechazoAcceso.ErrorDelServicio</c>, para que nunca
/// llegue cruda a la interfaz.
/// </para>
/// <para>
/// <b>Nunca incluir la llave, el texto en claro ni el texto cifrado en el mensaje.</b>
/// </para>
/// </remarks>
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
