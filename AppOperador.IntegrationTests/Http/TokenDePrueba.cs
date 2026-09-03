using System.Text;
using System.Text.Json;

namespace AppOperador.IntegrationTests.Http;

/// <summary>
/// Arma tokens con la forma de los que emite Jacob CCO, para las pruebas.
/// </summary>
/// <remarks>
/// <para>
/// La firma es un relleno cualquiera y no pretende ser válida: la app no la verifica —no
/// tiene el secreto— y lo que se ejercita aquí es el cotejo del claim <c>module</c> contra
/// los permisos del cuerpo (JTT-1379 CA 8).
/// </para>
/// <para>
/// Existe para que las pruebas no lleven un token literal ilegible: leer
/// <c>TokenDePrueba.Con("APP_OPERADOR_MOVIL")</c> dice qué se está probando; una cadena
/// Base64 de ochenta caracteres, no.
/// </para>
/// </remarks>
internal static class TokenDePrueba
{
	/// <summary>Token cuyo claim <c>module</c> lleva los módulos indicados.</summary>
	public static string Con(params string[] modulos)
	{
		object claim = modulos.Length == 1 ? modulos[0] : modulos;
		return Armar(new Dictionary<string, object>
		{
			["sub"] = "op-1",
			["module"] = claim,
		});
	}

	/// <summary>Token bien formado pero sin ningún claim <c>module</c>.</summary>
	public static string SinModulo() =>
		Armar(new Dictionary<string, object> { ["sub"] = "op-1" });

	private static string Armar(Dictionary<string, object> cuerpo)
	{
		var encabezado = Base64Url("""{"alg":"HS256","typ":"JWT"}"""u8.ToArray());
		var carga = Base64Url(JsonSerializer.SerializeToUtf8Bytes(cuerpo));

		return $"{encabezado}.{carga}.firma-de-prueba";
	}

	private static string Base64Url(byte[] bytes) =>
		Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
