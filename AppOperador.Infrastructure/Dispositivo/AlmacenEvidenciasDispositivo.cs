using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Guarda las evidencias en el espacio privado de la app (JTT-1398 CA 5 y 9).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dónde.</b> En una subcarpeta del directorio de datos de la aplicación, que en Android e
/// iOS solo la app puede leer. No en el directorio de caché: el sistema lo vacía cuando le hace
/// falta espacio, y ahí se perdería una evidencia pendiente de enviar sin que nadie se entere.
/// </para>
/// <para>
/// <b>El nombre lo pone el UUID de la evidencia, no el operador.</b> Dos fotos llamadas
/// <c>IMG_0001.jpg</c> son lo normal en un teléfono, y con el nombre original una pisaría a la
/// otra. El nombre que el operador reconoce se guarda en la fila, no en el disco.
/// </para>
/// <para>
/// <b>Se copia, no se mueve.</b> El CA 9 dice que no se modifica el archivo original fuera del
/// espacio privado: la foto del operador sigue en su galería tal como estaba.
/// </para>
/// </remarks>
public sealed class AlmacenEvidenciasDispositivo : IAlmacenEvidencias
{
	/// <summary>Subcarpeta donde viven las evidencias, dentro del espacio privado.</summary>
	internal const string NombreCarpeta = "evidencias";

	private readonly string _carpeta;

	public AlmacenEvidenciasDispositivo(string directorioDatos)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directorioDatos);
		_carpeta = Path.Combine(directorioDatos, NombreCarpeta);
	}

	/// <inheritdoc />
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
			// El archivo desapareció, se revocó el acceso o no cabe. Se limpia lo que haya
			// quedado a medias: un archivo truncado es peor que ninguno, porque la cola lo
			// subiría creyendo que está completo.
			await IntentarBorrarAsync(destino).ConfigureAwait(false);
			return null;
		}
	}

	/// <inheritdoc />
	public Task EliminarAsync(string rutaArchivo, CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();
		return IntentarBorrarAsync(rutaArchivo);
	}

	/// <summary>
	/// Conserva la extensión del original.
	/// </summary>
	/// <remarks>
	/// No decide el tipo —eso lo hace el servidor, por contenido— pero sin extensión el archivo
	/// copiado es opaco para cualquiera que abra el espacio privado a revisar un defecto.
	/// </remarks>
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
			// Que no se pueda borrar no puede tumbar la operación: la fila ya se quitó y lo
			// que queda es un archivo suelto, no una inconsistencia.
		}

		return Task.CompletedTask;
	}
}
