using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

/// <summary>
/// Cuerpo de <c>POST ITS/AppLogin/Preauth</c>.
/// </summary>
/// <remarks>
/// <b>Los dos primeros campos van cifrados</b> con RSA-OAEP-SHA256 y codificados en Base64,
/// nunca en claro. El API los descifra con su llave privada; si llegan sin cifrar o con otro
/// relleno, responde <c>appoperador.credenciales.invalidas</c> sin más pistas.
/// </remarks>
public sealed class SolicitudPreauth
{
	/// <summary>Correo del operador, cifrado y en Base64.</summary>
	[JsonPropertyName("email")]
	public required string Email { get; init; }

	/// <summary>Contraseña del operador, cifrada y en Base64.</summary>
	[JsonPropertyName("password")]
	public required string Password { get; init; }

	/// <summary>
	/// Plataforma declarada: <c>Android</c> o <c>iOS</c>, máximo 20 caracteres.
	/// </summary>
	/// <remarks>Solo auditoría. No altera permisos ni el resultado de la validación.</remarks>
	[JsonPropertyName("plataforma")]
	public required string Plataforma { get; init; }
}
