using AppOperador.Aplicacion.Modelos;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

// Una captura no puede fallar porque falte su sesión en la base; la sesión creada así queda no vigente.
internal static class ReferenciasSesion
{
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

	public static Task AsegurarOperadorAsync(SQLiteAsyncConnection conexion, string cuenta, string rol) =>
		conexion.ExecuteAsync(
			"""
			INSERT INTO "operador_local" ("cuenta", "rol") VALUES (?, ?)
			ON CONFLICT("cuenta") DO UPDATE SET "rol" = excluded."rol" WHERE excluded."rol" <> '';
			""",
			cuenta,
			rol);

	// Una unidad vista desde una incidencia solo trae la clave y no debe pisar lo que dejó una sesión.
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
