using System.Security.Cryptography;
using System.Text;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Cifra credenciales con la llave pública de Jacob CCO, igual que el canal web.
/// </summary>
/// <remarks>
/// <para>
/// Criptografía pura, sin HTTP: recibe la llave ya obtenida y solo cifra. Así el formato de
/// la llave y el relleno se pueden probar con vectores conocidos, sin levantar un servidor.
/// Quien trae la llave del API es <c>ClientePreauthJacob</c>.
/// </para>
/// <para>
/// <b>Las tres decisiones que el API no perdona</b>, porque descifra con exactamente estos
/// parámetros y ante cualquier desviación responde
/// <c>appoperador.credenciales.invalidas</c> sin decir por qué:
/// </para>
/// <list type="number">
///   <item>La llave es <b>SubjectPublicKeyInfo</b> (X.509) DER en Base64, sin encabezados
///   PEM. Va con <see cref="RSA.ImportSubjectPublicKeyInfo"/>; con
///   <c>ImportRSAPublicKey</c>, que espera PKCS#1, revienta.</item>
///   <item>El relleno es <b>OAEP con SHA-256</b>. Ni PKCS#1 ni OAEP-SHA1.</item>
///   <item>El resultado se envía en <b>Base64</b>, no como bytes crudos.</item>
/// </list>
/// </remarks>
public sealed class CifradorRsa : IDisposable
{
	// OAEP consume 2 · tamañoHash + 2 bytes de relleno. Con RSA de 2048 bits (256 bytes) y
	// SHA-256 quedan 256 - 2·32 - 2 = 190 bytes útiles. Un email o una contraseña normales
	// caben de sobra; el límite se comprueba para fallar con un mensaje claro en vez de con
	// una excepción críptica de la plataforma.
	private const int SobrecargaOaepSha256 = (2 * 32) + 2;

	private readonly RSA _rsa;

	private CifradorRsa(RSA rsa) => _rsa = rsa;

	/// <summary>Bytes que caben en un solo bloque con esta llave.</summary>
	public int LimiteDeBytes => (_rsa.KeySize / 8) - SobrecargaOaepSha256;

	/// <summary>
	/// Construye el cifrador a partir de la llave pública en Base64 tal como la devuelve
	/// <c>GET ITS/Login/GetPublicKey</c> dentro de <c>resultado</c>.
	/// </summary>
	/// <exception cref="ErrorDeCifradoException">
	/// La cadena no es Base64 válido o no es una llave <c>SubjectPublicKeyInfo</c>.
	/// </exception>
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
			// Caso típico: se decodificó el cuerpo entero en vez de extraer 'resultado'
			// del Envelope, así que llega JSON donde debería haber Base64.
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

	/// <summary>
	/// Cifra un texto con OAEP-SHA256 y lo devuelve en Base64.
	/// </summary>
	/// <remarks>
	/// Cada llamada produce un resultado distinto aunque el texto sea el mismo: OAEP
	/// incorpora relleno aleatorio, y por eso el texto cifrado no puede compararse ni
	/// usarse como identificador.
	/// </remarks>
	/// <exception cref="ErrorDeCifradoException">El texto excede lo que cabe en un bloque.</exception>
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
