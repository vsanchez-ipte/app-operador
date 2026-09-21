using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>intento_incidencia</c>: cada envío de una incidencia a Jacob, exitoso o fallido.
/// </summary>
/// <remarks>
/// El documento de arquitectura pide conservar "intentos y errores" y poder responder por qué
/// un registro no ha llegado. Sin esta tabla, un registro atorado en Fallido no dice nada: con
/// ella se sabe cuántas veces se intentó, contra qué respondió Jacob y cuándo fue la última vez.
/// Hasta el esquema 10 compartía tabla con los intentos de evidencia; se partió porque una
/// llave foránea no puede apuntar a dos tablas.
/// </remarks>
[Table("intento_incidencia")]
internal sealed class IntentoIncidencia
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	/// <summary>Incidencia que se intentó enviar; llave a <c>incidencia_local</c>.</summary>
	[Indexed(Name = "ix_intento_incidencia")]
	[Column("incidencia_uuid")]
	public string IncidenciaUuid { get; set; } = string.Empty;

	/// <summary>Instante del intento, en ticks UTC.</summary>
	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	/// <summary>Si Jacob confirmó el registro.</summary>
	[Column("exito")]
	public bool Exito { get; set; }

	/// <summary>
	/// Código HTTP del intento. <b>Ya no se escribe</b>; se conserva por lo guardado antes.
	/// Las filas nuevas la dejan nula y usan <see cref="CodigoTexto"/>.
	/// </summary>
	[Column("codigo")]
	public int? Codigo { get; set; }

	/// <summary>
	/// Código de error de Jacob, como <c>appincidencias.km.fueradecorredor</c>.
	/// </summary>
	/// <remarks>
	/// Es texto porque el contrato del canal móvil identifica los errores por cadena y no por
	/// número, y es <b>por el código y nunca por el mensaje</b> como se decide si se reintenta.
	/// </remarks>
	[Column("codigo_texto")]
	public string? CodigoTexto { get; set; }

	/// <summary>Mensaje devuelto o motivo del fallo local.</summary>
	[Column("mensaje")]
	public string? Mensaje { get; set; }
}
