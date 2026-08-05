using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>evidencia_local</c>: una fotografía o video asociado a una incidencia.
/// </summary>
/// <remarks>
/// <b>Aquí no se guarda el binario</b>, solo la ruta al archivo privado. El documento de
/// arquitectura lo prohíbe expresamente: SQLite guarda "sin tokens ni binarios".
///
/// La evidencia se sincroniza <b>por separado</b> de su incidencia y se reintenta por
/// separado: una evidencia fallida no revierte una incidencia ya confirmada. Por eso
/// tiene su propio UUID, su propio estado y su propio contador de intentos.
/// </remarks>
[Table("evidencia_local")]
internal sealed class EvidenciaLocal
{
	/// <summary>Identificador de idempotencia propio de la evidencia.</summary>
	[PrimaryKey]
	[Column("uuid")]
	public string Uuid { get; set; } = string.Empty;

	/// <summary>UUID de la incidencia a la que pertenece.</summary>
	[Indexed(Name = "ix_evidencia_incidencia")]
	[Column("incidencia_uuid")]
	public string IncidenciaUuid { get; set; } = string.Empty;

	/// <summary>Ruta al archivo privado del dispositivo. Nunca el contenido.</summary>
	[Column("ruta_archivo")]
	public string RutaArchivo { get; set; } = string.Empty;

	/// <summary>Tipo de medio, por ejemplo <c>image/jpeg</c>.</summary>
	[Column("tipo_medio")]
	public string TipoMedio { get; set; } = string.Empty;

	/// <summary>Tamaño en bytes, para estimar el costo del envío.</summary>
	[Column("bytes")]
	public long Bytes { get; set; }

	/// <summary>Estado dentro de la cola. Ver <c>EstadoSincronizacion</c>.</summary>
	[Indexed(Name = "ix_evidencia_estado")]
	[Column("estado")]
	public int Estado { get; set; }

	/// <summary>Instante de captura, en ticks UTC.</summary>
	[Column("creado_utc_ticks")]
	public long CreadoUtcTicks { get; set; }

	/// <summary>Número de envíos intentados.</summary>
	[Column("intentos")]
	public int Intentos { get; set; }
}
