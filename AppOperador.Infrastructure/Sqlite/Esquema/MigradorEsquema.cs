using System.Text;
using AppOperador.Aplicacion.Modelos;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Esquema;

/// <summary>
/// Lleva el archivo local hasta <see cref="EsquemaLocal.Version"/>, desde cualquier versión
/// anterior o desde cero.
/// </summary>
/// <remarks>
/// <para>
/// <b>SQLite no sabe agregar una llave foránea a una tabla que ya existe</b>, así que cada
/// tabla se reconstruye: se crea la nueva, se copian las filas, se tira la vieja y se renombra.
/// Es el procedimiento que documenta SQLite para <c>ALTER TABLE</c>, con las llaves apagadas
/// mientras dura y <c>PRAGMA foreign_key_check</c> antes de confirmar.
/// </para>
/// <para>
/// <b>Una sola migración para todas las versiones.</b> La copia toma solo las columnas que la
/// base vieja tenga; lo que le falte queda con su valor por omisión. Así una base de la versión
/// 3 y una de la 10 llegan a la 11 por el mismo camino, y una base nueva también: sin nada que
/// copiar, solo crea.
/// </para>
/// <para>
/// <b>O queda en la versión nueva completa o queda como estaba.</b> Todo corre en una
/// transacción, y además se copia el archivo antes de empezar: si algo falla, se restaura la
/// copia y la excepción sale. El arranque siguiente lo vuelve a intentar. Con datos de campo
/// en el teléfono, una base intacta en la versión vieja vale más que una a medias en la nueva.
/// </para>
/// </remarks>
internal sealed class MigradorEsquema
{
	/// <summary>Sufijo de la tabla provisional mientras se reconstruye una.</summary>
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

	/// <summary>
	/// Aplica lo que haga falta. Devuelve la versión desde la que se migró, o
	/// <see langword="null"/> si el archivo ya estaba al día.
	/// </summary>
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

		// Una base en la versión 0 acaba de nacer: no hay nada que respaldar.
		var respaldo = version >= 1 ? Respaldar(version) : null;

		try
		{
			using var conexion = Abrir();

			// Apagadas mientras se reconstruye, como manda el procedimiento de SQLite: con
			// ellas encendidas, tirar una tabla padre fallaría por los hijos que la apuntan.
			// El PRAGMA no tiene efecto dentro de una transacción, por eso va antes.
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

	/// <summary>
	/// Devuelve el archivo a como estaba. Si ni eso se puede, el respaldo se queda en su sitio
	/// para recuperarlo a mano.
	/// </summary>
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
		// De 3 a 4 (JTT-1394): el catálogo de tipos cambió de llave, de una clave de texto
		// inventada en la maqueta al entero de Jacob. No hay correspondencia posible, así que se
		// tira y lo llena la primera descarga del catálogo real.
		if (versionAnterior < 4)
		{
			conexion.Execute("DROP TABLE IF EXISTS \"catalogo_tipo_incidencia\";");
		}

		var informe = new InformeMigracion();

		foreach (var tabla in EsquemaLocal.Tablas)
		{
			Reconstruir(conexion, tabla);

			// Va aquí y no al final porque los intentos de evidencia, que vienen después, solo
			// deben copiarse para las evidencias que se quedan.
			if (tabla == EsquemaLocal.EvidenciaLocal)
			{
				informe.EvidenciasSinIncidencia = DescartarEvidenciasSinIncidencia(conexion);
			}
		}

		PoblarReferencias(conexion, informe);

		// Las tablas de la versión anterior que cambiaron de nombre ya se vaciaron en las
		// nuevas; las de arriba las leyeron todas, así que ahora sí se pueden tirar.
		foreach (var tabla in EsquemaLocal.Tablas)
		{
			if (tabla.NombreAnterior is not null)
			{
				conexion.Execute($"DROP TABLE IF EXISTS \"{tabla.NombreAnterior}\";");
			}
		}

		ComprobarLlaves(conexion);

		// Una base recién nacida no tiene nada que contar; una migrada, sí.
		if (versionAnterior > 0)
		{
			DejarConstancia(conexion, versionAnterior, informe);
		}

		conexion.Execute($"PRAGMA user_version = {EsquemaLocal.Version};");
	}

	/// <summary>
	/// Deja la tabla con su forma definitiva, conservando lo que hubiera en ella o en la tabla
	/// de la versión anterior de la que viene.
	/// </summary>
	private static void Reconstruir(SQLiteConnection conexion, DefinicionTabla tabla)
	{
		if (ExisteTabla(conexion, tabla.Nombre))
		{
			// Ya existe con su nombre: se reconstruye sobre sí misma, para agregarle las
			// llaves, los valores por omisión y las columnas que le falten.
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

	/// <summary>
	/// Qué filas de la tabla anterior pasan a esta, cuando no son todas.
	/// </summary>
	/// <remarks>
	/// <c>intento_sincronizacion</c> se reparte por <c>clase</c> entre las dos tablas de
	/// intentos, y solo pasan los intentos cuyo registro sigue existiendo: los demás no tendrían
	/// a quién apuntar. La sesión guardada pasa solo si tiene operador, porque sin él no hay
	/// llave que la sostenga.
	/// </remarks>
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

	/// <summary>
	/// Crea los operadores, unidades y sesiones a los que las filas ya guardadas tienen que
	/// apuntar, y desengancha lo que no se pueda sostener.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Antes de la versión 11 la sesión era una sola fila que se sobrescribía: las sesiones de
	/// turnos anteriores solo sobreviven como texto en las incidencias y en la bitácora. De ahí
	/// se reconstruyen, con <c>vigente = 0</c> y con lo poco que esas filas saben de ellas.
	/// </para>
	/// <para>
	/// <b>El vacío pasa a <c>NULL</c></b> en toda columna que sea llave: una llave foránea admite
	/// no apuntar a nada, pero no apuntar a una cadena vacía que no existe.
	/// </para>
	/// </remarks>
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

		// La sesión que estaba guardada es la única que trae el identificador técnico y la
		// descripción de su unidad; va primero para que nadie la pise con un vacío.
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

		// Del más reciente al más antiguo: con OR IGNORE, el rol que queda es el último que se
		// le vio a cada operador.
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

		// Sesiones de turnos anteriores, reconstruidas desde la bitácora —que sabe rol, permiso
		// y unidad— y desde las incidencias —que saben unidad y versión de catálogo—.
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

		// Lo que apunta a una sesión de la que no se sabe ni el operador se desengancha: es
		// preferible una incidencia sin sesión a una base que no abre.
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

	/// <summary>
	/// Una evidencia cuya incidencia ya no existe no tiene dueño ni forma de enviarse: es lo
	/// que dejaba borrar un borrador sin quitar antes sus adjuntos.
	/// </summary>
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

	/// <summary>
	/// Escribe en la bitácora qué pasó, sin operador ni origen para que la vea cualquiera:
	/// así se reconocen las líneas anteriores al esquema 10, y así se muestra esta.
	/// </summary>
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

	/// <summary>Lo que se le cuenta al operador en la bitácora al terminar.</summary>
	private sealed class InformeMigracion
	{
		public int EvidenciasSinIncidencia { get; set; }

		public int IntentosSinRegistro { get; set; }

		public int ReferenciasSinSesion { get; set; }
	}

	/// <summary>Forma de una fila de <c>PRAGMA table_info</c>. Solo interesa el nombre.</summary>
	private sealed class InfoColumna
	{
		public string name { get; set; } = string.Empty;
	}

	/// <summary>Forma de una fila de <c>PRAGMA foreign_key_check</c>.</summary>
	private sealed class ViolacionLlave
	{
		[Column("table")]
		public string Table { get; set; } = string.Empty;

		[Column("parent")]
		public string Parent { get; set; } = string.Empty;
	}
}
