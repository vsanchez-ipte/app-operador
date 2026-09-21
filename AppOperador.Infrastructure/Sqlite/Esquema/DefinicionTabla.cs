using System.Text;

namespace AppOperador.Infrastructure.Sqlite.Esquema;

/// <summary>
/// Una columna del esquema local, con lo necesario para crearla y para copiarla desde una
/// versión anterior.
/// </summary>
/// <param name="Nombre">Nombre en el esquema vigente.</param>
/// <param name="Tipo">Afinidad de SQLite: <c>TEXT</c>, <c>INTEGER</c> o <c>REAL</c>.</param>
/// <param name="Nulable">Si admite <c>NULL</c>. Una columna no nulable lleva <see cref="PorOmision"/>, salvo que sea llave.</param>
/// <param name="PorOmision">
/// Valor por omisión, ya escrito como literal SQL. Es también lo que se pone en las filas de
/// una versión anterior que no traían la columna o la traían en <c>NULL</c>.
/// </param>
/// <param name="Referencia">Tabla y columna a la que apunta, como <c>operador_local(cuenta)</c>, si es llave foránea.</param>
/// <param name="NombreAnterior">
/// Cómo se llamaba en el esquema anterior, si cambió. La migración lee de ahí y escribe aquí.
/// </param>
internal sealed record Columna(
	string Nombre,
	string Tipo,
	bool Nulable = true,
	string? PorOmision = null,
	string? Referencia = null,
	string? NombreAnterior = null)
{
	/// <summary>Nombre con el que se busca la columna en una tabla de la versión anterior.</summary>
	public string NombreOrigen => NombreAnterior ?? Nombre;

	public static Columna Texto(string nombre, bool nulable = true, string? nombreAnterior = null) =>
		new(nombre, "TEXT", nulable, nulable ? null : "''", NombreAnterior: nombreAnterior);

	public static Columna Entero(string nombre, bool nulable = false, string? nombreAnterior = null) =>
		new(nombre, "INTEGER", nulable, nulable ? null : "0", NombreAnterior: nombreAnterior);

	public static Columna Real(string nombre) => new(nombre, "REAL");

	/// <summary>Llave foránea de texto. <c>ON DELETE RESTRICT</c> siempre: borrar un padre no se lleva historial.</summary>
	public static Columna Llave(string nombre, string referencia, bool nulable, string? nombreAnterior = null) =>
		new(nombre, "TEXT", nulable, null, referencia, nombreAnterior);
}

/// <summary>
/// Una tabla del esquema local: cómo se crea y cómo se rellena desde la versión anterior.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aquí está el DDL y no en los atributos de sqlite-net</b>, que no sabe declarar llaves
/// foráneas ni valores por omisión. Las entidades de <c>Entidades/</c> solo mapean columnas; la
/// prueba <c>LasEntidadesMapeanColumnasQueExisten</c> vigila que las dos descripciones coincidan.
/// </para>
/// <para>
/// Los índices van con su nombre para que sobrevivan a la reconstrucción: SQLite los tira con
/// la tabla vieja y aquí se vuelven a crear sobre la nueva.
/// </para>
/// </remarks>
/// <param name="Nombre">Nombre de la tabla.</param>
/// <param name="ClavePrimaria">Columna que es llave primaria. Con <paramref name="Autoincremento"/> es un entero que asigna SQLite.</param>
/// <param name="Columnas">Todas las columnas, la llave incluida, en el orden en que se crean.</param>
/// <param name="Indices">Sentencias <c>CREATE INDEX</c> completas.</param>
/// <param name="Autoincremento">Si la llave primaria es <c>INTEGER PRIMARY KEY AUTOINCREMENT</c>.</param>
/// <param name="NombreAnterior">Cómo se llamaba la tabla en el esquema anterior, si cambió.</param>
internal sealed record DefinicionTabla(
	string Nombre,
	string ClavePrimaria,
	IReadOnlyList<Columna> Columnas,
	IReadOnlyList<string> Indices,
	bool Autoincremento = false,
	string? NombreAnterior = null)
{
	/// <summary>Nombre con el que se busca la tabla en una base de la versión anterior.</summary>
	public string NombreOrigen => NombreAnterior ?? Nombre;

	/// <summary>
	/// Sentencia <c>CREATE TABLE</c>, con el nombre que se indique.
	/// </summary>
	/// <remarks>
	/// El nombre se parametriza porque la reconstrucción crea primero la tabla con un nombre
	/// provisional y la renombra al final. Las referencias de las llaves foráneas apuntan
	/// siempre al nombre definitivo del padre, que es el que queda cuando todo termina.
	/// </remarks>
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

			// Una llave no nulable no lleva valor por omisión: no hay padre «por omisión» al que
			// apuntar, y una fila que no sepa a quién apunta debe fallar, no inventarlo.
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

	/// <summary>
	/// Sentencia que copia a <paramref name="destino"/> lo que <paramref name="origen"/> tenga de
	/// esta tabla, limitándose a las columnas que existan en las dos.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Es lo que hace que una sola migración sirva desde cualquier versión anterior: una
	/// columna que la base vieja no traía queda con su valor por omisión, y una que traía en
	/// <c>NULL</c> pero ahora no lo admite se rellena con ese mismo valor.
	/// </para>
	/// <para>
	/// Cada columna se busca en el origen primero por su nombre anterior y luego por el actual,
	/// así la misma sentencia sirve para leer de una tabla de la versión vieja y de una que ya
	/// tiene la forma nueva.
	/// </para>
	/// </remarks>
	/// <param name="condicion">Cláusula <c>WHERE</c> sin la palabra, para copiar solo parte de las filas.</param>
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
