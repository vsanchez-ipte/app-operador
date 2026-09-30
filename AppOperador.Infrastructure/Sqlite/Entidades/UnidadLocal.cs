using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// Llave: la clave visible, que es lo que guardaban las filas viejas. Id vacío si la migración reconstruyó la unidad.
[Table("unidad_local")]
internal sealed class UnidadLocal
{
	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = string.Empty;

	// El que viaja al API.
	[Column("id")]
	public string Id { get; set; } = string.Empty;

	[Column("descripcion")]
	public string Descripcion { get; set; } = string.Empty;
}
