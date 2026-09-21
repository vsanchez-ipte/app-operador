using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>operador_local</c>: cada operador que ha trabajado en este dispositivo.
/// </summary>
/// <remarks>
/// La llave es la cuenta en Jacob, porque es lo único que el canal móvil entrega para
/// identificar a una persona: no hay un identificador numérico que copiar. Es a lo que apuntan
/// las sesiones y las incidencias, y lo que hace que «ana.lopez» sea la misma en todas.
/// </remarks>
[Table("operador_local")]
internal sealed class OperadorLocal
{
	[PrimaryKey]
	[Column("cuenta")]
	public string Cuenta { get; set; } = string.Empty;

	/// <summary>Último rol con el que ingresó. El de cada sesión está en <c>sesion_local</c>.</summary>
	[Column("rol")]
	public string Rol { get; set; } = string.Empty;
}
