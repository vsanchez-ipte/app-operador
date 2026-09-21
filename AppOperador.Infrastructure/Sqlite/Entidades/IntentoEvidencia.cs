using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>intento_evidencia</c>: cada subida de una evidencia a Jacob, exitosa o fallida.
/// </summary>
/// <remarks>
/// Mismas columnas que <see cref="IntentoIncidencia"/>, en tabla aparte por lo mismo que la
/// evidencia tiene su propia cola: se envía y se reintenta por su cuenta, y su llave apunta a
/// <c>evidencia_local</c>, no a la incidencia.
/// </remarks>
[Table("intento_evidencia")]
internal sealed class IntentoEvidencia
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	/// <summary>Evidencia que se intentó subir; llave a <c>evidencia_local</c>.</summary>
	[Indexed(Name = "ix_intento_evidencia")]
	[Column("evidencia_uuid")]
	public string EvidenciaUuid { get; set; } = string.Empty;

	/// <summary>Instante del intento, en ticks UTC.</summary>
	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	/// <summary>Si Jacob confirmó la evidencia.</summary>
	[Column("exito")]
	public bool Exito { get; set; }

	/// <summary>Código HTTP. Nunca se ha escrito para evidencias; existe por simetría con la tabla hermana.</summary>
	[Column("codigo")]
	public int? Codigo { get; set; }

	/// <summary>Código de error de Jacob, como texto.</summary>
	[Column("codigo_texto")]
	public string? CodigoTexto { get; set; }

	/// <summary>Mensaje devuelto o motivo del fallo local.</summary>
	[Column("mensaje")]
	public string? Mensaje { get; set; }
}
