using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

[Table("intento_evidencia")]
internal sealed class IntentoEvidencia
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	[Indexed(Name = "ix_intento_evidencia")]
	[Column("evidencia_uuid")]
	public string EvidenciaUuid { get; set; } = string.Empty;

	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	[Column("exito")]
	public bool Exito { get; set; }

	// Nunca se ha escrito para evidencias; existe por simetría con intento_incidencia.
	[Column("codigo")]
	public int? Codigo { get; set; }

	[Column("codigo_texto")]
	public string? CodigoTexto { get; set; }

	[Column("mensaje")]
	public string? Mensaje { get; set; }
}
