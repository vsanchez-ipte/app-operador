using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

/// <summary>
/// Cuerpo de <c>POST ITS/AppLogin</c>, el segundo paso del acceso.
/// </summary>
/// <remarks>
/// El endpoint es anónimo: el desafío hace de credencial. No viaja ningún dato del operador,
/// así que aquí no hay nada que cifrar.
/// </remarks>
public sealed class SolicitudLogin
{
	/// <summary>Desafío emitido por la preautenticación. De un solo uso, cinco minutos.</summary>
	[JsonPropertyName("challengeId")]
	public required string ChallengeId { get; init; }

	/// <summary>
	/// Identificador técnico de la unidad elegida, tomado del catálogo del paso 1.
	/// </summary>
	/// <remarks>
	/// Es el <c>id</c>, no la clave visible: el API revalida la unidad contra él
	/// (JTT-1381 CA 6 y CA 7).
	/// </remarks>
	[JsonPropertyName("unidadId")]
	public required string UnidadId { get; init; }
}
