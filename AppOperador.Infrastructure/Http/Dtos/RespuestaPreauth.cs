using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

/// <summary>
/// Contenido de <c>resultado</c> en una preautenticación correcta.
/// </summary>
public sealed class RespuestaPreauth
{
	/// <summary>
	/// Desafío de un solo uso, válido cinco minutos.
	/// </summary>
	/// <remarks>
	/// El servidor solo guarda su hash: no puede recuperarse ni consultarse después. Si la
	/// app lo pierde, hay que repetir la preautenticación.
	/// </remarks>
	[JsonPropertyName("challengeId")]
	public string? ChallengeId { get; init; }

	/// <summary>Caducidad del desafío, en UTC.</summary>
	[JsonPropertyName("expiresAtUtc")]
	public DateTime? ExpiresAtUtc { get; init; }

	/// <summary>
	/// Unidades activas asignadas al operador.
	/// </summary>
	/// <remarks>
	/// Si la lista llegara vacía, el API ya habría respondido <c>appoperador.sin.vehiculos</c>
	/// en lugar de un resultado correcto.
	/// </remarks>
	[JsonPropertyName("unidades")]
	public IReadOnlyList<UnidadPreauth> Unidades { get; init; } = [];
}

/// <summary>Unidad vehicular tal como la devuelve la preautenticación.</summary>
public sealed class UnidadPreauth
{
	/// <summary>Identificador de la unidad. Es el que consume el segundo paso del acceso.</summary>
	[JsonPropertyName("id")]
	public string? Id { get; init; }

	/// <summary>Clave visible, por ejemplo <c>VEH-01</c>.</summary>
	[JsonPropertyName("clave")]
	public string? Clave { get; init; }

	[JsonPropertyName("descripcion")]
	public string? Descripcion { get; init; }
}
