using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// Caché del servidor, se reemplaza entera. La llave es el id de Jacob, sin AutoIncrement.
[Table("catalogo_tipo_incidencia")]
internal sealed class TipoIncidenciaLocal
{
	[PrimaryKey]
	[Column("id")]
	public int Id { get; set; }

	[Column("nombre")]
	public string Nombre { get; set; } = string.Empty;

	[Column("exige_descripcion")]
	public bool ExigeDescripcion { get; set; }

	[Column("orden")]
	public int Orden { get; set; }
}
