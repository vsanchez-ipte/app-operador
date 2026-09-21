using System.Reflection;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Crea en disco una base tal como la dejaba una versión anterior de la app, a partir de las
/// fixtures SQL embebidas en <c>Fixtures/</c>.
/// </summary>
/// <remarks>
/// Se ejecuta con una conexión directa y en claro: lo que se prueba es la migración, y la
/// prueba de cifrado tiene su propia fixture. Cada sentencia va por separado porque sqlite-net
/// ejecuta una a la vez.
/// </remarks>
public static class BaseDeVersionAnterior
{
	/// <summary>Escribe la base de la versión indicada en <paramref name="ruta"/>.</summary>
	public static void Crear(int version, string ruta)
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true));

		foreach (var sentencia in Sentencias(version))
		{
			conexion.Execute(sentencia);
		}
	}

	/// <summary>Consulta directa sobre el archivo, sin pasar por la app.</summary>
	public static T Escalar<T>(string ruta, string sql, string? clave = null)
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true, key: clave));

		return conexion.ExecuteScalar<T>(sql);
	}

	/// <summary>Filas de una consulta directa, mapeadas por nombre de columna.</summary>
	public static List<T> Consultar<T>(string ruta, string sql, string? clave = null) where T : new()
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true, key: clave));

		return conexion.Query<T>(sql);
	}

	private static IEnumerable<string> Sentencias(int version)
	{
		var ensamblado = Assembly.GetExecutingAssembly();
		var recurso = ensamblado.GetManifestResourceNames()
			.Single(n => n.EndsWith($"esquema-{version}.sql", StringComparison.Ordinal));

		using var flujo = ensamblado.GetManifestResourceStream(recurso)!;
		using var lector = new StreamReader(flujo);
		var texto = lector.ReadToEnd();

		// Los comentarios van fuera antes de partir por punto y coma: un comentario puede
		// llevar uno dentro.
		var sinComentarios = string.Join(
			"\n",
			texto.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));

		return sinComentarios
			.Split(';')
			.Select(s => s.Trim())
			.Where(s => s.Length > 0)
			.Select(s => s + ";");
	}
}
