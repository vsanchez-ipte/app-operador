using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

// Historial: cada validación agrega una fila. Sin token ni contraseña; los permisos se recotejan con el token.
[Table("sesion_local")]
internal class SesionLocal
{
	[PrimaryKey]
	[Column("session_id")]
	public string SessionId { get; set; } = string.Empty;

	[Indexed(Name = "ix_sesion_operador")]
	[Column("operador")]
	public string Operador { get; set; } = string.Empty;

	[Indexed(Name = "ix_sesion_unidad")]
	[Column("unidad_clave")]
	public string? UnidadClave { get; set; }

	[Column("rol")]
	public string Rol { get; set; } = string.Empty;

	[Column("permisos")]
	public string Permisos { get; set; } = string.Empty;

	[Column("validado_utc_ticks")]
	public long ValidadoUtcTicks { get; set; }

	[Column("offline_hasta_utc_ticks")]
	public long OfflineHastaUtcTicks { get; set; }

	// Sin él, atrasar el reloj alargaría la ventana offline.
	[Column("monotonico_al_validar_ticks")]
	public long MonotonicoAlValidarTicks { get; set; }

	[Column("version_aplicacion")]
	public string VersionAplicacion { get; set; } = string.Empty;

	[Column("version_catalogos")]
	public string VersionCatalogos { get; set; } = string.Empty;

	// Un índice único parcial impide dos vigentes.
	[Column("vigente")]
	public int Vigente { get; set; }
}
