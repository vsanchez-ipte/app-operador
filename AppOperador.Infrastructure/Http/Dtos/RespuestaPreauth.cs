using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

public sealed class RespuestaPreauth
{
	// El servidor solo guarda su hash: si la app lo pierde, se repite la preautenticación.
	[JsonPropertyName("challengeId")]
	public string? ChallengeId { get; init; }

	[JsonPropertyName("expiresAtUtc")]
	public DateTime? ExpiresAtUtc { get; init; }

	[JsonPropertyName("unidades")]
	public IReadOnlyList<UnidadPreauth> Unidades { get; init; } = [];
}

public sealed class UnidadPreauth
{
	[JsonPropertyName("id")]
	public string? Id { get; init; }

	[JsonPropertyName("clave")]
	public string? Clave { get; init; }

	[JsonPropertyName("descripcion")]
	public string? Descripcion { get; init; }
}
