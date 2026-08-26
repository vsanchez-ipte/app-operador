using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

/// <summary>
/// Contenido de <c>resultado</c> cuando Jacob CCO revalida la sesión (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// <b>No trae token ni identidad del operador.</b> La revalidación no emite un token nuevo
/// —si el que hay está por vencer, toca autenticarse— y el nombre del operador ya lo tiene
/// la app desde el acceso. Lo que sí puede haber cambiado, y por eso viaja, son el rol, la
/// unidad, los permisos y la ventana offline.
/// </para>
/// <para>
/// Todo anulable a propósito: son datos de la red.
/// </para>
/// </remarks>
public sealed class RespuestaRevalidacion
{
	[JsonPropertyName("sessionId")]
	public string? SessionId { get; init; }

	/// <summary>Estado de la sesión tras revalidar. Hoy siempre <c>activa</c>.</summary>
	[JsonPropertyName("estado")]
	public string? Estado { get; init; }

	[JsonPropertyName("rol")]
	public RolLogin? Rol { get; init; }

	/// <summary>Unidad vigente. Puede haber cambiado de estado desde el acceso.</summary>
	[JsonPropertyName("unidad")]
	public UnidadPreauth? Unidad { get; init; }

	[JsonPropertyName("permisos")]
	public IReadOnlyList<string>? Permisos { get; init; }

	/// <summary>Nueva fecha de validación en línea, puesta por el servidor.</summary>
	[JsonPropertyName("lastValidatedAtUtc")]
	public DateTime? LastValidatedAtUtc { get; init; }

	/// <summary>Nueva vigencia offline, calculada por el servidor.</summary>
	[JsonPropertyName("offlineUntilUtc")]
	public DateTime? OfflineUntilUtc { get; init; }

	[JsonPropertyName("serverTimeUtc")]
	public DateTime? ServerTimeUtc { get; init; }
}
