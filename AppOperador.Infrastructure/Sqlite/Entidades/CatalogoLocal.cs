using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>catalogo_severidad</c>: copia local de los niveles de Jacob (JTT-1394 CA 2).
/// </summary>
/// <remarks>
/// El <c>Guid</c> se guarda como texto porque sqlite-net no tiene tipo propio para él. Se
/// conserva también <see cref="Orden"/>, que es de donde sale la prioridad de sincronización, y
/// <see cref="Hexadecimal"/>, para no inventar colores del lado de la app.
/// </remarks>
[Table("catalogo_severidad")]
internal sealed class SeveridadLocal
{
	/// <summary>Identificador del nivel en Jacob, en forma canónica.</summary>
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = string.Empty;

	/// <summary>Texto que ve el operador: «Crítico», «Advertencia», «Información».</summary>
	[Column("nivel")]
	public string Nivel { get; set; } = string.Empty;

	/// <summary>Posición en la escala, donde <b>menor es más grave</b>.</summary>
	[Column("orden")]
	public int Orden { get; set; }

	/// <summary>Color de la insignia, en formato <c>#RRGGBB</c>.</summary>
	[Column("hexadecimal")]
	public string Hexadecimal { get; set; } = string.Empty;
}

/// <summary>
/// Fila de <c>catalogo_afectacion</c>: el grado de cierre de la vía (JTT-1394 CA 2).
/// </summary>
/// <remarks>
/// <b>No es el carril afectado</b>: los valores son «Total», «Parcial» y «Sin afectación».
/// </remarks>
[Table("catalogo_afectacion")]
internal sealed class AfectacionLocal
{
	/// <summary>Identificador en el catálogo de Jacob.</summary>
	[PrimaryKey]
	[Column("id")]
	public int Id { get; set; }

	/// <summary>Texto que ve el operador.</summary>
	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;
}

/// <summary>
/// Fila de <c>catalogo_cuerpo</c>: los cuerpos de la vía (JTT-1394 CA 2).
/// </summary>
/// <remarks>
/// Lista cerrada de cuatro que el API publica sin que exista tabla detrás. Se guarda igual que
/// los demás para que el formulario no dependa de la red.
/// </remarks>
[Table("catalogo_cuerpo")]
internal sealed class CuerpoLocal
{
	/// <summary>Lo que se guarda en la incidencia: <c>A</c>, <c>B</c>, <c>C</c> o <c>D</c>.</summary>
	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = string.Empty;

	/// <summary>Texto que ve el operador.</summary>
	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;
}

/// <summary>
/// Fila única de <c>catalogo_meta</c>: qué versión de catálogo hay guardada (JTT-1394 CA 4 y 5).
/// </summary>
/// <remarks>
/// <para>
/// Es <b>una sola fila</b>, con <see cref="Clave"/> fija. Se separa de las tablas de catálogo
/// porque la versión es de la descarga completa, no de cada lista: lo que se sella en una
/// incidencia capturada sin conexión es una sola versión, no cuatro.
/// </para>
/// <para>
/// La fecha se guarda como texto <c>yyyy-MM-dd</c>, igual que en <c>SesionLocal</c>, para que
/// una lectura no dependa de la cultura del dispositivo.
/// </para>
/// </remarks>
[Table("catalogo_meta")]
internal sealed class CatalogoMetaLocal
{
	/// <summary>Valor fijo de la única fila.</summary>
	public const string ClaveUnica = "vigente";

	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = ClaveUnica;

	/// <summary>Fecha en que se descargó el catálogo, en formato <c>yyyy-MM-dd</c>.</summary>
	[Column("version")]
	public string Version { get; set; } = string.Empty;

	/// <summary>
	/// Tipos MIME de evidencia admitidos, separados por coma (JTT-1398).
	/// </summary>
	/// <remarks>
	/// Van en la fila de metadatos y no en una tabla propia porque son <b>un solo dato del
	/// catálogo</b>, no una lista consultable: nadie filtra ni ordena por formato. Una tabla de
	/// cuatro filas para leerlas siempre juntas sería ceremonia sin uso.
	/// </remarks>
	[Column("evidencia_formatos")]
	public string EvidenciaFormatos { get; set; } = string.Empty;

	/// <summary>Tope por archivo en megabytes; <c>0</c> significa que no se ha descargado.</summary>
	[Column("evidencia_tamano_maximo_mb")]
	public int EvidenciaTamanoMaximoMb { get; set; }

	/// <summary>Archivos admitidos por incidencia; <c>0</c> significa que no se ha descargado.</summary>
	[Column("evidencia_maximo_archivos")]
	public int EvidenciaMaximoArchivos { get; set; }
}
