using AppOperador.Aplicacion.Modelos;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Garantiza que el operador, la unidad y la sesión a los que va a apuntar una fila existan.
/// </summary>
/// <remarks>
/// <para>
/// Con llaves foráneas activas, guardar una incidencia o una línea de bitácora con una sesión
/// que no esté en <c>sesion_local</c> falla. En la app real la sesión ya está —la custodia la
/// persiste antes de anunciarla—, pero una captura no puede depender de ese orden: si algo lo
/// rompiera, el operador se quedaría sin poder guardar en campo. Aquí se asegura con
/// <c>INSERT OR IGNORE</c>, que no toca nada si ya existe.
/// </para>
/// <para>
/// Una sesión creada por este camino queda con <c>vigente = 0</c>: vigente es la que la custodia
/// guardó, no una que se supo de paso.
/// </para>
/// </remarks>
internal static class ReferenciasSesion
{
	/// <summary>
	/// Deja creados operador, unidad y sesión de <paramref name="sesion"/>, si hay sesión.
	/// Devuelve el identificador de sesión que sellar en la fila, o <see langword="null"/> si
	/// no hay sesión o no tiene identificador.
	/// </summary>
	public static async Task<string?> AsegurarAsync(SQLiteAsyncConnection conexion, SesionOperador? sesion)
	{
		if (sesion is null || string.IsNullOrWhiteSpace(sesion.Operador))
		{
			return null;
		}

		await AsegurarOperadorAsync(conexion, sesion.Operador, sesion.Rol);

		if (!string.IsNullOrWhiteSpace(sesion.UnidadVehicular))
		{
			await AsegurarUnidadAsync(conexion, sesion.UnidadVehicular, id: null, descripcion: null);
		}

		if (string.IsNullOrWhiteSpace(sesion.SessionId))
		{
			return null;
		}

		await conexion.ExecuteAsync(
			"""
			INSERT OR IGNORE INTO "sesion_local"
				("session_id", "operador", "unidad_clave", "rol", "permisos",
				 "validado_utc_ticks", "offline_hasta_utc_ticks", "monotonico_al_validar_ticks",
				 "version_aplicacion", "version_catalogos", "vigente")
			VALUES (?, ?, ?, ?, ?, ?, ?, 0, ?, ?, 0);
			""",
			sesion.SessionId,
			sesion.Operador,
			string.IsNullOrWhiteSpace(sesion.UnidadVehicular) ? null : sesion.UnidadVehicular,
			sesion.Rol,
			string.Join(",", sesion.Permisos),
			sesion.Vigencia.LastValidatedAtUtc.Ticks,
			sesion.Vigencia.OfflineUntilUtc.Ticks,
			sesion.VersionAplicacion,
			sesion.VersionCatalogos.ToString("yyyy-MM-dd"));

		return sesion.SessionId;
	}

	/// <summary>Crea el operador o le actualiza el rol, que es el último conocido.</summary>
	public static Task AsegurarOperadorAsync(SQLiteAsyncConnection conexion, string cuenta, string rol) =>
		conexion.ExecuteAsync(
			"""
			INSERT INTO "operador_local" ("cuenta", "rol") VALUES (?, ?)
			ON CONFLICT("cuenta") DO UPDATE SET "rol" = excluded."rol" WHERE excluded."rol" <> '';
			""",
			cuenta,
			rol);

	/// <summary>
	/// Crea la unidad o completa lo que le faltaba. El identificador técnico y la descripción
	/// solo los sabe la sesión; una unidad vista desde una incidencia llega con la clave y nada
	/// más, y no debe pisar lo que otra sesión ya dejó.
	/// </summary>
	public static Task AsegurarUnidadAsync(SQLiteAsyncConnection conexion, string clave, string? id, string? descripcion) =>
		conexion.ExecuteAsync(
			"""
			INSERT INTO "unidad_local" ("clave", "id", "descripcion") VALUES (?, ?, ?)
			ON CONFLICT("clave") DO UPDATE SET
				"id" = CASE WHEN excluded."id" <> '' THEN excluded."id" ELSE "id" END,
				"descripcion" = CASE WHEN excluded."descripcion" <> '' THEN excluded."descripcion" ELSE "descripcion" END;
			""",
			clave,
			id ?? string.Empty,
			descripcion ?? string.Empty);
}
