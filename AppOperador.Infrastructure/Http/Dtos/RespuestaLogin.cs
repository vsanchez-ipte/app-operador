using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

/// <summary>
/// Contenido de <c>resultado</c> cuando Jacob CCO crea la sesión móvil.
/// </summary>
/// <remarks>
/// Todo es anulable a propósito: son datos de la red y el deserializador no garantiza nada.
/// La comprobación de lo imprescindible se hace al convertir, no aquí.
/// </remarks>
public sealed class RespuestaLogin
{
	/// <summary>Token del esquema <c>MobileBearer</c>. Vigencia de 8 horas.</summary>
	[JsonPropertyName("accessToken")]
	public string? AccessToken { get; init; }

	/// <summary>Caducidad del token, en UTC. Distinta de la vigencia offline.</summary>
	[JsonPropertyName("tokenExpiresAtUtc")]
	public DateTime? TokenExpiresAtUtc { get; init; }

	/// <summary>Identificador de la sesión, para trazas y soporte.</summary>
	[JsonPropertyName("sessionId")]
	public string? SessionId { get; init; }

	[JsonPropertyName("operador")]
	public OperadorLogin? Operador { get; init; }

	[JsonPropertyName("rol")]
	public RolLogin? Rol { get; init; }

	[JsonPropertyName("unidad")]
	public UnidadPreauth? Unidad { get; init; }

	/// <summary>Capacidades activas. Hoy siempre <c>["APP_OPERADOR_MOVIL"]</c>.</summary>
	[JsonPropertyName("permisos")]
	public IReadOnlyList<string>? Permisos { get; init; }

	/// <summary>Última validación en línea. Base del conteo offline.</summary>
	[JsonPropertyName("lastValidatedAtUtc")]
	public DateTime? LastValidatedAtUtc { get; init; }

	/// <summary>
	/// Hasta cuándo puede operarse sin conexión.
	/// </summary>
	/// <remarks>
	/// <b>Lo calcula el servidor.</b> La app lo adopta y no lo recalcula: si el reloj del
	/// teléfono está desfasado, el conteo propio sería incorrecto.
	/// </remarks>
	[JsonPropertyName("offlineUntilUtc")]
	public DateTime? OfflineUntilUtc { get; init; }

	/// <summary>Hora del servidor. Permite detectar desfase del reloj del dispositivo.</summary>
	[JsonPropertyName("serverTimeUtc")]
	public DateTime? ServerTimeUtc { get; init; }
}

/// <summary>Identidad del operador que devuelve el acceso.</summary>
public sealed class OperadorLogin
{
	[JsonPropertyName("id")]
	public string? Id { get; init; }

	[JsonPropertyName("email")]
	public string? Email { get; init; }

	/// <summary>Nombre visible. Es lo que se muestra en pantalla, no el correo.</summary>
	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}

/// <summary>Rol funcional con el que ingresó el operador.</summary>
public sealed class RolLogin
{
	[JsonPropertyName("id")]
	public int? Id { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}
