using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>unidad_local</c>: cada unidad vehicular con la que se ha operado desde aquí.
/// </summary>
/// <remarks>
/// <para>
/// La llave es la clave visible —<c>VEH-01</c>— y no el identificador técnico de Jacob, porque
/// la clave es lo que las incidencias y la bitácora guardaban desde el primer esquema: es lo
/// único por lo que una fila vieja puede encontrar a su unidad.
/// </para>
/// <para>
/// <see cref="Id"/> queda vacío en las unidades que la migración reconstruyó a partir de
/// incidencias, que solo sabían la clave; se rellena la primera vez que alguien inicia sesión
/// con esa unidad.
/// </para>
/// </remarks>
[Table("unidad_local")]
internal sealed class UnidadLocal
{
	[PrimaryKey]
	[Column("clave")]
	public string Clave { get; set; } = string.Empty;

	/// <summary>Identificador técnico que emite Jacob; es el que viaja al API.</summary>
	[Column("id")]
	public string Id { get; set; } = string.Empty;

	[Column("descripcion")]
	public string Descripcion { get; set; } = string.Empty;
}
