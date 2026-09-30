using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

// Todo anulable: lo imprescindible se comprueba al convertir.
public sealed class RespuestaCatalogos
{
	// Sin hora, para que cuadre con el DateOnly de la sesión.
	[JsonPropertyName("version")]
	public DateOnly? Version { get; init; }

	[JsonPropertyName("tipos")]
	public IReadOnlyList<TipoCatalogo>? Tipos { get; init; }

	[JsonPropertyName("severidades")]
	public IReadOnlyList<SeveridadCatalogo>? Severidades { get; init; }

	[JsonPropertyName("afectaciones")]
	public IReadOnlyList<AfectacionCatalogo>? Afectaciones { get; init; }

	[JsonPropertyName("cuerpos")]
	public IReadOnlyList<CuerpoCatalogo>? Cuerpos { get; init; }

	// Falta en servidores viejos; entonces no se admiten adjuntos.
	[JsonPropertyName("limitesEvidencia")]
	public LimitesEvidenciaCatalogo? LimitesEvidencia { get; init; }
}

public sealed class LimitesEvidenciaCatalogo
{
	[JsonPropertyName("formatosPermitidos")]
	public IReadOnlyList<string>? FormatosPermitidos { get; init; }

	[JsonPropertyName("tamanoMaximoMb")]
	public int? TamanoMaximoMb { get; init; }

	[JsonPropertyName("maximoArchivosPorIncidencia")]
	public int? MaximoArchivosPorIncidencia { get; init; }
}

public sealed class TipoCatalogo
{
	[JsonPropertyName("id")]
	public int? Id { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }

	[JsonPropertyName("exigeDescripcion")]
	public bool? ExigeDescripcion { get; init; }
}

public sealed class SeveridadCatalogo
{
	[JsonPropertyName("id")]
	public Guid? Id { get; init; }

	[JsonPropertyName("nivel")]
	public string? Nivel { get; init; }

	[JsonPropertyName("orden")]
	public int? Orden { get; init; }

	[JsonPropertyName("hexadecimal")]
	public string? Hexadecimal { get; init; }
}

// Grado de cierre de la vía, no el carril.
public sealed class AfectacionCatalogo
{
	[JsonPropertyName("id")]
	public int? Id { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}

public sealed class CuerpoCatalogo
{
	[JsonPropertyName("clave")]
	public string? Clave { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}

public sealed class RespuestaEvidencia
{
	[JsonPropertyName("idEvidencia")]
	public string? IdEvidencia { get; init; }

	[JsonPropertyName("tipoMime")]
	public string? TipoMime { get; init; }

	[JsonPropertyName("tamanoBytes")]
	public long? TamanoBytes { get; init; }

	[JsonPropertyName("hashSha256")]
	public string? HashSha256 { get; init; }

	[JsonPropertyName("archivosAdjuntos")]
	public int? ArchivosAdjuntos { get; init; }

	[JsonPropertyName("maximoArchivos")]
	public int? MaximoArchivos { get; init; }

	// Es éxito.
	[JsonPropertyName("yaExistia")]
	public bool? YaExistia { get; init; }
}
