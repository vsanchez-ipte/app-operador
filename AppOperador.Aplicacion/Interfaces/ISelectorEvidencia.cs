using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>De dónde sale el archivo que el operador quiere adjuntar (JTT-1398 CA 2).</summary>
public enum OrigenEvidencia
{
	/// <summary>Tomarla en el momento con la cámara.</summary>
	Camara = 1,

	/// <summary>Elegir una que ya existe en el dispositivo.</summary>
	Galeria = 2,
}

/// <summary>
/// Pide al sistema un archivo para adjuntar (JTT-1398 CA 2, 3 y 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>El permiso se pide aquí y solo aquí</b>, al usar la función y no al iniciar sesión. Es el
/// CA 3, y también los criterios 1, 2 y 3 de JTT-1387: pedir cámara al entrar a la app asusta y
/// no se entiende; pedirla cuando el operador toca «tomar foto» se explica sola.
/// </para>
/// <para>
/// <b>Negarse no es un error.</b> El CA 4 dice que la negativa no impide guardar la incidencia
/// sin evidencia, así que cancelar el diálogo y rechazar el permiso devuelven lo mismo:
/// <see langword="null"/>. Quien llama no tiene nada que recuperar, solo que no adjuntar.
/// </para>
/// <para>
/// <b>Devuelve el archivo donde el sistema lo dejó</b>, no en el espacio privado de la app.
/// Copiarlo es trabajo de <see cref="IAlmacenEvidencias"/>, y va después de validarlo.
/// </para>
/// </remarks>
public interface ISelectorEvidencia
{
	/// <summary>Indica si el dispositivo puede ofrecer ese origen.</summary>
	/// <remarks>
	/// Un dispositivo sin cámara existe —un emulador, una tableta de escritorio— y ofrecer un
	/// botón que no puede funcionar es peor que no ofrecerlo.
	/// </remarks>
	bool Disponible(OrigenEvidencia origen);

	/// <summary>
	/// Pide un archivo al operador. <see langword="null"/> si canceló o negó el permiso.
	/// </summary>
	Task<ArchivoElegido?> ElegirAsync(
		OrigenEvidencia origen,
		CancellationToken cancelacion = default);
}
