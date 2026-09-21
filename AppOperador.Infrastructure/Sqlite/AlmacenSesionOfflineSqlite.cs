using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Sesión persistida en SQLite, para reanudarla tras cerrar la app (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// <c>sesion_local</c> es un historial: cada validación agrega o actualiza su fila y la marca
/// como vigente, quitándole la marca a la anterior. <b>Solo hay una vigente</b>, que es la que
/// se reanuda; las demás se quedan porque las incidencias y la bitácora apuntan a ellas.
/// Conservarlas no da material para reanudar una vencida: reanudar mira la vigente y nada más.
/// </para>
/// <para>
/// Limpiar no borra: quita la marca. Borrar una sesión a la que apunta una incidencia lo
/// impide la llave foránea, y de todos modos el rastro de con qué sesión se capturó algo es
/// justo lo que JTT-1383 CA 12 quiere conservar.
/// </para>
/// </remarks>
public sealed class AlmacenSesionOfflineSqlite : IOfflineSessionStore
{
	private const char Separador = ',';

	private readonly BaseDatosLocal _baseDatos;

	public AlmacenSesionOfflineSqlite(BaseDatosLocal baseDatos) => _baseDatos = baseDatos;

	/// <inheritdoc />
	public async Task GuardarAsync(SesionOfflinePersistida sesion, CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(sesion);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		// Primero a quién apunta la sesión, después la sesión: con llaves activas es el único
		// orden que SQLite acepta.
		await ReferenciasSesion.AsegurarOperadorAsync(conexion, sesion.Operador, sesion.Rol);
		await ReferenciasSesion.AsegurarUnidadAsync(conexion, sesion.Unidad.Clave, sesion.Unidad.Id, sesion.Unidad.Descripcion);

		await conexion.RunInTransactionAsync(tx =>
		{
			tx.Execute("UPDATE \"sesion_local\" SET \"vigente\" = 0 WHERE \"vigente\" = 1;");

			// Sobre la misma sesión —una revalidación— se actualiza la fila; una distinta se
			// agrega. La vigente anterior ya soltó la marca, así que el índice único no protesta.
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

	/// <inheritdoc />
	public async Task<SesionOfflinePersistida?> ObtenerAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		// El identificador técnico y la descripción de la unidad viven en unidad_local; se
		// traen en la misma consulta para reconstruir la sesión tal como se guardó.
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

	/// <inheritdoc />
	public async Task LimpiarAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.ExecuteAsync("UPDATE \"sesion_local\" SET \"vigente\" = 0 WHERE \"vigente\" = 1;");
	}

	/// <summary>
	/// Rehidrata la fila, o devuelve <see langword="null"/> si no es aprovechable.
	/// </summary>
	/// <remarks>
	/// Una fila corrupta o con fechas imposibles se trata como «no hay sesión guardada»: la
	/// consecuencia es pedir autenticación, que es la salida segura. Recuperar a medias una
	/// sesión ilegible sería peor que no tenerla.
	/// </remarks>
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

	/// <summary>La sesión vigente con lo que la unidad sabe de sí misma. Solo para la consulta de arriba.</summary>
	private sealed class FilaSesionVigente : SesionLocal
	{
		public string? UnidadId { get; set; }

		public string? UnidadDescripcion { get; set; }
	}
}
