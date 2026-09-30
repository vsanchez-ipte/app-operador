using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

// Sin token ni identidad: la revalidación no emite token. Todo anulable porque viene de la red.
public sealed class RespuestaRevalidacion
{
	[JsonPropertyName("sessionId")]
	public string? SessionId { get; init; }

	[JsonPropertyName("estado")]
	public string? Estado { get; init; }

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
