namespace AppOperador.Aplicacion.Interfaces;

// Al visor del sistema: reproducir video en la app exigiría un paquete que engorda el APK.
public interface IVisorEvidencia
{
	// nombreOriginal: en disco el archivo se llama por su UUID.
	Task<ResultadoApertura> AbrirAsync(
		string ruta,
		string nombreOriginal,
		string tipoMime,
		CancellationToken cancelacion = default);
}

// Cada resultado se resuelve distinto, así que se distinguen.
public enum ResultadoApertura
{
	Abierto = 0,

	ArchivoNoEncontrado = 1,

	SinAplicacion = 2,

	NoSePudo = 3,
}
