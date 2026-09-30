using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// Se copia al espacio privado: si la app guardara la ruta de la galería, borrar la foto rompería la cola.
public interface IAlmacenEvidencias
{
	// Nulo si no se pudo leer: en un teléfono eso no es excepcional.
	Task<string?> GuardarAsync(
		string uuidEvidencia,
		ArchivoElegido archivo,
		CancellationToken cancelacion = default);

	Task EliminarAsync(string rutaArchivo, CancellationToken cancelacion = default);
}
