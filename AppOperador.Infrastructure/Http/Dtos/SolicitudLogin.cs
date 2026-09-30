using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

public sealed class SolicitudLogin
{
	[JsonPropertyName("challengeId")]
	public required string ChallengeId { get; init; }

	// El id técnico, no la clave visible: el API revalida contra él.
	[JsonPropertyName("unidadId")]
	public required string UnidadId { get; init; }
}
