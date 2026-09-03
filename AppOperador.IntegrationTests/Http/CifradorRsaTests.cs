using System.Security.Cryptography;
using System.Text;
using AppOperador.Infrastructure.Http;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Comprueba el cifrado de credenciales contra una llave RSA real.
/// </summary>
/// <remarks>
/// Las pruebas generan su propio par de llaves y descifran con la privada. Así se verifica
/// el ciclo completo de verdad —formato de llave, relleno y codificación— en vez de dar por
/// bueno que la llamada no lanzó excepción.
/// </remarks>
public sealed class CifradorRsaTests
{
	private static (string LlavePublicaBase64, RSA Privada) GenerarPar()
	{
		var rsa = RSA.Create(2048);
		return (Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()), rsa);
	}

	[Fact]
	public void Importa_una_llave_SubjectPublicKeyInfo_en_Base64()
	{
		var (llave, privada) = GenerarPar();
		using var _ = privada;

		using var cifrador = CifradorRsa.DesdeBase64(llave);

		// 2048 bits menos la sobrecarga de OAEP-SHA256.
		Assert.Equal(190, cifrador.LimiteDeBytes);
	}

	[Fact]
	public void Lo_cifrado_se_descifra_con_OAEP_SHA256_y_devuelve_el_texto_original()
	{
		var (llave, privada) = GenerarPar();
		using var _ = privada;
		using var cifrador = CifradorRsa.DesdeBase64(llave);

		var cifrado = cifrador.Cifrar("operador.prueba@ipte.com.mx");

		var descifrado = Encoding.UTF8.GetString(
			privada.Decrypt(Convert.FromBase64String(cifrado), RSAEncryptionPadding.OaepSHA256));

		Assert.Equal("operador.prueba@ipte.com.mx", descifrado);
	}

	[Fact]
	public void El_relleno_debe_ser_OAEP_SHA256_y_no_otro()
	{
		var (llave, privada) = GenerarPar();
		using var _ = privada;
		using var cifrador = CifradorRsa.DesdeBase64(llave);

		var cifrado = Convert.FromBase64String(cifrador.Cifrar("secreto"));

		// Si el cliente usara PKCS#1 u OAEP-SHA1, el API no podría descifrar y respondería
		// 'credenciales.invalidas' sin explicar por qué. Esta prueba fija el relleno.
		Assert.Throws<CryptographicException>(() => privada.Decrypt(cifrado, RSAEncryptionPadding.Pkcs1));
		Assert.Throws<CryptographicException>(() => privada.Decrypt(cifrado, RSAEncryptionPadding.OaepSHA1));
	}

	[Fact]
	public void Email_y_contrasena_se_cifran_por_separado_y_dan_bloques_distintos()
	{
		var (llave, privada) = GenerarPar();
		using var _ = privada;
		using var cifrador = CifradorRsa.DesdeBase64(llave);

		var email = cifrador.Cifrar("operador@ipte.com.mx");
		var contrasena = cifrador.Cifrar("operador@ipte.com.mx");

		// Mismo texto, resultados distintos: OAEP añade relleno aleatorio. Por eso el texto
		// cifrado no sirve como identificador ni puede compararse entre peticiones.
		Assert.NotEqual(email, contrasena);
	}

	[Fact]
	public void Rechaza_una_llave_que_no_es_Base64()
	{
		// Caso real: pasar el cuerpo entero de la respuesta en vez de extraer 'resultado'.
		var error = Assert.Throws<ErrorDeCifradoException>(
			() => CifradorRsa.DesdeBase64("""{"resultado":"MIIBIjAN..."}"""));

		Assert.Contains("Envelope", error.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Rechaza_una_llave_que_no_es_SubjectPublicKeyInfo()
	{
		using var rsa = RSA.Create(2048);

		// PKCS#1 en vez de X.509: es Base64 válido pero no el formato que espera el contrato.
		var pkcs1 = Convert.ToBase64String(rsa.ExportRSAPublicKey());

		var error = Assert.Throws<ErrorDeCifradoException>(() => CifradorRsa.DesdeBase64(pkcs1));
		Assert.Contains("SubjectPublicKeyInfo", error.Message, StringComparison.Ordinal);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Rechaza_una_llave_ausente(string? llave)
	{
		Assert.Throws<ErrorDeCifradoException>(() => CifradorRsa.DesdeBase64(llave));
	}

	[Fact]
	public void Rechaza_un_texto_mas_largo_de_lo_que_cabe_sin_revelarlo()
	{
		var (llave, privada) = GenerarPar();
		using var _ = privada;
		using var cifrador = CifradorRsa.DesdeBase64(llave);

		var contrasenaEnorme = new string('a', 300);

		var error = Assert.Throws<ErrorDeCifradoException>(() => cifrador.Cifrar(contrasenaEnorme));

		// El mensaje informa el tamaño, nunca el valor: es una credencial.
		Assert.DoesNotContain(contrasenaEnorme, error.Message, StringComparison.Ordinal);
		Assert.Contains("190", error.Message, StringComparison.Ordinal);
	}
}
