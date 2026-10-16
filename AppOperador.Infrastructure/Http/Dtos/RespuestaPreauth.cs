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

	// Un API anterior no lo manda: entonces todas se pueden elegir.
	[JsonPropertyName("disponible")]
	public bool Disponible { get; init; } = true;

	[JsonPropertyName("estado")]
	public EstadoUnidadPreauth? Estado { get; init; }
}

public sealed class EstadoUnidadPreauth
{
	[JsonPropertyName("clave")]
	public string? Clave { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }

	[JsonPropertyName("color")]
	public string? Color { get; init; }
}
