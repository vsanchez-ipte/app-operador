using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>intento_sincronizacion</c>: la bitácora de cada envío, exitoso o fallido.
/// </summary>
/// <remarks>
/// El documento de arquitectura pide conservar "intentos y errores" y poder responder
/// por qué un registro no ha llegado. Sin esta tabla, un registro atorado en Fallido no
/// dice nada: con ella se sabe cuántas veces se intentó, contra qué respondió Jacob y
/// cuándo fue la última vez.
/// </remarks>
[Table("intento_sincronizacion")]
internal sealed class IntentoSincronizacion
{
	[PrimaryKey]
	[AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	/// <summary>UUID del registro que se intentó enviar.</summary>
	[Indexed(Name = "ix_intento_registro")]
	[Column("registro_uuid")]
	public string RegistroUuid { get; set; } = string.Empty;

	/// <summary>Si el registro era incidencia o evidencia. Ver <c>ClaseRegistro</c>.</summary>
	[Column("clase")]
	public int Clase { get; set; }

	/// <summary>Instante del intento, en ticks UTC.</summary>
	[Column("instante_utc_ticks")]
	public long InstanteUtcTicks { get; set; }

	/// <summary>Si Jacob confirmó el registro.</summary>
	[Column("exito")]
	public bool Exito { get; set; }

	/// <summary>
	/// Código HTTP del intento. <b>Ya no se escribe</b>; se conserva por lo guardado antes.
	/// </summary>
	/// <remarks>
	/// Quitar una columna en SQLite obliga a reconstruir la tabla, y con ella se iría la traza
	/// de los envíos anteriores. Las filas nuevas la dejan nula y usan
	/// <see cref="CodigoTexto"/>.
	/// </remarks>
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
