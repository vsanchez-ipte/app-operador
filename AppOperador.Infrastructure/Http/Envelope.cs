using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http;

// Todas las respuestas vienen así, incluida la llave pública. Un fallo funcional llega con HTTP 400.
public sealed class Envelope<T>
{
	[JsonPropertyName("resultado")]
	public T? Resultado { get; init; }

	// El API usa null y cadena vacía indistintamente para «sin error».
	[JsonPropertyName("codigoError")]
	public string? CodigoError { get; init; }

	// Texto para el desarrollador, no para el operador.
	[JsonPropertyName("mensajeError")]
	public string? MensajeError { get; init; }

	public bool HayError => !string.IsNullOrWhiteSpace(CodigoError);
}
