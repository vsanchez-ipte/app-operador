using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// Operador, rol, permiso y unidad van copiados: el acceso escribe antes de que exista la sesión.
[Table("evento_auditoria")]
internal sealed class EventoAuditoriaLocal
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	[Indexed(Name = "ix_auditoria_instante")]
	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	// Sello que no se mueve con el reloj; el orden lo da Id.
	[Column("monotonico_ticks")]
	public long MonotonicoTicks { get; set; }

	[Column("nivel")]
	public int Nivel { get; set; }

	// Nunca credenciales ni tokens.
	[Column("mensaje")]
	public string Mensaje { get; set; } = string.Empty;

	[Column("operacion")]
	public int Operacion { get; set; }

	[Column("resultado")]
	public int? Resultado { get; set; }

	[Column("motivo_codigo")]
	public string? MotivoCodigo { get; set; }

	[Indexed(Name = "ix_auditoria_operador")]
	[Column("operador")]
	public string? Operador { get; set; }

	[Column("rol")]
	public string? Rol { get; set; }

	[Column("permiso")]
	public string? Permiso { get; set; }

	[Column("unidad_clave")]
	public string? UnidadClave { get; set; }

	[Indexed(Name = "ix_auditoria_sesion")]
	[Column("sesion_id")]
	public string? SesionId { get; set; }

	// Nulo solo en filas anteriores al esquema 10.
	[Column("origen")]
	public int? Origen { get; set; }
}
