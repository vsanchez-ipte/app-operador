using System.Reflection;
using AppOperador.Infrastructure.Sqlite;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class EsquemaLocalTests
{
	[Fact]
	public async Task Cada_entidad_mapea_columnas_que_existen_en_su_tabla()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		using var conexion = new SQLiteConnection(new SQLiteConnectionString(contexto.Ruta, storeDateTimeAsTicks: true));
		var faltantes = new List<string>();

		foreach (var entidad in Entidades())
		{
			var mapeo = conexion.GetMapping(entidad);
			var columnas = conexion.GetTableInfo(mapeo.TableName).Select(c => c.Name).ToHashSet();

			if (columnas.Count == 0)
			{
				faltantes.Add($"{entidad.Name}: la tabla {mapeo.TableName} no existe");
				continue;
			}

			foreach (var columna in mapeo.Columns.Where(c => !columnas.Contains(c.Name)))
			{
				faltantes.Add($"{mapeo.TableName}.{columna.Name} (de {entidad.Name})");
			}
		}

		Assert.Empty(faltantes);
	}

	[Fact]
	public async Task Cada_tabla_del_archivo_tiene_entidad()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		using var conexion = new SQLiteConnection(new SQLiteConnectionString(contexto.Ruta, storeDateTimeAsTicks: true));
		var tablas = conexion.Query<Nombre>(
				"SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'")
			.Select(t => t.name)
			.Order()
			.ToList();

		var mapeadas = Entidades()
			.Select(e => conexion.GetMapping(e).TableName)
			.Order()
			.ToList();

		Assert.Equal(mapeadas, tablas);
	}

	[Fact]
	public async Task Las_llaves_foraneas_estan_declaradas_y_encendidas()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		using var conexion = new SQLiteConnection(new SQLiteConnectionString(contexto.Ruta, storeDateTimeAsTicks: true));

		var llaves = new Dictionary<string, List<string>>();
		foreach (var tabla in new[] { "sesion_local", "incidencia_local", "evidencia_local", "intento_incidencia", "intento_evidencia", "evento_auditoria" })
		{
			llaves[tabla] = conexion.Query<LlaveForanea>($"PRAGMA foreign_key_list(\"{tabla}\");")
				.Select(l => $"{l.from}→{l.table}")
				.Order()
				.ToList();
		}

		Assert.Equal(["operador→operador_local", "unidad_clave→unidad_local"], llaves["sesion_local"]);
		Assert.Equal(["operador→operador_local", "sesion_origen→sesion_local", "unidad_vehicular→unidad_local"], llaves["incidencia_local"]);
		Assert.Equal(["incidencia_uuid→incidencia_local"], llaves["evidencia_local"]);
		Assert.Equal(["incidencia_uuid→incidencia_local"], llaves["intento_incidencia"]);
		Assert.Equal(["evidencia_uuid→evidencia_local"], llaves["intento_evidencia"]);
		Assert.Equal(["sesion_id→sesion_local"], llaves["evento_auditoria"]);

		// Y todas prohíben borrar al padre mientras tenga hijos.
		var acciones = conexion.Query<LlaveForanea>("PRAGMA foreign_key_list(\"evidencia_local\");");
		Assert.All(acciones, l => Assert.Equal("RESTRICT", l.on_delete));
	}

	private static IEnumerable<Type> Entidades() =>
		typeof(BaseDatosLocal).Assembly.GetTypes()
			.Where(t => t.Namespace == "AppOperador.Infrastructure.Sqlite.Entidades"
				&& t.GetCustomAttribute<TableAttribute>() is not null
				&& !t.IsNested);

	public sealed class Nombre { public string name { get; set; } = ""; }

	public sealed class LlaveForanea
	{
		public string table { get; set; } = "";
		public string from { get; set; } = "";
		public string on_delete { get; set; } = "";
	}
}
