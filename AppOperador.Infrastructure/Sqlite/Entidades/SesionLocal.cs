using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Sesión guardada para reanudarla sin conexión (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo hay una fila</b>, con <see cref="Id"/> fijo: la app atiende a un operador a la
/// vez y guardar varias invitaría a reanudar la que no toca.
/// </para>
/// <para>
/// <b>Aquí no hay token ni contraseña.</b> El token vive en el almacenamiento seguro y la
/// contraseña no se guarda en ninguna parte (CA 3). Lo que sí queda —permisos incluidos— se
/// vuelve a cotejar contra el token al restaurar, así que alterar este archivo no concede
/// nada (JTT-1379 CA 8).
/// </para>
/// </remarks>
public sealed class SesionLocal
{
	/// <summary>Llave fija: esta tabla guarda una sola sesión.</summary>
	public const int IdUnico = 1;

	[PrimaryKey]
	public int Id { get; set; } = IdUnico;

	public string SessionId { get; set; } = string.Empty;

	public string Operador { get; set; } = string.Empty;

	public string Rol { get; set; } = string.Empty;

	public string UnidadId { get; set; } = string.Empty;

	public string UnidadClave { get; set; } = string.Empty;

	public string UnidadDescripcion { get; set; } = string.Empty;

	/// <summary>Permisos separados por coma. Se revalidan contra el token al restaurar.</summary>
	public string Permisos { get; set; } = string.Empty;

	public long ValidadoUtcTicks { get; set; }

	public long OfflineHastaUtcTicks { get; set; }

	/// <summary>
	/// Contador monotónico en el instante de validar.
	/// </summary>
	/// <remarks>
	/// Es lo que permite detectar que alguien movió la hora del dispositivo. Sin este dato,
	/// la ventana offline se mediría solo con el reloj y atrasarlo la alargaría.
	/// </remarks>
	public long MonotonicoAlValidarTicks { get; set; }

	public string VersionAplicacion { get; set; } = string.Empty;

	public string VersionCatalogos { get; set; } = string.Empty;
}
