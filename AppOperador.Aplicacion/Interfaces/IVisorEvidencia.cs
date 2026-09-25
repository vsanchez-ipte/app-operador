namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Abre una evidencia ya guardada para que el operador la vea.
/// </summary>
/// <remarks>
/// <para>
/// <b>Se entrega al visor del sistema en vez de pintarla aquí.</b> El teléfono ya trae con qué
/// ver una foto y con qué reproducir un video, y reproducir video dentro de la app exigiría un
/// paquete aparte —<c>CommunityToolkit.Maui.MediaElement</c>, con ExoPlayer detrás— que engorda
/// el APK para repetir lo que el dispositivo hace igual o mejor.
/// </para>
/// <para>
/// <b>Sirve para lo que todavía no se ha enviado y para lo que ya se envió</b>, porque el archivo
/// local no se borra al sincronizar: solo desaparece si el operador quita la evidencia o elimina
/// el borrador. Lo que no alcanza es la evidencia capturada en <i>otro</i> dispositivo: esa solo
/// existe en el servidor, y el canal móvil todavía no tiene por dónde descargarla.
/// </para>
/// </remarks>
public interface IVisorEvidencia
{
	/// <summary>Abre el archivo con la aplicación que el sistema tenga para su tipo.</summary>
	/// <param name="ruta">Dónde está dentro del espacio privado.</param>
	/// <param name="nombreOriginal">
	/// El nombre que el operador reconoce. En disco el archivo se llama por su UUID, así que sin
	/// esto el visor mostraría una cadena sin sentido como título.
	/// </param>
	/// <param name="tipoMime">Con qué se decide qué aplicación lo abre.</param>
	Task<ResultadoApertura> AbrirAsync(
		string ruta,
		string nombreOriginal,
		string tipoMime,
		CancellationToken cancelacion = default);
}

/// <summary>
/// Qué pasó al intentar mostrar la evidencia.
/// </summary>
/// <remarks>
/// <b>Se distinguen porque cada una se resuelve distinto</b>, y esta pantalla ya pagó dos veces
/// el precio de no distinguir: primero con un <see langword="null"/> que valía para todo, después
/// con «no se pudo abrir» tapando un permiso bloqueado. Al operador no le sirve el mismo aviso
/// cuando el archivo ya no está que cuando lo que falta es una aplicación que lo abra.
/// </remarks>
public enum ResultadoApertura
{
	/// <summary>Se abrió.</summary>
	Abierto = 0,

	/// <summary>El archivo ya no está donde decía la fila.</summary>
	ArchivoNoEncontrado = 1,

	/// <summary>El dispositivo no tiene ninguna aplicación que abra ese tipo.</summary>
	SinAplicacion = 2,

	/// <summary>No se pudo, y no por las razones anteriores.</summary>
	NoSePudo = 3,
}
