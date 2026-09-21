namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Copia legible de la base local, viva mientras no se libere.
/// </summary>
/// <remarks>
/// <para>
/// Es desechable a propósito. El archivo temporal queda en la caché de la app, sin cifrar, y
/// tiene que desaparecer tanto si se guardó como si la persona canceló el diálogo o algo
/// falló a medias. Atarlo a <c>IAsyncDisposable</c> deja esa limpieza en un <c>await using</c>
/// del llamador en vez de repartirla por cada camino de salida, que es donde se olvida.
/// </para>
/// <para>
/// Lo que la persona elija guardar es una copia de este archivo y ya no se administra desde
/// aquí: sale del ámbito de la app y queda bajo su resguardo.
/// </para>
/// </remarks>
public sealed class ExportacionBaseDatos : IAsyncDisposable
{
	/// <param name="rutaTemporal">Archivo legible recién creado, en la caché de la app.</param>
	/// <param name="nombreSugerido">Nombre que se propone al guardar, sin datos personales.</param>
	/// <param name="versionEsquema">Versión de esquema que traía la base de origen.</param>
	/// <param name="tablas">Tablas incluidas, en orden alfabético.</param>
	/// <param name="bytes">Tamaño del archivo producido.</param>
	public ExportacionBaseDatos(
		string rutaTemporal,
		string nombreSugerido,
		int versionEsquema,
		IReadOnlyList<string> tablas,
		long bytes)
	{
		RutaTemporal = rutaTemporal;
		NombreSugerido = nombreSugerido;
		VersionEsquema = versionEsquema;
		Tablas = tablas;
		Bytes = bytes;
	}

	/// <summary>Archivo legible, mientras esta instancia siga viva.</summary>
	public string RutaTemporal { get; }

	/// <summary>
	/// Nombre propuesto al guardar.
	/// </summary>
	/// <remarks>
	/// Lleva ambiente, fecha, hora y versión de esquema porque son los tres datos que hacen
	/// falta para saber qué se está mirando semanas después. No lleva operador ni unidad: el
	/// nombre de archivo se ve en carpetas compartidas y en el título de las herramientas.
	/// </remarks>
	public string NombreSugerido { get; }

	/// <summary>Versión de esquema de la base de origen, ya comprobada en la copia.</summary>
	public int VersionEsquema { get; }

	/// <summary>Tablas incluidas en la copia.</summary>
	public IReadOnlyList<string> Tablas { get; }

	/// <summary>Tamaño del archivo producido, en bytes.</summary>
	public long Bytes { get; }

	/// <summary>Borra el archivo temporal sin cifrar.</summary>
	/// <remarks>
	/// No propaga fallos al borrar: dejar un temporal huérfano es un problema menor que tumbar
	/// la pantalla justo después de una exportación que sí salió bien. El siguiente intento
	/// barre los temporales viejos de esta misma función.
	/// </remarks>
	public ValueTask DisposeAsync()
	{
		try
		{
			if (File.Exists(RutaTemporal))
			{
				File.Delete(RutaTemporal);
			}
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}

		return ValueTask.CompletedTask;
	}
}

/// <summary>
/// La copia legible no se pudo producir, o no superó la comprobación.
/// </summary>
/// <remarks>
/// Se distingue del resto de fallos de SQLite porque la pantalla la traduce a un mensaje que
/// la persona pueda accionar. Cuando se lanza, el temporal ya se borró.
/// </remarks>
public sealed class ExportacionBaseDatosException : Exception
{
	public ExportacionBaseDatosException(string mensaje)
		: base(mensaje)
	{
	}

	public ExportacionBaseDatosException(string mensaje, Exception interna)
		: base(mensaje, interna)
	{
	}
}
