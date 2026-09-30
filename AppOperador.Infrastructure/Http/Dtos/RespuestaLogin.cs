using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

// Todo anulable: lo imprescindible se comprueba al convertir.
public sealed class RespuestaLogin
{
	[JsonPropertyName("accessToken")]
	public string? AccessToken { get; init; }

	[JsonPropertyName("tokenExpiresAtUtc")]
	public DateTime? TokenExpiresAtUtc { get; init; }

	[JsonPropertyName("sessionId")]
	public string? SessionId { get; init; }

	[JsonPropertyName("operador")]
	public OperadorLogin? Operador { get; init; }

	[JsonPropertyName("rol")]
	public RolLogin? Rol { get; init; }

	[JsonPropertyName("unidad")]
	public UnidadPreauth? Unidad { get; init; }

	[JsonPropertyName("permisos")]
	public IReadOnlyList<string>? Permisos { get; init; }

	[JsonPropertyName("lastValidatedAtUtc")]
	public DateTime? LastValidatedAtUtc { get; init; }

	[JsonPropertyName("offlineUntilUtc")]
	public DateTime? OfflineUntilUtc { get; init; }

	[JsonPropertyName("serverTimeUtc")]
	public DateTime? ServerTimeUtc { get; init; }
}

public sealed class OperadorLogin
{
	[JsonPropertyName("id")]
	public string? Id { get; init; }

	[JsonPropertyName("email")]
	public string? Email { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}

public sealed class RolLogin
{
	[JsonPropertyName("id")]
	public int? Id { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}
