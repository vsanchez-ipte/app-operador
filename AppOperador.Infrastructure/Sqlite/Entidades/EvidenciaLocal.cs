using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// Solo la ruta, nunca el binario. Estado e intentos propios: se sincroniza aparte de su incidencia.
[Table("evidencia_local")]
internal sealed class EvidenciaLocal
{
	[PrimaryKey]
	[Column("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[Indexed(Name = "ix_evidencia_incidencia")]
	[Column("incidencia_uuid")]
	public string IncidenciaUuid { get; set; } = string.Empty;

	[Column("ruta_archivo")]
	public string RutaArchivo { get; set; } = string.Empty;

	// Para que el operador la reconozca; el archivo se encuentra por RutaArchivo.
	[Column("nombre_original")]
	public string NombreOriginal { get; set; } = string.Empty;

	[Column("tipo_medio")]
	public string TipoMedio { get; set; } = string.Empty;

	[Column("bytes")]
	public long Bytes { get; set; }

	[Indexed(Name = "ix_evidencia_estado")]
	[Column("estado")]
	public int Estado { get; set; }

	[Column("creado_utc_ticks")]
	public long CreadoUtcTicks { get; set; }

	[Column("intentos")]
	public int Intentos { get; set; }

	// Persistido para que un rechazo funcional no se reintente al reabrir la app.
	[Column("ultimo_error_codigo")]
	public string? UltimoErrorCodigo { get; set; }
}
