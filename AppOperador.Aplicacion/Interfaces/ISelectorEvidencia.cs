using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>De dónde sale el archivo que el operador quiere adjuntar (JTT-1398 CA 2).</summary>
public enum OrigenEvidencia
{
	/// <summary>Tomarla en el momento con la cámara.</summary>
	Camara = 1,

	/// <summary>Elegir una fotografía que ya existe en el dispositivo.</summary>
	Galeria = 2,

	/// <summary>Grabar un video corto en el momento.</summary>
	/// <remarks>
	/// Que el dispositivo pueda grabarlo no significa que el servidor lo acepte: hoy el catálogo
	/// no publica ningún <c>video/*</c>. Quien ofrezca este origen debe mirar además
	/// <c>LimitesEvidencia.AdmiteVideo</c>.
	/// </remarks>
	Video = 3,

	/// <summary>Elegir un archivo cualquiera del dispositivo, no solo del carrete.</summary>
	/// <remarks>
	/// <b>Es la otra mitad del CA 2</b>, que pide «selector de archivos o fotografías». La
	/// galería no alcanza: el servidor admite <b>PDF</b> y ningún selector de fotos lo ofrece,
	/// así que sin este origen un formato admitido no tenía por dónde entrar.
	/// </remarks>
	Archivo = 4,
}

/// <summary>
/// Lo que contestó el selector del sistema (JTT-1398 CA 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe porque no distinguir costó una ronda de pruebas.</b> El 28 de agosto los tres
/// botones de evidencia no hacían «absolutamente nada» en el emulador: faltaba el bloque
/// <c>&lt;queries&gt;</c> del manifiesto, así que Android no dejaba resolver el intent de la
/// cámara y <c>MediaPicker</c> devolvía <see langword="null"/> sin lanzar. Con un solo
/// <see langword="null"/> para todo, eso era indistinguible de cancelar, y cancelar se atiende
/// en silencio a propósito. <b>El defecto no tenía cómo manifestarse.</b>
/// </para>
/// <para>
/// <b>Cancelar y negar el permiso siguen siendo silencio</b>, que es lo que pide el CA 4. Lo que
/// deja de serlo es que el sistema no pueda ofrecer la función: eso no es decisión del operador
/// y hay que decírselo, o se queda pulsando un botón que nunca responde.
/// </para>
/// </remarks>
/// <param name="Archivo">Lo elegido, o <see langword="null"/> si no hubo nada.</param>
/// <param name="NoSePudoAbrir">
/// El sistema no pudo ofrecer la función. <b>No es una respuesta del operador.</b>
/// </param>
public sealed record SeleccionEvidencia(ArchivoElegido? Archivo, bool NoSePudoAbrir)
{
	/// <summary>El operador canceló o negó el permiso. Respuesta válida: no se avisa.</summary>
	public static readonly SeleccionEvidencia Cancelada = new(null, NoSePudoAbrir: false);

	/// <summary>El dispositivo no pudo abrir la cámara o el selector.</summary>
	public static readonly SeleccionEvidencia NoDisponible = new(null, NoSePudoAbrir: true);

	public static SeleccionEvidencia Elegido(ArchivoElegido archivo) =>
		new(archivo, NoSePudoAbrir: false);
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
/// sin evidencia, así que cancelar el diálogo y rechazar el permiso llegan igual, como
/// <see cref="SeleccionEvidencia.Cancelada"/>. Quien llama no tiene nada que recuperar, solo que
/// no adjuntar, y <b>no debe avisar de nada</b>.
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
	/// Pide un archivo al operador.
	/// </summary>
	/// <remarks>
	/// Ver <see cref="SeleccionEvidencia"/>: cancelar y no poder abrir el selector llegan
	/// distintos a propósito, porque solo lo segundo hay que avisarlo.
	/// </remarks>
	Task<SeleccionEvidencia> ElegirAsync(
		OrigenEvidencia origen,
		CancellationToken cancelacion = default);
}
