using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>evento_auditoria</c>: la bitácora que el operador ve en su perfil (JTT-1392).
/// </summary>
/// <remarks>
/// <para>
/// Desde la versión 10 del esquema lleva las dimensiones del CA 1 —operador, rol, permiso,
/// unidad, sesión, online/offline—. Desde la 11 la sesión es llave a <c>sesion_local</c>, que ya
/// es historial; operador, rol, permiso y unidad siguen <b>copiados en la fila</b> a propósito:
/// la línea del acceso se escribe antes de que exista la sesión, y el operador se reescribe al
/// atribuir un alias.
/// </para>
/// <para>
/// Las filas anteriores a la versión 10 quedan con estas columnas en nulo o en su valor por
/// omisión. No se borran: se muestran como historial previo, sin operador.
/// </para>
/// </remarks>
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

	/// <summary>
	/// Contador monotónico del sistema al registrar: el sello que no se puede mover con el
	/// reloj (CA 3). No es el orden —se reinicia con el dispositivo—; el orden es <see cref="Id"/>.
	/// </summary>
	[Column("monotonico_ticks")]
	public long MonotonicoTicks { get; set; }

	/// <summary>Severidad. Ver <c>NivelAuditoria</c>.</summary>
	[Column("nivel")]
	public int Nivel { get; set; }

	/// <summary>Texto del evento. Nunca debe contener credenciales ni tokens.</summary>
	[Column("mensaje")]
	public string Mensaje { get; set; } = string.Empty;

	/// <summary>Operación, con el vocabulario del CCO. Ver <c>OperacionAuditada</c>.</summary>
	[Column("operacion")]
	public int Operacion { get; set; }

	/// <summary>Éxito o rechazo, o nulo en un aviso general. Ver <c>ResultadoAuditoria</c>.</summary>
	[Column("resultado")]
	public int? Resultado { get; set; }

	/// <summary>Código del rechazo, el mismo que devolvió Jacob.</summary>
	[Column("motivo_codigo")]
	public string? MotivoCodigo { get; set; }

	[Indexed(Name = "ix_auditoria_operador")]
	[Column("operador")]
	public string? Operador { get; set; }

	[Column("rol")]
	public string? Rol { get; set; }

	/// <summary>Permiso funcional con el que operaba, si la sesión traía uno.</summary>
	[Column("permiso")]
	public string? Permiso { get; set; }

	[Column("unidad_clave")]
	public string? UnidadClave { get; set; }

	/// <summary>Sesión abierta al registrar; llave a <c>sesion_local</c>. Nulo en el acceso, que ocurre antes de tenerla.</summary>
	[Indexed(Name = "ix_auditoria_sesion")]
	[Column("sesion_id")]
	public string? SesionId { get; set; }

	/// <summary>
	/// Si había enlace con el CCO. Ver <c>OrigenAuditoria</c>. Nulo solo en las filas anteriores
	/// al esquema 10, que es como se reconocen: la migración deja las columnas nuevas en NULL.
	/// </summary>
	[Column("origen")]
	public int? Origen { get; set; }
}
