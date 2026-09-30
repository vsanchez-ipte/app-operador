namespace AppOperador.Aplicacion.Modelos;

// Desechable para que el temporal sin cifrar se borre en cualquier salida.
public sealed class ExportacionBaseDatos : IAsyncDisposable
{
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

	public string RutaTemporal { get; }

	// Sin operador ni unidad: el nombre del archivo se ve en carpetas compartidas.
	public string NombreSugerido { get; }

	public int VersionEsquema { get; }

	public IReadOnlyList<string> Tablas { get; }

	public long Bytes { get; }

	// No propaga fallos: un temporal huérfano es menor que tumbar la pantalla.
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
