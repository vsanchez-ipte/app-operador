using System.Text.Json.Serialization;

namespace AppOperador.Infrastructure.Http.Dtos;

/// <summary>
/// Contenido de <c>resultado</c> en <c>GET ITS/AppCatalogos/Vigentes</c> (JTT-1394).
/// </summary>
/// <remarks>
/// Todo es anulable a propósito, como en los demás DTO de este canal: son datos de la red y el
/// deserializador no garantiza nada. Lo imprescindible se comprueba al convertir.
/// </remarks>
public sealed class RespuestaCatalogos
{
	/// <summary>
	/// Fecha en que se sirvió el catálogo, en formato <c>yyyy-MM-dd</c>.
	/// </summary>
	/// <remarks>
	/// Llega como <c>DateOnly</c> y <b>sin hora</b>: si viajara con marca de tiempo no
	/// parsearía contra <c>SesionOperador.VersionCatalogos</c>, que es <c>DateOnly</c>.
	/// </remarks>
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

	/// <summary>Lo que el servidor admite como evidencia (JTT-1398).</summary>
	/// <remarks>
	/// Puede faltar: un servidor anterior a la tercera tanda del canal móvil no lo publica. En
	/// ese caso no se inventan valores —la app deja de admitir adjuntos hasta que llegue—,
	/// porque validar con números distintos a los del servidor es peor que no validar.
	/// </remarks>
	[JsonPropertyName("limitesEvidencia")]
	public LimitesEvidenciaCatalogo? LimitesEvidencia { get; init; }
}

/// <summary>Límites de evidencia vigentes, tal como los declara el servidor (JTT-1398).</summary>
public sealed class LimitesEvidenciaCatalogo
{
	[JsonPropertyName("formatosPermitidos")]
	public IReadOnlyList<string>? FormatosPermitidos { get; init; }

	[JsonPropertyName("tamanoMaximoMb")]
	public int? TamanoMaximoMb { get; init; }

	[JsonPropertyName("maximoArchivosPorIncidencia")]
	public int? MaximoArchivosPorIncidencia { get; init; }
}

/// <summary>Tipo de incidencia vigente.</summary>
public sealed class TipoCatalogo
{
	/// <summary>Entero autonumérico del catálogo de Jacob.</summary>
	[JsonPropertyName("id")]
	public int? Id { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }

	/// <summary>
	/// Si el tipo obliga a capturar una nota (JTT-1397).
	/// </summary>
	/// <remarks>
	/// Es lo que permite que ninguna capa conozca la palabra «Otro»: el id de ese tipo ni
	/// siquiera es el mismo en todos los ambientes.
	/// </remarks>
	[JsonPropertyName("exigeDescripcion")]
	public bool? ExigeDescripcion { get; init; }
}

/// <summary>Nivel de severidad vigente.</summary>
public sealed class SeveridadCatalogo
{
	[JsonPropertyName("id")]
	public Guid? Id { get; init; }

	[JsonPropertyName("nivel")]
	public string? Nivel { get; init; }

	/// <summary>Posición en la escala, donde <b>menor es más grave</b>.</summary>
	[JsonPropertyName("orden")]
	public int? Orden { get; init; }

	/// <summary>Color de la insignia, en formato <c>#RRGGBB</c>.</summary>
	[JsonPropertyName("hexadecimal")]
	public string? Hexadecimal { get; init; }
}

/// <summary>Grado de cierre de la vía. <b>No es el carril.</b></summary>
public sealed class AfectacionCatalogo
{
	[JsonPropertyName("id")]
	public int? Id { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}

/// <summary>Cuerpo de la vía: <c>A</c>, <c>B</c>, <c>C</c> o <c>D</c>.</summary>
public sealed class CuerpoCatalogo
{
	[JsonPropertyName("clave")]
	public string? Clave { get; init; }

	[JsonPropertyName("nombre")]
	public string? Nombre { get; init; }
}

/// <summary>
/// Contenido de <c>resultado</c> en <c>POST /ITS/AppIncidencias/{uuid}/Evidencias</c> (JTT-1398).
/// </summary>
/// <remarks>
/// Todo anulable, como el resto de los DTO de este canal: son datos de la red y el
/// deserializador no garantiza nada.
/// </remarks>
public sealed class RespuestaEvidencia
{
	[JsonPropertyName("idEvidencia")]
	public string? IdEvidencia { get; init; }

	/// <summary>El que el <b>servidor</b> determinó por contenido, no el que se envió.</summary>
	[JsonPropertyName("tipoMime")]
	public string? TipoMime { get; init; }

	[JsonPropertyName("tamanoBytes")]
	public long? TamanoBytes { get; init; }

	/// <summary>Del contenido guardado, 64 hexadecimales.</summary>
	[JsonPropertyName("hashSha256")]
	public string? HashSha256 { get; init; }

	[JsonPropertyName("archivosAdjuntos")]
	public int? ArchivosAdjuntos { get; init; }

	[JsonPropertyName("maximoArchivos")]
	public int? MaximoArchivos { get; init; }

	/// <summary>El servidor ya tenía este contenido para esta incidencia. <b>Es éxito.</b></summary>
	[JsonPropertyName("yaExistia")]
	public bool? YaExistia { get; init; }
}
