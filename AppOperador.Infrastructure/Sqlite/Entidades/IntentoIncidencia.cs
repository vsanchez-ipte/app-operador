using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

[Table("intento_incidencia")]
internal sealed class IntentoIncidencia
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	[Indexed(Name = "ix_intento_incidencia")]
	[Column("incidencia_uuid")]
	public string IncidenciaUuid { get; set; } = string.Empty;

	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	[Column("exito")]
	public bool Exito { get; set; }

	// Ya no se escribe; se decide con codigo_texto, nunca con el mensaje.
	[Column("codigo")]
	public int? Codigo { get; set; }

	[Column("codigo_texto")]
	public string? CodigoTexto { get; set; }

	[Column("mensaje")]
	public string? Mensaje { get; set; }
}
