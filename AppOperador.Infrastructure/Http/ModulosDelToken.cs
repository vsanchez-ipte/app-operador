using System.Text;
using System.Text.Json;

namespace AppOperador.Infrastructure.Http;

// No verifica la firma: la app no tiene el secreto. Un token ilegible da lista vacía y la sesión no se abre.
internal static class ModulosDelToken
{
	private const string ClaimModulo = "module";

	public static IReadOnlyList<string> Leer(string? token)
	{
		if (string.IsNullOrWhiteSpace(token))
		{
			return [];
		}

		var partes = token.Split('.');
		if (partes.Length != 3)
		{
			return [];
		}

		try
		{
			var json = Encoding.UTF8.GetString(DesdeBase64Url(partes[1]));
			using var documento = JsonDocument.Parse(json);

			if (!documento.RootElement.TryGetProperty(ClaimModulo, out var modulo))
			{
				return [];
			}

			// Un módulo llega como cadena; varios, como arreglo.
			return modulo.ValueKind switch
			{
				JsonValueKind.String => [modulo.GetString()!],
				JsonValueKind.Array =>
				[
					.. modulo.EnumerateArray()
						.Where(elemento => elemento.ValueKind == JsonValueKind.String)
						.Select(elemento => elemento.GetString()!)
				],
				_ => [],
			};
		}
		catch (Exception excepcion) when (excepcion is FormatException or JsonException or DecoderFallbackException)
		{
			return [];
		}
	}

	private static byte[] DesdeBase64Url(string valor)
	{
		var base64 = valor.Replace('-', '+').Replace('_', '/');

		return Convert.FromBase64String(base64.PadRight(
			base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
	}
}
