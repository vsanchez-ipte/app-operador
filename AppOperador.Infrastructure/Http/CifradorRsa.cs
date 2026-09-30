using System.Security.Cryptography;
using System.Text;

namespace AppOperador.Infrastructure.Http;

// Llave SubjectPublicKeyInfo en Base64, relleno OAEP-SHA256 y salida en Base64: el API no acepta otra cosa.
public sealed class CifradorRsa : IDisposable
{
	// Con RSA-2048 caben 190 bytes; se comprueba para fallar con un mensaje claro.
	private const int SobrecargaOaepSha256 = (2 * 32) + 2;

	private readonly RSA _rsa;

	private CifradorRsa(RSA rsa) => _rsa = rsa;

	public int LimiteDeBytes => (_rsa.KeySize / 8) - SobrecargaOaepSha256;

	public static CifradorRsa DesdeBase64(string? llavePublicaBase64)
	{
		if (string.IsNullOrWhiteSpace(llavePublicaBase64))
		{
			throw new ErrorDeCifradoException("Jacob CCO no devolvió una llave pública.");
		}

		byte[] derivada;
		try
		{
			derivada = Convert.FromBase64String(llavePublicaBase64);
		}
		catch (FormatException excepcion)
		{
			// Caso típico: se decodificó el cuerpo entero en vez de 'resultado'.
			throw new ErrorDeCifradoException(
				"La llave pública no viene en Base64 válido. ¿Se extrajo 'resultado' del Envelope?",
				excepcion);
		}

		var rsa = RSA.Create();
		try
		{
			rsa.ImportSubjectPublicKeyInfo(derivada, out _);
		}
		catch (CryptographicException excepcion)
		{
			rsa.Dispose();
			throw new ErrorDeCifradoException(
				"La llave pública no tiene formato SubjectPublicKeyInfo.",
				excepcion);
		}

		return new CifradorRsa(rsa);
	}

	// OAEP es aleatorio: el mismo texto da un cifrado distinto cada vez.
	public string Cifrar(string textoEnClaro)
	{
		ArgumentNullException.ThrowIfNull(textoEnClaro);

		var bytes = Encoding.UTF8.GetBytes(textoEnClaro);
		if (bytes.Length > LimiteDeBytes)
		{
			// Sin incluir el texto: es una credencial.
			throw new ErrorDeCifradoException(
				$"El valor a cifrar ocupa {bytes.Length} bytes y el máximo con esta llave es {LimiteDeBytes}.");
		}

		return Convert.ToBase64String(_rsa.Encrypt(bytes, RSAEncryptionPadding.OaepSHA256));
	}

	public void Dispose() => _rsa.Dispose();
}
