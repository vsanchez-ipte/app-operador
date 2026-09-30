using System.Text;

namespace AppOperador.Infrastructure.Sqlite.Esquema;

internal sealed record Columna(
	string Nombre,
	string Tipo,
	bool Nulable = true,
	// También rellena lo que una versión anterior no traía o traía en NULL.
	string? PorOmision = null,
	string? Referencia = null,
	string? NombreAnterior = null)
{
	public string NombreOrigen => NombreAnterior ?? Nombre;

	public static Columna Texto(string nombre, bool nulable = true, string? nombreAnterior = null) =>
		new(nombre, "TEXT", nulable, nulable ? null : "''", NombreAnterior: nombreAnterior);

	public static Columna Entero(string nombre, bool nulable = false, string? nombreAnterior = null) =>
		new(nombre, "INTEGER", nulable, nulable ? null : "0", NombreAnterior: nombreAnterior);

	public static Columna Real(string nombre) => new(nombre, "REAL");

	public static Columna Llave(string nombre, string referencia, bool nulable, string? nombreAnterior = null) =>
		new(nombre, "TEXT", nulable, null, referencia, nombreAnterior);
}

// El DDL vive aquí porque sqlite-net no declara llaves foráneas ni valores por omisión.
internal sealed record DefinicionTabla(
	string Nombre,
	string ClavePrimaria,
	IReadOnlyList<Columna> Columnas,
	IReadOnlyList<string> Indices,
	bool Autoincremento = false,
	string? NombreAnterior = null)
{
	public string NombreOrigen => NombreAnterior ?? Nombre;

	// Nombre parametrizado: la reconstrucción crea con nombre provisional y renombra al final.
	public string SentenciaCrear(string? comoNombre = null)
	{
		var sql = new StringBuilder();
		sql.Append("CREATE TABLE \"").Append(comoNombre ?? Nombre).Append("\" (");

		for (var i = 0; i < Columnas.Count; i++)
		{
			var columna = Columnas[i];
			sql.Append(i == 0 ? "\n\t" : ",\n\t");
			sql.Append('"').Append(columna.Nombre).Append("\" ").Append(columna.Tipo);

			if (columna.Nombre == ClavePrimaria)
			{
				sql.Append(" NOT NULL PRIMARY KEY");
				if (Autoincremento)
				{
					sql.Append(" AUTOINCREMENT");
				}

				continue;
			}

			if (!columna.Nulable)
			{
				sql.Append(" NOT NULL");
			}

			// Una llave no lleva valor por omisión: no hay padre «por omisión» al que apuntar.
			if (columna.PorOmision is not null)
			{
				sql.Append(" DEFAULT ").Append(columna.PorOmision);
			}

			if (columna.Referencia is not null)
			{
				sql.Append(" REFERENCES ").Append(columna.Referencia).Append(" ON DELETE RESTRICT");
			}
		}

		sql.Append("\n);");
		return sql.ToString();
	}

	// Copia solo las columnas que existan en ambas: una migración sirve desde cualquier versión.
	public string? SentenciaCopiar(
		string origen,
		string destino,
		IReadOnlySet<string> columnasDelOrigen,
		string? condicion = null)
	{
		var destinos = new List<string>();
		var fuentes = new List<string>();

		foreach (var columna in Columnas)
		{
			var enOrigen = columnasDelOrigen.Contains(columna.NombreOrigen) ? columna.NombreOrigen
				: columnasDelOrigen.Contains(columna.Nombre) ? columna.Nombre
				: null;

			if (enOrigen is null)
			{
				continue;
			}

			destinos.Add($"\"{columna.Nombre}\"");
			fuentes.Add(columna.PorOmision is null
				? $"\"{enOrigen}\""
				: $"COALESCE(\"{enOrigen}\", {columna.PorOmision})");
		}

		if (destinos.Count == 0)
		{
			return null;
		}

		var donde = condicion is null ? string.Empty : $" WHERE {condicion}";

		return $"INSERT INTO \"{destino}\" ({string.Join(", ", destinos)}) " +
			$"SELECT {string.Join(", ", fuentes)} FROM \"{origen}\"{donde};";
	}
}
