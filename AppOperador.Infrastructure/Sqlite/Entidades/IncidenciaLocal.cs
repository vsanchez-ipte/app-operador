using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// Instantes en ticks UTC: sqlite-net devuelve DateTime sin Kind y el dominio exige UTC.
[Table("incidencia_local")]
internal sealed class IncidenciaLocal
{
	// Clave de idempotencia: se conserva entre reintentos.
	[PrimaryKey]
	[Column("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[Indexed(Name = "ix_incidencia_clave", Unique = true)]
	[Column("clave_local")]
	public string ClaveLocal { get; set; } = string.Empty;

	// Texto: es un sello histórico, no una llave. Las filas viejas traen claves de maqueta y no son enviables.
	[Column("tipo_clave")]
	public string? TipoClave { get; set; }

	[Column("tipo_nombre")]
	public string? TipoNombre { get; set; }

	[Column("kilometro")]
	public string? Kilometro { get; set; }

	[Column("fuente_kilometro")]
	public int FuenteKilometro { get; set; }

	[Column("kilometro_metros")]
	public int? KilometroMetros { get; set; }

	[Column("gps_latitud")]
	public double? GpsLatitud { get; set; }

	[Column("gps_longitud")]
	public double? GpsLongitud { get; set; }

	[Column("gps_precision_metros")]
	public double? GpsPrecisionMetros { get; set; }

	[Column("gps_instante_utc_ticks")]
	public long? GpsInstanteUtcTicks { get; set; }

	// Ya no se escribe; se conserva por las filas viejas.
	[Column("gravedad")]
	public int Gravedad { get; set; }

	[Column("severidad_id")]
	public string SeveridadId { get; set; } = string.Empty;

	[Column("severidad_nombre")]
	public string SeveridadNombre { get; set; } = string.Empty;

	[Column("severidad_orden")]
	public int SeveridadOrden { get; set; }

	[Column("prioridad")]
	public int Prioridad { get; set; }

	// Se sella aquí: la sesión puede haber bajado otro catálogo cuando esto se envíe.
	[Column("version_catalogo")]
	public string VersionCatalogo { get; set; } = string.Empty;

	[Column("nota")]
	public string Nota { get; set; } = string.Empty;

	[Indexed(Name = "ix_incidencia_estado")]
	[Column("estado")]
	public int Estado { get; set; }

	// Nunca se sobrescribe con nulo: un reintento fallido no debe borrar el folio.
	[Column("folio_central")]
	public string? FolioCentral { get; set; }

	// Toda consulta de cola y borradores filtra por aquí.
	[Indexed(Name = "ix_incidencia_operador")]
	[Column("operador")]
	public string? Operador { get; set; }

	[Indexed(Name = "ix_incidencia_unidad")]
	[Column("unidad_vehicular")]
	public string? UnidadVehicular { get; set; }

	[Column("creado_utc_ticks")]
	public long CreadoUtcTicks { get; set; }

	[Column("actualizado_utc_ticks")]
	public long ActualizadoUtcTicks { get; set; }

	[Column("intentos")]
	public int Intentos { get; set; }

	// Persistido para que un rechazo funcional no se reintente al reabrir la app.
	[Column("ultimo_error_codigo")]
	public string? UltimoErrorCodigo { get; set; }

	// Con CreadoUtcTicks permite ordenar aunque se haya movido el reloj.
	[Column("monotonico_ticks")]
	public long MonotonicoTicks { get; set; }

	[Indexed(Name = "ix_incidencia_sesion")]
	[Column("sesion_origen")]
	public string? SesionOrigen { get; set; }

	// Al crear, no al enviar: para entonces el permiso pudo cambiar.
	[Column("permiso_origen")]
	public string PermisoOrigen { get; set; } = string.Empty;
}
