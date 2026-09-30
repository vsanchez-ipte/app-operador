using System.Text;
using AppOperador.Aplicacion.Modelos;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Esquema;

// SQLite no agrega llaves a una tabla existente: se reconstruye. Una sola migración para cualquier versión, todo o nada.
internal sealed class MigradorEsquema
{
	private const string SufijoProvisional = "__migracion";

	private readonly string _ruta;
	private readonly SQLiteOpenFlags _banderas;
	private readonly string? _clave;

	public MigradorEsquema(string ruta, SQLiteOpenFlags banderas, string? clave)
	{
		_ruta = ruta;
		_banderas = banderas;
		_clave = clave;
	}

	public int? Aplicar()
	{
		int version;
		using (var conexion = Abrir())
		{
			version = conexion.ExecuteScalar<int>("PRAGMA user_version;");
		}

		if (version >= EsquemaLocal.Version)
		{
			return null;
		}

		// Una base en versión 0 acaba de nacer: nada que respaldar.
		var respaldo = version >= 1 ? Respaldar(version) : null;

		try
		{
			using var conexion = Abrir();

			// Apagadas mientras se reconstruye; el PRAGMA no surte efecto dentro de una transacción.
			conexion.Execute("PRAGMA foreign_keys = OFF;");
			conexion.RunInTransaction(() => Migrar(conexion, version));
			conexion.Execute("PRAGMA foreign_keys = ON;");
		}
		catch
		{
			Restaurar(respaldo);
			throw;
		}

		if (respaldo is not null)
		{
			File.Delete(respaldo);
		}

		return version;
	}

	private SQLiteConnection Abrir() =>
		new(new SQLiteConnectionString(_ruta, _banderas, storeDateTimeAsTicks: true, key: _clave));

	private string Respaldar(int version)
	{
		var respaldo = $"{_ruta}.v{version}.bak";
		File.Copy(_ruta, respaldo, overwrite: true);
		return respaldo;
	}

	// Si ni eso se puede, el respaldo queda para recuperarlo a mano.
	private void Restaurar(string? respaldo)
	{
		if (respaldo is null || !File.Exists(respaldo))
		{
			return;
		}

		File.Copy(respaldo, _ruta, overwrite: true);
		File.Delete(respaldo);
	}

	private static void Migrar(SQLiteConnection conexion, int versionAnterior)
	{
		// El catálogo de tipos cambió de llave de texto a entero: se tira y lo llena la primera descarga.
		if (versionAnterior < 4)
		{
			conexion.Execute("DROP TABLE IF EXISTS \"catalogo_tipo_incidencia\";");
		}

		var informe = new InformeMigracion();

		foreach (var tabla in EsquemaLocal.Tablas)
		{
			Reconstruir(conexion, tabla);

			// Aquí y no al final: los intentos de evidencia solo se copian para las que se quedan.
			if (tabla == EsquemaLocal.EvidenciaLocal)
			{
				informe.EvidenciasSinIncidencia = DescartarEvidenciasSinIncidencia(conexion);
			}
		}

		PoblarReferencias(conexion, informe);

		foreach (var tabla in EsquemaLocal.Tablas)
		{
			// Las tablas renombradas ya se copiaron a las nuevas; ahora se pueden tirar.
			if (tabla.NombreAnterior is not null)
			{
				conexion.Execute($"DROP TABLE IF EXISTS \"{tabla.NombreAnterior}\";");
			}
		}

		ComprobarLlaves(conexion);

		// Una base recién nacida no tiene nada que contar.
		if (versionAnterior > 0)
		{
			DejarConstancia(conexion, versionAnterior, informe);
		}

		conexion.Execute($"PRAGMA user_version = {EsquemaLocal.Version};");
	}

	private static void Reconstruir(SQLiteConnection conexion, DefinicionTabla tabla)
	{
		if (ExisteTabla(conexion, tabla.Nombre))
		{
			// Ya existe: se reconstruye sobre sí misma para sumarle llaves, valores por omisión y columnas.
			var provisional = tabla.Nombre + SufijoProvisional;

			conexion.Execute($"DROP TABLE IF EXISTS \"{provisional}\";");
			conexion.Execute(tabla.SentenciaCrear(provisional));
			Copiar(conexion, tabla, tabla.Nombre, provisional, condicion: null);
			conexion.Execute($"DROP TABLE \"{tabla.Nombre}\";");
			conexion.Execute($"ALTER TABLE \"{provisional}\" RENAME TO \"{tabla.Nombre}\";");
		}
		else
		{
			conexion.Execute(tabla.SentenciaCrear());

			if (tabla.NombreAnterior is not null && ExisteTabla(conexion, tabla.NombreAnterior))
			{
				Copiar(conexion, tabla, tabla.NombreAnterior, tabla.Nombre, CondicionDesdeAnterior(tabla));
			}
		}

		foreach (var indice in tabla.Indices)
		{
			conexion.Execute(indice);
		}
	}

	private static void Copiar(
		SQLiteConnection conexion,
		DefinicionTabla tabla,
		string origen,
		string destino,
		string? condicion)
	{
		var sentencia = tabla.SentenciaCopiar(origen, destino, ColumnasDe(conexion, origen), condicion);

		if (sentencia is not null)
		{
			conexion.Execute(sentencia);
		}
	}

	// Solo pasan los intentos cuyo registro existe y la sesión que tiene operador.
	private static string? CondicionDesdeAnterior(DefinicionTabla tabla)
	{
		if (tabla == EsquemaLocal.IntentoIncidencia)
		{
			return $"\"clase\" = {(int)ClaseRegistro.Incidencia} " +
				"AND \"registro_uuid\" IN (SELECT \"uuid\" FROM \"incidencia_local\")";
		}

		if (tabla == EsquemaLocal.IntentoEvidencia)
		{
			return $"\"clase\" = {(int)ClaseRegistro.Evidencia} " +
				"AND \"registro_uuid\" IN (SELECT \"uuid\" FROM \"evidencia_local\")";
		}

		if (tabla == EsquemaLocal.SesionLocal)
		{
			return "\"Operador\" IS NOT NULL AND \"Operador\" <> ''";
		}

		return null;
	}

	// Crea los operadores, unidades y sesiones a que apuntan las filas. El vacío pasa a NULL en las llaves.
	private static void PoblarReferencias(SQLiteConnection conexion, InformeMigracion informe)
	{
		foreach (var tabla in EsquemaLocal.Tablas)
		{
			foreach (var columna in tabla.Columnas)
			{
				if (columna.Referencia is not null && columna.Nulable)
				{
					conexion.Execute(
						$"UPDATE \"{tabla.Nombre}\" SET \"{columna.Nombre}\" = NULL WHERE \"{columna.Nombre}\" = '';");
				}
			}
		}

		// La sesión guardada es la única que sabe id y descripción de su unidad: va primero.
		if (ExisteTabla(conexion, "SesionLocal"))
		{
			conexion.Execute(
				"""
				INSERT OR IGNORE INTO "unidad_local" ("clave", "id", "descripcion")
				SELECT "UnidadClave", COALESCE("UnidadId", ''), COALESCE("UnidadDescripcion", '')
				FROM "SesionLocal"
				WHERE "UnidadClave" IS NOT NULL AND "UnidadClave" <> '';
				""");

			conexion.Execute(
				"""
				UPDATE "sesion_local" SET "vigente" = 1
				WHERE "session_id" = (SELECT COALESCE("SessionId", '') FROM "SesionLocal" WHERE "Operador" IS NOT NULL AND "Operador" <> '' LIMIT 1);
				""");
		}

		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "operador_local" ("cuenta", "rol")
			SELECT "operador", "rol" FROM "sesion_local";
			""");

		// Del más reciente al más antiguo: con OR IGNORE queda el último rol visto.
		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "operador_local" ("cuenta", "rol")
			SELECT "operador", COALESCE("rol", '') FROM "evento_auditoria"
			WHERE "operador" IS NOT NULL AND "operador" <> ''
			ORDER BY "id" DESC;
			""");

		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "operador_local" ("cuenta", "rol")
			SELECT DISTINCT "operador", '' FROM "incidencia_local" WHERE "operador" IS NOT NULL;
			""");

		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "unidad_local" ("clave", "id", "descripcion")
			SELECT DISTINCT "unidad_vehicular", '', '' FROM "incidencia_local" WHERE "unidad_vehicular" IS NOT NULL;
			""");

		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "unidad_local" ("clave", "id", "descripcion")
			SELECT DISTINCT "unidad_clave", '', '' FROM "evento_auditoria"
			WHERE "unidad_clave" IS NOT NULL AND "unidad_clave" <> '';
			""");

		// Sesiones de turnos anteriores, reconstruidas desde la bitácora y las incidencias.
		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "sesion_local"
				("session_id", "operador", "unidad_clave", "rol", "permisos",
				 "validado_utc_ticks", "offline_hasta_utc_ticks", "monotonico_al_validar_ticks",
				 "version_aplicacion", "version_catalogos", "vigente")
			SELECT "sesion_id", MAX("operador"), NULLIF(MAX(COALESCE("unidad_clave", '')), ''),
				COALESCE(MAX("rol"), ''), COALESCE(MAX("permiso"), ''),
				MIN("instante_utc_ticks"), 0, 0, '', '', 0
			FROM "evento_auditoria"
			WHERE "sesion_id" IS NOT NULL AND "operador" IS NOT NULL AND "operador" <> ''
			GROUP BY "sesion_id";
			""");

		conexion.Execute(
			"""
			INSERT OR IGNORE INTO "sesion_local"
				("session_id", "operador", "unidad_clave", "rol", "permisos",
				 "validado_utc_ticks", "offline_hasta_utc_ticks", "monotonico_al_validar_ticks",
				 "version_aplicacion", "version_catalogos", "vigente")
			SELECT "sesion_origen", MAX("operador"), NULLIF(MAX(COALESCE("unidad_vehicular", '')), ''),
				'', COALESCE(MAX("permiso_origen"), ''),
				MIN("creado_utc_ticks"), 0, 0, '', COALESCE(MAX("version_catalogo"), ''), 0
			FROM "incidencia_local"
			WHERE "sesion_origen" IS NOT NULL AND "operador" IS NOT NULL
			GROUP BY "sesion_origen";
			""");

		// Lo que apunta a una sesión desconocida se desengancha: mejor eso que una base que no abre.
		informe.ReferenciasSinSesion += conexion.Execute(
			"""
			UPDATE "incidencia_local" SET "sesion_origen" = NULL
			WHERE "sesion_origen" IS NOT NULL
			  AND "sesion_origen" NOT IN (SELECT "session_id" FROM "sesion_local");
			""");

		informe.ReferenciasSinSesion += conexion.Execute(
			"""
			UPDATE "evento_auditoria" SET "sesion_id" = NULL
			WHERE "sesion_id" IS NOT NULL
			  AND "sesion_id" NOT IN (SELECT "session_id" FROM "sesion_local");
			""");

		if (ExisteTabla(conexion, "intento_sincronizacion"))
		{
			var total = conexion.ExecuteScalar<int>("SELECT COUNT(*) FROM \"intento_sincronizacion\";");
			var repartidos = conexion.ExecuteScalar<int>("SELECT COUNT(*) FROM \"intento_incidencia\";")
				+ conexion.ExecuteScalar<int>("SELECT COUNT(*) FROM \"intento_evidencia\";");
			informe.IntentosSinRegistro = total - repartidos;
		}
	}

	// Sin incidencia no tienen dueño ni forma de enviarse.
	private static int DescartarEvidenciasSinIncidencia(SQLiteConnection conexion) =>
		conexion.Execute(
			"""
			DELETE FROM "evidencia_local"
			WHERE "incidencia_uuid" NOT IN (SELECT "uuid" FROM "incidencia_local");
			""");

	private static void ComprobarLlaves(SQLiteConnection conexion)
	{
		var violaciones = conexion.Query<ViolacionLlave>("PRAGMA foreign_key_check;");

		if (violaciones.Count > 0)
		{
			var detalle = string.Join("; ", violaciones
				.GroupBy(v => (v.Table, v.Parent))
				.Select(g => $"{g.Key.Table} → {g.Key.Parent}: {g.Count()} filas"));

			throw new InvalidOperationException(
				$"La migración dejó llaves foráneas sin resolver: {detalle}.");
		}
	}

	// Sin operador ni origen, para que la vea cualquiera.
	private static void DejarConstancia(SQLiteConnection conexion, int versionAnterior, InformeMigracion informe)
	{
		var mensaje = new StringBuilder();

		mensaje.Append(
			$"Base local migrada del esquema {versionAnterior} al {EsquemaLocal.Version}: " +
			$"{EsquemaLocal.Tablas.Count} tablas reconstruidas con llaves foráneas.");

		if (informe.EvidenciasSinIncidencia > 0)
		{
			mensaje.Append($" Se descartaron {informe.EvidenciasSinIncidencia} evidencias sin incidencia.");
		}

		if (informe.IntentosSinRegistro > 0)
		{
			mensaje.Append($" Se descartaron {informe.IntentosSinRegistro} intentos de envío sin registro.");
		}

		if (informe.ReferenciasSinSesion > 0)
		{
			mensaje.Append($" {informe.ReferenciasSinSesion} filas apuntaban a sesiones sin datos y quedaron sin enlace.");
		}

		conexion.Execute(
			"""
			INSERT INTO "evento_auditoria"
				("instante_utc_ticks", "monotonico_ticks", "nivel", "mensaje", "operacion", "operador", "origen")
			VALUES (?, 0, ?, ?, ?, NULL, NULL);
			""",
			DateTime.UtcNow.Ticks,
			(int)NivelAuditoria.Info,
			mensaje.ToString(),
			(int)OperacionAuditada.Otra);
	}

	private static bool ExisteTabla(SQLiteConnection conexion, string nombre) =>
		conexion.ExecuteScalar<int>(
			"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = ?;", nombre) > 0;

	private static HashSet<string> ColumnasDe(SQLiteConnection conexion, string tabla) =>
		conexion.Query<InfoColumna>($"PRAGMA table_info(\"{tabla}\");")
			.Select(c => c.name)
			.ToHashSet(StringComparer.Ordinal);

	private sealed class InformeMigracion
	{
		public int EvidenciasSinIncidencia { get; set; }

		public int IntentosSinRegistro { get; set; }

		public int ReferenciasSinSesion { get; set; }
	}

	private sealed class InfoColumna
	{
		public string name { get; set; } = string.Empty;
	}

	private sealed class ViolacionLlave
	{
		[Column("table")]
		public string Table { get; set; } = string.Empty;

		[Column("parent")]
		public string Parent { get; set; } = string.Empty;
	}
}
