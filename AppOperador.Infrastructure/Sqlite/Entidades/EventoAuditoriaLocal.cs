using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>evento_auditoria</c>: la bitácora que el operador ve en su perfil.
/// </summary>
[Table("evento_auditoria")]
internal sealed class EventoAuditoriaLocal
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	/// <summary>Instante del evento, en ticks UTC. La vista lo presenta en hora local.</summary>
	[Indexed(Name = "ix_auditoria_instante")]
	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	/// <summary>Severidad. Ver <c>NivelAuditoria</c>.</summary>
	[Column("nivel")]
	public int Nivel { get; set; }

	/// <summary>Texto del evento. Nunca debe contener credenciales ni tokens.</summary>
	[Column("mensaje")]
	public string Mensaje { get; set; } = string.Empty;
}
