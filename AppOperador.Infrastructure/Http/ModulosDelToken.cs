using System.Text;
using System.Text.Json;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Lee el claim <c>module</c> del token que emite Jacob CCO (JTT-1379 CA 8).
/// </summary>
/// <remarks>
/// <para>
/// El token es un JWT: tres partes separadas por puntos, la de en medio un JSON en
/// Base64Url. Solo se necesita esa parte, y solo la propiedad <c>module</c>, que es donde
/// Jacob pone el permiso funcional de la App Operador.
/// </para>
/// <para>
/// <b>Aquí no se verifica la firma.</b> El token va firmado con un secreto que la app no
/// tiene ni debe tener, así que comprobarla es imposible en el dispositivo. Quien manda es
/// el servidor, que la valida en cada petición. Lo que este cotejo aporta es que la app no
/// conceda capacidades que su propio token no menciona.
/// </para>
/// <para>
/// Cualquier token con forma inesperada devuelve una lista vacía en vez de lanzar: el
/// resultado será que la sesión no se abre, que es la salida correcta ante algo ilegible.
/// </para>
/// </remarks>
internal static class ModulosDelToken
{
	private const string ClaimModulo = "module";

	/// <summary>Módulos que el token declara, o vacío si no se pudo leer.</summary>
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

			// Un solo claim llega como cadena; varios, como arreglo. El emisor de hoy manda
			// uno, pero el formato admite ambos y distinguirlo aquí evita una sorpresa el día
			// que Jacob conceda más de un módulo.
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

	/// <summary>
	/// Convierte Base64Url a bytes.
	/// </summary>
	/// <remarks>
	/// Base64Url cambia <c>+</c> por <c>-</c> y <c>/</c> por <c>_</c>, y omite el relleno.
	/// <c>Convert.FromBase64String</c> exige longitud múltiplo de cuatro, así que se repone.
	/// </remarks>
	private static byte[] DesdeBase64Url(string valor)
	{
		var base64 = valor.Replace('-', '+').Replace('_', '/');

		return Convert.FromBase64String(base64.PadRight(
			base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
	}
}
