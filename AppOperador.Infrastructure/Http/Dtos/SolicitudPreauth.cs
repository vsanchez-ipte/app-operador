using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

// Email y password van cifrados con RSA-OAEP-SHA256 en Base64; sin cifrar, el API solo dice «credenciales inválidas».
public sealed class SolicitudPreauth
{
	[JsonPropertyName("email")]
	public required string Email { get; init; }

	[JsonPropertyName("password")]
	public required string Password { get; init; }

	// Solo auditoría.
	[JsonPropertyName("plataforma")]
	public required string Plataforma { get; init; }
}
