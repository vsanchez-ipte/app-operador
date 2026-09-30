using System.Text;
using System.Text.Json;

namespace AppOperador.IntegrationTests.Http;

internal static class TokenDePrueba
{
	public static string Con(params string[] modulos)
	{
		object claim = modulos.Length == 1 ? modulos[0] : modulos;
		return Armar(new Dictionary<string, object>
		{
			["sub"] = "op-1",
			["module"] = claim,
		});
	}

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
