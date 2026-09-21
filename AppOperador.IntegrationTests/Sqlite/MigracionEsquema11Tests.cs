using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// La migración del esquema 10 al 11: llaves foráneas, sesión como historial e intentos por
/// tabla, sin perder una sola fila de lo que había en el dispositivo.
/// </summary>
/// <remarks>
/// La base de partida es <c>Fixtures/esquema-10.sql</c>, con el DDL exacto que generaba
/// sqlite-net y datos de todos los tipos. Lo que aquí se afirma es lo que un teléfono de QA
/// con cola de campo tiene que conservar al actualizar la app.
/// </remarks>
public sealed class MigracionEsquema11Tests : IAsyncDisposable
{
	private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"appoperador-migracion-{Guid.NewGuid():N}.db3");
	private readonly List<BaseDatosLocal> _abiertas = [];

	private async Task<BaseDatosLocal> MigrarDesdeVersion10Async(IDatabaseKeyProvider? claves = null)
	{
		BaseDeVersionAnterior.Crear(10, _ruta);

		var baseDatos = new BaseDatosLocal(_ruta, claves);
		_abiertas.Add(baseDatos);
		await baseDatos.InicializarAsync();
		return baseDatos;
	}

	[Fact]
	public async Task Migra_a_la_version_11_con_llaves_activas_y_sin_violaciones()
	{
		var baseDatos = await MigrarDesdeVersion10Async();

		Assert.Equal(11, await baseDatos.ObtenerVersionEsquemaAsync());
		Assert.Empty(BaseDeVersionAnterior.Consultar<Violacion>(_ruta, "PRAGMA foreign_key_check;"));

		// Las tablas viejas ya no están y las trece nuevas sí.
		var tablas = BaseDeVersionAnterior.Consultar<Nombre>(_ruta,
			"SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name")
			.Select(t => t.name).ToList();

		Assert.Equal(
		[
			"catalogo_afectacion", "catalogo_cuerpo", "catalogo_meta", "catalogo_severidad", "catalogo_tipo_incidencia",
			"evento_auditoria", "evidencia_local", "incidencia_local", "intento_evidencia", "intento_incidencia",
			"operador_local", "sesion_local", "unidad_local",
		], tablas);

		Assert.False(File.Exists(_ruta + ".v10.bak"), "El respaldo se retira cuando la migración termina bien.");
	}

	[Fact]
	public async Task Conserva_cada_incidencia_con_sus_columnas_renombradas()
	{
		await MigrarDesdeVersion10Async();

		Assert.Equal(6, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM incidencia_local"));

		var enviada = Fila("SELECT * FROM incidencia_local WHERE uuid = 'i-1'");
		Assert.Equal("INC-APK-2026-0034", enviada["folio_central"]);
		Assert.Equal("s-010", enviada["sesion_origen"]);
		Assert.Equal("APP_OPERADOR_MOVIL", enviada["permiso_origen"]);
		Assert.Equal(12345L, enviada["monotonico_ticks"]);
		Assert.Equal("ana.lopez", enviada["operador"]);
		Assert.Equal("VEH-01", enviada["unidad_vehicular"]);
		Assert.Equal(19.4326, enviada["gps_latitud"]);

		// La capturada sin sesión queda con las llaves en NULL, no en cadena vacía.
		var simulada = Fila("SELECT * FROM incidencia_local WHERE uuid = 'i-5'");
		Assert.Null(simulada["operador"]);
		Assert.Null(simulada["unidad_vehicular"]);
		Assert.Null(simulada["sesion_origen"]);
		Assert.Equal("OBJETO", simulada["tipo_clave"]);
		Assert.Equal(2L, simulada["gravedad"]);

		// El borrador tampoco tenía sesión, pero sí operador y unidad.
		var borrador = Fila("SELECT * FROM incidencia_local WHERE uuid = 'i-4'");
		Assert.Equal("ana.lopez", borrador["operador"]);
		Assert.Null(borrador["sesion_origen"]);
		Assert.Equal("012+", borrador["kilometro"]);
	}

	[Fact]
	public async Task Reconstruye_operadores_unidades_y_sesiones_desde_lo_guardado()
	{
		await MigrarDesdeVersion10Async();

		// Operadores: los dos que aparecen, con el rol de su sesión o de su bitácora.
		var operadores = BaseDeVersionAnterior.Consultar<Operador>(_ruta, "SELECT cuenta, rol FROM operador_local ORDER BY cuenta");
		Assert.Equal(2, operadores.Count);
		Assert.Equal(("ana.lopez", "Operador de campo"), (operadores[0].cuenta, operadores[0].rol));
		Assert.Equal(("luis.mtz", "Supervisor"), (operadores[1].cuenta, operadores[1].rol));

		// Unidades: la de la sesión guardada trae identificador y descripción; las demás, solo clave.
		var unidades = BaseDeVersionAnterior.Consultar<Unidad>(_ruta, "SELECT clave, id, descripcion FROM unidad_local ORDER BY clave");
		Assert.Equal(["VEH-01", "VEH-03", "VEH-07"], unidades.Select(u => u.clave));
		Assert.Equal(("u-7", "Camioneta 07"), (unidades[2].id, unidades[2].descripcion));
		Assert.Equal(("", ""), (unidades[0].id, unidades[0].descripcion));

		// Sesiones: la guardada queda vigente; s-010 y s-020 se reconstruyen desde la bitácora;
		// s-099 no tiene operador y no se puede sostener.
		var sesiones = BaseDeVersionAnterior.Consultar<Sesion>(_ruta,
			"SELECT session_id, operador, unidad_clave, rol, vigente FROM sesion_local ORDER BY session_id");
		Assert.Equal(["s-010", "s-020", "s-030"], sesiones.Select(s => s.session_id));
		Assert.Equal(("ana.lopez", "VEH-01", "Operador de campo", 0), (sesiones[0].operador, sesiones[0].unidad_clave, sesiones[0].rol, sesiones[0].vigente));
		Assert.Equal(("luis.mtz", "VEH-03", "Supervisor", 0), (sesiones[1].operador, sesiones[1].unidad_clave, sesiones[1].rol, sesiones[1].vigente));
		Assert.Equal(("ana.lopez", "VEH-07", "Operador de campo", 1), (sesiones[2].operador, sesiones[2].unidad_clave, sesiones[2].rol, sesiones[2].vigente));

		// La línea que apuntaba a s-099 sigue ahí, sin enlace.
		Assert.Equal(9, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM evento_auditoria WHERE id <= 9"));
		Assert.Null(Fila("SELECT * FROM evento_auditoria WHERE id = 9")["sesion_id"]);
		Assert.Equal("s-010", Fila("SELECT * FROM evento_auditoria WHERE id = 4")["sesion_id"]);
	}

	[Fact]
	public async Task Reparte_los_intentos_y_descarta_lo_que_no_tiene_dueno()
	{
		await MigrarDesdeVersion10Async();

		// Cuatro intentos de incidencia con su id original, uno de evidencia, y el huérfano fuera.
		var deIncidencia = BaseDeVersionAnterior.Consultar<Intento>(_ruta,
			"SELECT id, incidencia_uuid AS registro, codigo, codigo_texto, mensaje FROM intento_incidencia ORDER BY id");
		Assert.Equal([1, 2, 3, 4], deIncidencia.Select(i => i.id));
		Assert.Equal(503, deIncidencia[0].codigo);
		Assert.Equal("appincidencias.km.fueradecorredor", deIncidencia[3].codigo_texto);

		var deEvidencia = BaseDeVersionAnterior.Consultar<Intento>(_ruta,
			"SELECT id, evidencia_uuid AS registro, codigo, codigo_texto, mensaje FROM intento_evidencia");
		var unico = Assert.Single(deEvidencia);
		Assert.Equal(("e-3", "appevidencias.tamano"), (unico.registro, unico.codigo_texto));

		// La evidencia huérfana se fue; las otras cuatro siguen.
		Assert.Equal(4, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM evidencia_local"));
		Assert.Equal(0, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM evidencia_local WHERE uuid = 'e-9'"));

		// Y la bitácora lo cuenta, en una línea que ve cualquiera.
		var constancia = Fila("SELECT * FROM evento_auditoria ORDER BY id DESC LIMIT 1");
		Assert.Null(constancia["operador"]);
		Assert.Null(constancia["origen"]);
		var mensaje = (string)constancia["mensaje"]!;
		Assert.Contains("migrada del esquema 10 al 11", mensaje);
		Assert.Contains("1 evidencias sin incidencia", mensaje);
		Assert.Contains("1 intentos de envío sin registro", mensaje);
		Assert.Contains("1 filas apuntaban a sesiones sin datos", mensaje);
	}

	[Fact]
	public async Task Conserva_el_catalogo_y_la_bitacora_anterior()
	{
		await MigrarDesdeVersion10Async();

		Assert.Equal(2, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM catalogo_tipo_incidencia"));
		Assert.Equal(2, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM catalogo_severidad"));
		Assert.Equal(2, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM catalogo_afectacion"));
		Assert.Equal(2, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM catalogo_cuerpo"));
		Assert.Equal(25, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT evidencia_tamano_maximo_mb FROM catalogo_meta"));

		// La línea anterior al esquema 10 sigue sin operador ni origen, y el monotónico nulo
		// pasó a cero porque la columna ya no admite nulos.
		var previa = Fila("SELECT * FROM evento_auditoria WHERE id = 1");
		Assert.Null(previa["operador"]);
		Assert.Null(previa["origen"]);
		Assert.Equal(0L, previa["monotonico_ticks"]);
		Assert.Equal(0L, previa["operacion"]);
	}

	[Fact]
	public async Task La_app_lee_lo_migrado_como_si_siempre_hubiera_estado_asi()
	{
		var baseDatos = await MigrarDesdeVersion10Async();
		var reloj = new RelojFijo();
		var sesion = new SesionFija("ana.lopez");

		// La cola de Ana: la enviada, la pendiente y la fallida con el motivo de su último intento.
		var cola = await new ColaSincronizacionSqlite(baseDatos, reloj, sesion).ObtenerRegistrosAsync();
		Assert.Equal(["LOC-673529", "LOC-673528", "LOC-673527"], cola.Select(r => r.ClaveLocal));
		var fallida = cola.Single(r => r.ClaveLocal == "LOC-673529");
		Assert.Equal(EstadoSincronizacion.Fallido, fallida.Estado);
		Assert.Equal("El kilómetro 300+000 sigue fuera del corredor", fallida.UltimoErrorMensaje);

		// Su borrador, con su evidencia.
		var repositorio = new RepositorioIncidenciasSqlite(baseDatos, reloj, sesion);
		var borrador = Assert.Single(await repositorio.ObtenerBorradoresAsync());
		Assert.Equal("LOC-673530", borrador.ClaveLocal);
		Assert.Equal(1, await new RepositorioEvidenciasSqlite(baseDatos).ContarDeIncidenciaAsync("i-4"));

		// La sesión guardada se reanuda con su unidad completa.
		var persistida = await new AlmacenSesionOfflineSqlite(baseDatos).ObtenerAsync();
		Assert.NotNull(persistida);
		Assert.Equal("s-030", persistida.SessionId);
		Assert.Equal(new UnidadVehicular("u-7", "VEH-07", "Camioneta 07"), persistida.Unidad);
		Assert.Equal(["APP_OPERADOR_MOVIL", "CAPTURA_INCIDENCIAS"], persistida.Permisos);

		// Y la bitácora de Ana trae lo suyo, más el historial previo y la constancia de la migración.
		var bitacora = new BitacoraAuditoriaSqlite(baseDatos, reloj, new MonotonicoFijo(), sesion, new ConectividadControlada());
		var eventos = await bitacora.ObtenerEventosAsync();
		Assert.Contains(eventos, e => e.Mensaje == "Aplicación iniciada");
		Assert.Contains(eventos, e => e.Mensaje.StartsWith("Base local migrada", StringComparison.Ordinal));
		Assert.Contains(eventos, e => e.Mensaje == "Incidencia LOC-673529 rechazada" && e.SesionId == "s-030");
		Assert.DoesNotContain(eventos, e => e.Operador == "luis.mtz");
	}

	[Fact]
	public async Task Las_llaves_se_hacen_valer_despues_de_migrar()
	{
		var baseDatos = await MigrarDesdeVersion10Async();
		var evidencias = new RepositorioEvidenciasSqlite(baseDatos);

		// Una evidencia de una incidencia inexistente ya no se puede escribir…
		await Assert.ThrowsAsync<SQLiteException>(() => evidencias.AgregarAsync(
			new EvidenciaAdjunta("e-nueva", "no-existe", "x.jpg", "image/jpeg", 1, "/x.jpg", EstadoSincronizacion.Pendiente, null)));

		// …y un borrador con adjuntos no se puede borrar sin quitarlos antes.
		var repositorio = new RepositorioIncidenciasSqlite(baseDatos, new RelojFijo(), new SesionFija("ana.lopez"));
		await Assert.ThrowsAsync<SQLiteException>(() => repositorio.EliminarBorradorAsync("LOC-673530"));

		await evidencias.EliminarAsync("e-4");
		Assert.True(await repositorio.EliminarBorradorAsync("LOC-673530"));
	}

	[Fact]
	public async Task Una_segunda_apertura_no_vuelve_a_migrar()
	{
		await MigrarDesdeVersion10Async();
		var constancias = BaseDeVersionAnterior.Escalar<int>(_ruta,
			"SELECT COUNT(*) FROM evento_auditoria WHERE mensaje LIKE 'Base local migrada%'");

		var otraVez = new BaseDatosLocal(_ruta);
		_abiertas.Add(otraVez);
		await otraVez.InicializarAsync();

		Assert.Equal(11, await otraVez.ObtenerVersionEsquemaAsync());
		Assert.Equal(constancias, BaseDeVersionAnterior.Escalar<int>(_ruta,
			"SELECT COUNT(*) FROM evento_auditoria WHERE mensaje LIKE 'Base local migrada%'"));
	}

	[Fact]
	public async Task Migra_igual_una_base_cifrada()
	{
		var claves = new ClaveFija();
		var baseDatos = await MigrarDesdeVersion10Async(claves);

		Assert.Equal(11, await baseDatos.ObtenerVersionEsquemaAsync());
		Assert.Equal(6, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM incidencia_local", ClaveFija.Clave));

		// Sigue cifrada: en claro no se deja leer.
		Assert.Throws<SQLiteException>(() => BaseDeVersionAnterior.Escalar<int>(_ruta, "PRAGMA user_version;"));
	}

	[Fact]
	public async Task Si_la_migracion_falla_la_base_queda_intacta_en_la_version_10()
	{
		BaseDeVersionAnterior.Crear(10, _ruta);

		// Dos incidencias con la misma clave local: el índice único de la versión 11 no lo
		// admite, y así se provoca un fallo a media reconstrucción.
		using (var directa = new SQLiteConnection(_ruta))
		{
			directa.Execute("DROP INDEX ix_incidencia_clave;");
			directa.Execute("INSERT INTO incidencia_local (uuid, clave_local, estado) VALUES ('i-dup', 'LOC-673528', 2);");
		}

		var baseDatos = new BaseDatosLocal(_ruta);
		_abiertas.Add(baseDatos);
		await Assert.ThrowsAnyAsync<Exception>(() => baseDatos.InicializarAsync());

		Assert.Equal(10, BaseDeVersionAnterior.Escalar<int>(_ruta, "PRAGMA user_version;"));
		Assert.Equal(7, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM incidencia_local"));
		Assert.Equal(1, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM SesionLocal"));
		Assert.Equal(6, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM intento_sincronizacion"));
		Assert.False(File.Exists(_ruta + ".v10.bak"));
	}

	[Fact]
	public async Task Una_base_nueva_nace_en_la_version_11()
	{
		var baseDatos = new BaseDatosLocal(_ruta);
		_abiertas.Add(baseDatos);
		await baseDatos.InicializarAsync();

		Assert.Equal(11, await baseDatos.ObtenerVersionEsquemaAsync());
		Assert.Equal(13, BaseDeVersionAnterior.Escalar<int>(_ruta,
			"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"));

		// Nacer no deja constancia: no hubo nada que migrar.
		Assert.Equal(0, BaseDeVersionAnterior.Escalar<int>(_ruta, "SELECT COUNT(*) FROM evento_auditoria"));
	}

	/// <summary>Una fila, columna por columna y con su tipo real, sin pasar por ninguna entidad.</summary>
	private Dictionary<string, object?> Fila(string sql)
	{
		using var conexion = new SQLiteConnection(new SQLiteConnectionString(_ruta, storeDateTimeAsTicks: true));
		var sentencia = SQLite3.Prepare2(conexion.Handle, sql);
		try
		{
			Assert.Equal(SQLite3.Result.Row, SQLite3.Step(sentencia));

			var fila = new Dictionary<string, object?>();
			for (var i = 0; i < SQLite3.ColumnCount(sentencia); i++)
			{
				fila[SQLite3.ColumnName(sentencia, i)] = SQLite3.ColumnType(sentencia, i) switch
				{
					SQLite3.ColType.Null => null,
					SQLite3.ColType.Integer => SQLite3.ColumnInt64(sentencia, i),
					SQLite3.ColType.Float => SQLite3.ColumnDouble(sentencia, i),
					_ => SQLite3.ColumnString(sentencia, i),
				};
			}

			return fila;
		}
		finally
		{
			SQLite3.Finalize(sentencia);
		}
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var abierta in _abiertas)
		{
			await abierta.DisposeAsync();
		}

		try
		{
			File.Delete(_ruta);
			File.Delete(_ruta + ".v10.bak");
		}
		catch (IOException)
		{
		}
	}

	private sealed class ClaveFija : IDatabaseKeyProvider
	{
		public const string Clave = "clave-de-prueba-32-bytes-abcdefgh";

		public Task<string> ObtenerAsync(CancellationToken cancelacion = default) => Task.FromResult(Clave);
	}

	public sealed class Violacion { public string table { get; set; } = ""; }
	public sealed class Nombre { public string name { get; set; } = ""; }
	public sealed class Operador { public string cuenta { get; set; } = ""; public string rol { get; set; } = ""; }
	public sealed class Unidad { public string clave { get; set; } = ""; public string id { get; set; } = ""; public string descripcion { get; set; } = ""; }
	public sealed class Sesion
	{
		public string session_id { get; set; } = "";
		public string operador { get; set; } = "";
		public string? unidad_clave { get; set; }
		public string rol { get; set; } = "";
		public int vigente { get; set; }
	}
	public sealed class Intento
	{
		public int id { get; set; }
		public string registro { get; set; } = "";
		public int? codigo { get; set; }
		public string? codigo_texto { get; set; }
		public string? mensaje { get; set; }
	}
}
