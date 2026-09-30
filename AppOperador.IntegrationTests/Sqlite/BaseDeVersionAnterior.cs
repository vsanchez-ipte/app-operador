using System.Reflection;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

public static class BaseDeVersionAnterior
{
	public static void Crear(int version, string ruta)
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true));

		foreach (var sentencia in Sentencias(version))
		{
			conexion.Execute(sentencia);
		}
	}

	public static T Escalar<T>(string ruta, string sql, string? clave = null)
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true, key: clave));

		return conexion.ExecuteScalar<T>(sql);
	}

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

		// Fuera los comentarios antes de partir por punto y coma: uno puede traerlo dentro.
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
