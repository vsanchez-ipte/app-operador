using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// La llave es la cuenta de Jacob: es lo único con que el canal móvil identifica a una persona.
[Table("operador_local")]
internal sealed class OperadorLocal
{
	[PrimaryKey]
	[Column("cuenta")]
	public string Cuenta { get; set; } = string.Empty;

	[Column("rol")]
	public string Rol { get; set; } = string.Empty;
}
