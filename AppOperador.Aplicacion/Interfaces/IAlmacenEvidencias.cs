using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Guarda y retira los archivos de evidencia del espacio privado de la app (JTT-1398 CA 5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Copiar no es un detalle de implementación, es el criterio.</b> El CA 5 pide que los
/// archivos vivan en el espacio privado y el CA 9 que no se modifique el original fuera de él.
/// Lo que el selector devuelve apunta a la galería o a la carpeta de la cámara: si la app se
/// quedara con esa ruta, bastaría con que el operador borrara la foto para que la evidencia
/// pendiente de enviar dejara de existir, y la cola quedaría apuntando a nada.
/// </para>
/// <para>
/// <b>Solo mueve bytes.</b> No valida, no decide y no sabe de límites: eso es
/// <c>ReglaEvidenciaAdmisible</c>, y va antes.
/// </para>
/// </remarks>
public interface IAlmacenEvidencias
{
	/// <summary>
	/// Copia el archivo al espacio privado y devuelve dónde quedó.
	/// </summary>
	/// <param name="uuidEvidencia">Identidad de la evidencia; da nombre al archivo copiado.</param>
	/// <param name="archivo">El archivo elegido por el operador.</param>
	/// <returns>La ruta dentro del espacio privado, o <see langword="null"/> si no se pudo copiar.</returns>
	/// <remarks>
	/// Devuelve <see langword="null"/> en vez de lanzar cuando el archivo no se puede leer: que
	/// el operador revoque el acceso a la galería a media copia, o que el archivo desaparezca,
	/// no es excepcional en un teléfono.
	/// </remarks>
	Task<string?> GuardarAsync(
		string uuidEvidencia,
		ArchivoElegido archivo,
		CancellationToken cancelacion = default);

	/// <summary>Borra el archivo copiado. No falla si ya no está.</summary>
	Task EliminarAsync(string rutaArchivo, CancellationToken cancelacion = default);
}
