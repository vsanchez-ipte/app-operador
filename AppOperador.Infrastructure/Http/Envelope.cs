using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Sobre con el que el API de Jacob envuelve <b>todas</b> sus respuestas.
/// </summary>
/// <remarks>
/// <para>
/// Sin excepciones, incluida <c>GetPublicKey</c>: la llave pública viaja dentro de
/// <see cref="Resultado"/>, no como texto plano. Una versión anterior del contrato afirmaba
/// lo contrario y es falso; intentar decodificar el cuerpo completo como Base64 termina en
/// <see cref="FormatException"/>.
/// </para>
/// <para>
/// En caso de fallo funcional el API responde <b>HTTP 400</b> con <see cref="Resultado"/>
/// nulo y el código en <see cref="CodigoError"/>. No todos los errores llegan con esta
/// forma: ver <c>ClientePreauthJacob</c> para los que se salen del catálogo.
/// </para>
/// </remarks>
/// <typeparam name="T">Tipo del contenido útil de la respuesta.</typeparam>
public sealed class Envelope<T>
{
	/// <summary>Contenido útil. Nulo cuando la operación falló.</summary>
	[JsonPropertyName("resultado")]
	public T? Resultado { get; init; }

	/// <summary>
	/// Código funcional del catálogo <c>appoperador.*</c>. Vacío o nulo si todo fue bien.
	/// </summary>
	/// <remarks>
	/// El API usa indistintamente <c>null</c> y cadena vacía para "sin error", así que hay
	/// que comprobar ambos.
	/// </remarks>
	[JsonPropertyName("codigoError")]
	public string? CodigoError { get; init; }

	/// <summary>
	/// Descripción del error para el desarrollador.
	/// </summary>
	/// <remarks>
	/// <b>No es el texto que ve el operador.</b> Los literales de la interfaz los fija
	/// JTT-279 y se resuelven en la capa de presentación a partir de
	/// <see cref="CodigoError"/>.
	/// </remarks>
	[JsonPropertyName("mensajeError")]
	public string? MensajeError { get; init; }

	/// <summary>Indica si el sobre trae un error funcional.</summary>
	public bool HayError => !string.IsNullOrWhiteSpace(CodigoError);
}
