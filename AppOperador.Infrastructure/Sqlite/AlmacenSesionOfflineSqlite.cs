using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

// Historial con una sola vigente; limpiar quita la marca, no borra, porque otras filas apuntan a ellas.
public sealed class AlmacenSesionOfflineSqlite : IOfflineSessionStore
{
	private const char Separador = ',';

	private readonly BaseDatosLocal _baseDatos;

	public AlmacenSesionOfflineSqlite(BaseDatosLocal baseDatos) => _baseDatos = baseDatos;

	public async Task GuardarAsync(SesionOfflinePersistida sesion, CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(sesion);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		// Primero operador y unidad: con llaves activas es el único orden posible.
		await ReferenciasSesion.AsegurarOperadorAsync(conexion, sesion.Operador, sesion.Rol);
		await ReferenciasSesion.AsegurarUnidadAsync(conexion, sesion.Unidad.Clave, sesion.Unidad.Id, sesion.Unidad.Descripcion);

		await conexion.RunInTransactionAsync(tx =>
		{
			tx.Execute("UPDATE \"sesion_local\" SET \"vigente\" = 0 WHERE \"vigente\" = 1;");

			tx.Execute(
				"""
				INSERT INTO "sesion_local"
					("session_id", "operador", "unidad_clave", "rol", "permisos",
					 "validado_utc_ticks", "offline_hasta_utc_ticks", "monotonico_al_validar_ticks",
					 "version_aplicacion", "version_catalogos", "vigente")
				VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)
				ON CONFLICT("session_id") DO UPDATE SET
					"operador" = excluded."operador",
					"unidad_clave" = excluded."unidad_clave",
					"rol" = excluded."rol",
					"permisos" = excluded."permisos",
					"validado_utc_ticks" = excluded."validado_utc_ticks",
					"offline_hasta_utc_ticks" = excluded."offline_hasta_utc_ticks",
					"monotonico_al_validar_ticks" = excluded."monotonico_al_validar_ticks",
					"version_aplicacion" = excluded."version_aplicacion",
					"version_catalogos" = excluded."version_catalogos",
					"vigente" = 1;
				""",
				sesion.SessionId,
				sesion.Operador,
				sesion.Unidad.Clave,
				sesion.Rol,
				string.Join(Separador, sesion.Permisos),
				sesion.Vigencia.LastValidatedAtUtc.Ticks,
				sesion.Vigencia.OfflineUntilUtc.Ticks,
				sesion.MonotonicoAlValidar.Ticks,
				sesion.Instalacion.VersionAplicacion,
				sesion.Instalacion.VersionCatalogos.ToString("yyyy-MM-dd"));
		});
	}

	public async Task<SesionOfflinePersistida?> ObtenerAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var filas = await conexion.QueryAsync<FilaSesionVigente>(
			"""
			SELECT s.*, u."id" AS "UnidadId", u."descripcion" AS "UnidadDescripcion"
			FROM "sesion_local" s
			LEFT JOIN "unidad_local" u ON u."clave" = s."unidad_clave"
			WHERE s."vigente" = 1
			LIMIT 1;
			""");

		return filas.Count == 0 ? null : Convertir(filas[0]);
	}

	public async Task LimpiarAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.ExecuteAsync("UPDATE \"sesion_local\" SET \"vigente\" = 0 WHERE \"vigente\" = 1;");
	}

	// Una fila ilegible se trata como «no hay sesión»: pedir autenticación es lo seguro.
	private static SesionOfflinePersistida? Convertir(FilaSesionVigente fila)
	{
		if (fila.OfflineHastaUtcTicks < fila.ValidadoUtcTicks)
		{
			return null;
		}

		VigenciaOffline vigencia;
		try
		{
			vigencia = VigenciaOffline.DelServidor(
				new DateTime(fila.ValidadoUtcTicks, DateTimeKind.Utc),
				new DateTime(fila.OfflineHastaUtcTicks, DateTimeKind.Utc));
		}
		catch (ArgumentException)
		{
			return null;
		}

		if (!DateOnly.TryParse(fila.VersionCatalogos, out var catalogos))
		{
			catalogos = DateOnly.FromDateTime(vigencia.LastValidatedAtUtc);
		}

		return new SesionOfflinePersistida(
			SessionId: fila.SessionId,
			Operador: fila.Operador,
			Rol: fila.Rol,
			Unidad: new UnidadVehicular(fila.UnidadId ?? string.Empty, fila.UnidadClave ?? string.Empty, fila.UnidadDescripcion ?? string.Empty),
			Permisos: PermisosOperador.DelServidor(
				fila.Permisos.Split(Separador, StringSplitOptions.RemoveEmptyEntries)),
			Vigencia: vigencia,
			MonotonicoAlValidar: TimeSpan.FromTicks(fila.MonotonicoAlValidarTicks),
			Instalacion: new DatosDeInstalacion(fila.VersionAplicacion, catalogos));
	}

	private sealed class FilaSesionVigente : SesionLocal
	{
		public string? UnidadId { get; set; }

		public string? UnidadDescripcion { get; set; }
	}
}
