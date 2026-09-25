using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Entrega la evidencia al visor del sistema, con las API de MAUI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué se copia a la caché en vez de abrir el archivo donde está.</b> Las evidencias viven
/// en el directorio de datos de la app —a propósito: la caché la vacía el sistema cuando necesita
/// espacio, y ahí se perdería una evidencia pendiente de enviar—. Pero el proveedor de archivos
/// que MAUI declara en el manifiesto solo expone <c>cache-path</c> y las rutas externas, <b>no
/// el directorio de datos</b>: abrirlo directamente fallaría al no encontrar una raíz que lo
/// contenga. La copia es lo que lo vuelve alcanzable sin sacar el original de su sitio.
/// </para>
/// <para>
/// <b>Y de paso arregla el nombre.</b> En disco el archivo se llama por el UUID de la evidencia,
/// porque dos fotos <c>IMG_0001.jpg</c> son lo normal en un teléfono y una pisaría a la otra. El
/// operador vería esa cadena como título en el visor; la copia lleva el nombre que él reconoce.
/// </para>
/// <para>
/// <b>La copia es desechable.</b> Se rehace en cada apertura y se limpia la anterior: es la caché
/// haciendo de caché. Que el sistema la borre no pierde nada, porque el original sigue donde
/// estaba.
/// </para>
/// </remarks>
public sealed class VisorEvidenciaDispositivo : IVisorEvidencia
{
	/// <summary>Subcarpeta de la caché donde se dejan las copias para ver.</summary>
	internal const string NombreCarpeta = "vista-evidencia";

	private readonly string _carpeta;

	public VisorEvidenciaDispositivo(string directorioCache)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directorioCache);
		_carpeta = Path.Combine(directorioCache, NombreCarpeta);
	}

	/// <inheritdoc />
	public async Task<ResultadoApertura> AbrirAsync(
		string ruta,
		string nombreOriginal,
		string tipoMime,
		CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		if (string.IsNullOrWhiteSpace(ruta) || !File.Exists(ruta))
		{
			// La fila dice que hay un archivo y no lo hay. Es lo que pasa si alguien limpia el
			// almacenamiento de la app desde la configuración del sistema.
			return ResultadoApertura.ArchivoNoEncontrado;
		}

		string copia;

		try
		{
			// Se limpia antes y no después: si la app muere con el visor abierto, el archivo
			// sigue haciendo falta. Lo de la vez pasada ya no.
			LimpiarCopiasAnteriores();

			Directory.CreateDirectory(_carpeta);
			copia = Path.Combine(_carpeta, NombreSeguro(nombreOriginal, ruta));

			File.Copy(ruta, copia, overwrite: true);
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			// Sin espacio, sin permiso, o el original desapareció entre la comprobación y la
			// copia. No hay nada que el operador pueda corregir tocando otra vez.
			return ResultadoApertura.NoSePudo;
		}

		try
		{
			var peticion = new OpenFileRequest(
				string.IsNullOrWhiteSpace(nombreOriginal) ? "Evidencia" : nombreOriginal,
				new ReadOnlyFile(copia, tipoMime));

			// Falso aquí es que ninguna aplicación del dispositivo se ofreció a abrirlo: un
			// teléfono sin lector de PDF es lo normal, y decirle «no se pudo» mandaría a
			// reintentar algo que nunca va a funcionar.
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
			// El proveedor de archivos no encontró una raíz que contenga la copia. Si esto
			// aparece, es que la caché dejó de estar declarada en el manifiesto combinado.
			return ResultadoApertura.NoSePudo;
		}
	}

	/// <summary>
	/// Un nombre que el sistema de archivos acepte, conservando el que el operador reconoce.
	/// </summary>
	/// <remarks>
	/// El nombre viene de otro dispositivo o de una galería, así que puede traer cualquier cosa.
	/// Si no queda nada utilizable se usa el del archivo guardado, que es un UUID: feo, pero
	/// abre. <b>La extensión se conserva del archivo real</b> y no del nombre declarado, porque
	/// es la que hace que el visor lo reconozca.
	/// </remarks>
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

	/// <summary>Quita las copias de aperturas anteriores.</summary>
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
			// Un archivo de caché que no se pudo borrar lo recoge el sistema. No vale dejar al
			// operador sin ver su evidencia por esto.
		}
	}
}
