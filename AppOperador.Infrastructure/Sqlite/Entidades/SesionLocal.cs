using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>sesion_local</c>: una sesión validada en Jacob, para reanudarla sin conexión
/// (JTT-1383) y para que lo capturado sepa de qué sesión salió.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es un historial, no una fila que se sobrescribe.</b> Hasta el esquema 10 había una sola
/// y entrar otro operador la pisaba; por eso las incidencias y la bitácora copiaban el operador
/// y la unidad como texto. Ahora cada validación agrega una fila, la app trabaja con la que
/// tiene <see cref="Vigente"/>, y cerrar sesión la marca en vez de borrarla: lo que apuntaba a
/// ella sigue apuntando.
/// </para>
/// <para>
/// <b>Aquí no hay token ni contraseña.</b> El token vive en el almacenamiento seguro y la
/// contraseña no se guarda en ninguna parte (CA 3). Lo que sí queda —permisos incluidos— se
/// vuelve a cotejar contra el token al restaurar, así que alterar este archivo no concede
/// nada (JTT-1379 CA 8).
/// </para>
/// </remarks>
[Table("sesion_local")]
internal class SesionLocal
{
	/// <summary>Identificador de la sesión en Jacob. Vacío en los recorridos simulados, que no abren sesión.</summary>
	[PrimaryKey]
	[Column("session_id")]
	public string SessionId { get; set; } = string.Empty;

	/// <summary>Cuenta del operador; llave a <c>operador_local</c>.</summary>
	[Indexed(Name = "ix_sesion_operador")]
	[Column("operador")]
	public string Operador { get; set; } = string.Empty;

	/// <summary>
	/// Clave de la unidad; llave a <c>unidad_local</c>. Nula solo en sesiones que la migración
	/// reconstruyó sin saber la unidad.
	/// </summary>
	[Indexed(Name = "ix_sesion_unidad")]
	[Column("unidad_clave")]
	public string? UnidadClave { get; set; }

	[Column("rol")]
	public string Rol { get; set; } = string.Empty;

	/// <summary>Permisos separados por coma. Se revalidan contra el token al restaurar.</summary>
	[Column("permisos")]
	public string Permisos { get; set; } = string.Empty;

	[Column("validado_utc_ticks")]
	public long ValidadoUtcTicks { get; set; }

	[Column("offline_hasta_utc_ticks")]
	public long OfflineHastaUtcTicks { get; set; }

	/// <summary>
	/// Contador monotónico en el instante de validar.
	/// </summary>
	/// <remarks>
	/// Es lo que permite detectar que alguien movió la hora del dispositivo. Sin este dato,
	/// la ventana offline se mediría solo con el reloj y atrasarlo la alargaría.
	/// </remarks>
	[Column("monotonico_al_validar_ticks")]
	public long MonotonicoAlValidarTicks { get; set; }

	[Column("version_aplicacion")]
	public string VersionAplicacion { get; set; } = string.Empty;

	[Column("version_catalogos")]
	public string VersionCatalogos { get; set; } = string.Empty;

	/// <summary>
	/// <c>1</c> en la sesión con la que la app trabaja; <c>0</c> en las demás. Un índice único
	/// parcial impide que haya dos vigentes.
	/// </summary>
	[Column("vigente")]
	public int Vigente { get; set; }
}
