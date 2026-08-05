using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>catalogo_tipo_incidencia</c>: el catálogo autorizado, copiado de Jacob.
/// </summary>
/// <remarks>
/// Se guarda en local para que la captura funcione sin conexión. El catálogo definitivo
/// lo entrega Jacob y todavía no está cerrado; hasta que exista el endpoint, la siembra
/// inicial la hace <c>BaseDatosLocal</c> con los tipos que muestra la maqueta.
///
/// <see cref="Activo"/> permite que Jacob retire un tipo sin borrar el histórico: las
/// incidencias ya capturadas conservan el nombre del tipo en su propia fila.
/// </remarks>
[Table("catalogo_tipo_incidencia")]
internal sealed class TipoIncidenciaLocal
{
	/// <summary>Identificador estable del tipo.</summary>
	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = string.Empty;

	/// <summary>Texto que ve el operador.</summary>
	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;

	/// <summary>Si obliga a capturar una nota mínima (JTT-333).</summary>
	[Column("exige_descripcion")]
	public bool ExigeDescripcion { get; set; }

	/// <summary>Si el tipo sigue disponible para capturar.</summary>
	[Column("activo")]
	public bool Activo { get; set; } = true;

	/// <summary>Orden de presentación en el desplegable.</summary>
	[Column("orden")]
	public int Orden { get; set; }
}
