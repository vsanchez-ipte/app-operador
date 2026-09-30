using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

// Datos privados y no caché (el sistema la vacía). Nombre por uuid: dos IMG_0001.jpg son lo normal. Se copia, no se mueve.
public sealed class AlmacenEvidenciasDispositivo : IAlmacenEvidencias
{
	internal const string NombreCarpeta = "evidencias";

	private readonly string _carpeta;

	public AlmacenEvidenciasDispositivo(string directorioDatos)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directorioDatos);
		_carpeta = Path.Combine(directorioDatos, NombreCarpeta);
	}

	public async Task<string?> GuardarAsync(
		string uuidEvidencia,
		ArchivoElegido archivo,
		CancellationToken cancelacion = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(uuidEvidencia);
		ArgumentNullException.ThrowIfNull(archivo);

		var destino = Path.Combine(_carpeta, uuidEvidencia + ExtensionDe(archivo.NombreOriginal));

		try
		{
			Directory.CreateDirectory(_carpeta);

			await using var origen = await archivo.AbrirContenido(cancelacion).ConfigureAwait(false);
			await using var escritura = File.Create(destino);
			await origen.CopyToAsync(escritura, cancelacion).ConfigureAwait(false);

			return destino;
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// Un archivo truncado se subiría como si estuviera completo.
			await IntentarBorrarAsync(destino).ConfigureAwait(false);
			return null;
		}
	}

	public Task EliminarAsync(string rutaArchivo, CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();
		return IntentarBorrarAsync(rutaArchivo);
	}

	// Solo para que el archivo no sea opaco al revisar; el tipo lo decide el servidor.
	private static string ExtensionDe(string nombreOriginal)
	{
		var extension = Path.GetExtension(nombreOriginal);

		return string.IsNullOrWhiteSpace(extension) || extension.Length > 10
			? string.Empty
			: extension;
	}

	private static Task IntentarBorrarAsync(string ruta)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(ruta) && File.Exists(ruta))
			{
				File.Delete(ruta);
			}
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException)
		{
			// Un archivo suelto no es inconsistencia: la fila ya se quitó.
		}

		return Task.CompletedTask;
	}
}
