using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Dispositivo;

// Copia a la caché: el proveedor de archivos no expone el directorio de datos, y la copia lleva el nombre original.
public sealed class VisorEvidenciaDispositivo : IVisorEvidencia
{
	internal const string NombreCarpeta = "vista-evidencia";

	private readonly string _carpeta;

	public VisorEvidenciaDispositivo(string directorioCache)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directorioCache);
		_carpeta = Path.Combine(directorioCache, NombreCarpeta);
	}

	public async Task<ResultadoApertura> AbrirAsync(
		string ruta,
		string nombreOriginal,
		string tipoMime,
		CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
		{
			// La fila dice que hay archivo y no lo hay: alguien limpió el almacenamiento de la app.
			return ResultadoApertura.ArchivoNoEncontrado;
		}

		string copia;

		try
		{
			// Antes y no después: si la app muere con el visor abierto, la copia actual sigue haciendo falta.
			LimpiarCopiasAnteriores();

			Directory.CreateDirectory(_carpeta);
			copia = Path.Combine(_carpeta, NombreSeguro(nombreOriginal, ruta));

			File.Copy(ruta, copia, overwrite: true);
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// Sin espacio, sin permiso o el original desapareció: tocar otra vez no lo corrige.
			return ResultadoApertura.NoSePudo;
		}

		try
		{
			var peticion = new OpenFileRequest(
				string.IsNullOrWhiteSpace(nombreOriginal) ? "Evidencia" : nombreOriginal,
				new ReadOnlyFile(copia, tipoMime));

			// Falso es que ninguna app se ofreció a abrirlo: reintentar no serviría.
			return await Launcher.Default.OpenAsync(peticion).ConfigureAwait(false)
				? ResultadoApertura.Abierto
				: ResultadoApertura.SinAplicacion;
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException or FeatureNotSupportedException)
		{
			// En escritorio y en las pruebas no hay visor que lanzar.
			return ResultadoApertura.NoSePudo;
		}
		catch (Exception excepcion) when (excepcion is ArgumentException or InvalidOperationException)
		{
			// Ninguna raíz del proveedor contiene la copia: la caché dejó de estar declarada en el manifiesto.
			return ResultadoApertura.NoSePudo;
		}
	}

	// La extensión sale del archivo real, que es la que hace que el visor lo reconozca.
	private static string NombreSeguro(string nombreOriginal, string rutaReal)
	{
		var extension = Path.GetExtension(rutaReal);
		var limpio = new string((nombreOriginal ?? string.Empty)
			.Where(c => !Path.GetInvalidFileNameChars().Contains(c))
			.ToArray())
			.Trim();

		if (string.IsNullOrWhiteSpace(limpio))
		{
			return Path.GetFileName(rutaReal);
		}

		return Path.HasExtension(limpio) || string.IsNullOrEmpty(extension)
			? limpio
			: limpio + extension;
	}

	private void LimpiarCopiasAnteriores()
	{
		try
		{
			if (Directory.Exists(_carpeta))
			{
				Directory.Delete(_carpeta, recursive: true);
			}
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException)
		{
			// Un archivo de caché que no se pudo borrar lo recoge el sistema.
		}
	}
}
