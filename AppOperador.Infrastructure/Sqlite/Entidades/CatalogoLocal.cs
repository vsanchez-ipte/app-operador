using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// El Guid va como texto: sqlite-net no tiene tipo para él.
[Table("catalogo_severidad")]
internal sealed class SeveridadLocal
{
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = string.Empty;

	[Column("nivel")]
	public string Nivel { get; set; } = string.Empty;

	[Column("orden")]
	public int Orden { get; set; }

	[Column("hexadecimal")]
	public string Hexadecimal { get; set; } = string.Empty;
}

// Grado de cierre de la vía, no el carril.
[Table("catalogo_afectacion")]
internal sealed class AfectacionLocal
{
	[PrimaryKey]
	[Column("id")]
	public int Id { get; set; }

	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;
}

[Table("catalogo_cuerpo")]
internal sealed class CuerpoLocal
{
	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = string.Empty;

	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;
}

// Una sola fila: la versión es de la descarga completa, no de cada lista.
[Table("catalogo_meta")]
internal sealed class CatalogoMetaLocal
{
	public const string ClaveUnica = "vigente";

	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = ClaveUnica;

	[Column("version")]
	public string Version { get; set; } = string.Empty;

	// Un solo dato del catálogo, no una lista consultable: no amerita tabla.
	[Column("evidencia_formatos")]
	public string EvidenciaFormatos { get; set; } = string.Empty;

	// 0 significa que no se ha descargado.
	[Column("evidencia_tamano_maximo_mb")]
	public int EvidenciaTamanoMaximoMb { get; set; }

	// 0 significa que no se ha descargado.
	[Column("evidencia_maximo_archivos")]
	public int EvidenciaMaximoArchivos { get; set; }
}
