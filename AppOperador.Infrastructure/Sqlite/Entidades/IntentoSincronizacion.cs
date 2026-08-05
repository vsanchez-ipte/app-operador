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

	/// <summary>Código HTTP o de error devuelto. Nulo si no hubo respuesta.</summary>
	[Column("codigo")]
	public int? Codigo { get; set; }

	/// <summary>Mensaje devuelto o motivo del fallo local.</summary>
	[Column("mensaje")]
	public string? Mensaje { get; set; }
}
