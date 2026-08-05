using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>incidencia_local</c>: una incidencia capturada en el dispositivo.
/// </summary>
/// <remarks>
/// Es el <c>LocalIncident</c> del documento de arquitectura. Sobrevive al cierre de la
/// app y a la falta de red: guardar nunca depende de la conexión.
///
/// Los instantes se guardan como <b>ticks UTC</b> en un entero, no como
/// <see cref="DateTime"/>. sqlite-net devuelve las fechas con
/// <see cref="DateTimeKind.Unspecified"/>, y la regla de vigencia del dominio exige
/// <see cref="DateTimeKind.Utc"/>: guardando ticks el error deja de ser posible en vez de
/// depender de acordarse de normalizar en cada lectura.
/// </remarks>
[Table("incidencia_local")]
internal sealed class IncidenciaLocal
{
	/// <summary>
	/// Identificador de idempotencia generado en el dispositivo.
	/// </summary>
	/// <remarks>
	/// Viaja a Jacob y se conserva entre reintentos: si un envío se repite porque la
	/// respuesta se perdió, el servidor reconoce el UUID y no duplica la incidencia.
	/// </remarks>
	[PrimaryKey]
	[Column("uuid")]
	public string Uuid { get; set; } = string.Empty;

	/// <summary>Clave visible para el operador, con la forma <c>LOC-######</c>.</summary>
	[Indexed(Name = "ix_incidencia_clave", Unique = true)]
	[Column("clave_local")]
	public string ClaveLocal { get; set; } = string.Empty;

	/// <summary>Clave del tipo en el catálogo. Nula mientras es borrador.</summary>
	[Column("tipo_clave")]
	public string? TipoClave { get; set; }

	/// <summary>Nombre del tipo al momento de capturar, para que el histórico no cambie
	/// si el catálogo se actualiza.</summary>
	[Column("tipo_nombre")]
	public string? TipoNombre { get; set; }

	/// <summary>Punto kilométrico en forma canónica <c>000+000</c>. Nulo en borradores.</summary>
	[Column("kilometro")]
	public string? Kilometro { get; set; }

	/// <summary>Origen del kilómetro: GPS o Manual. Ver <c>KilometerSource</c>.</summary>
	[Column("fuente_kilometro")]
	public int FuenteKilometro { get; set; }

	/// <summary>Gravedad declarada. Ver <c>Gravedad</c>.</summary>
	[Column("gravedad")]
	public int Gravedad { get; set; }

	/// <summary>Prioridad derivada de la gravedad por la regla de dominio. Ver <c>SyncPriority</c>.</summary>
	[Column("prioridad")]
	public int Prioridad { get; set; }

	/// <summary>Nota del operador. Obligatoria cuando el tipo exige descripción.</summary>
	[Column("nota")]
	public string Nota { get; set; } = string.Empty;

	/// <summary>Estado dentro de la cola. Ver <c>EstadoSincronizacion</c>.</summary>
	[Indexed(Name = "ix_incidencia_estado")]
	[Column("estado")]
	public int Estado { get; set; }

	/// <summary>Folio asignado por Jacob, con la forma <c>INC-####</c>. Nulo hasta sincronizar.</summary>
	[Column("folio_central")]
	public string? FolioCentral { get; set; }

	/// <summary>Operador que capturó, para trazabilidad.</summary>
	[Column("operador")]
	public string Operador { get; set; } = string.Empty;

	/// <summary>Unidad vehicular activa al capturar.</summary>
	[Column("unidad_vehicular")]
	public string UnidadVehicular { get; set; } = string.Empty;

	/// <summary>Instante de captura, en ticks UTC.</summary>
	[Column("creado_utc_ticks")]
	public long CreadoUtcTicks { get; set; }

	/// <summary>Instante del último cambio de estado, en ticks UTC.</summary>
	[Column("actualizado_utc_ticks")]
	public long ActualizadoUtcTicks { get; set; }

	/// <summary>Número de envíos intentados. Sirve para el reintento con espera creciente.</summary>
	[Column("intentos")]
	public int Intentos { get; set; }
}
