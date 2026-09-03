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
/// <param name="Desenlace">Qué ocurrió. Ver <see cref="DesenlaceSeleccion"/>.</param>
public sealed record SeleccionEvidencia(ArchivoElegido? Archivo, DesenlaceSeleccion Desenlace)
{
	/// <summary>El operador cerró el diálogo sin elegir. Respuesta válida: no se avisa.</summary>
	public static readonly SeleccionEvidencia Cancelada =
		new(null, DesenlaceSeleccion.Cancelado);

	/// <summary>Negó el permiso, pero el sistema volverá a preguntar. Tampoco se avisa.</summary>
	public static readonly SeleccionEvidencia PermisoNegado =
		new(null, DesenlaceSeleccion.PermisoNegado);

	/// <summary>El sistema ya no volverá a preguntar. Hay que decirlo y ofrecer la salida.</summary>
	public static readonly SeleccionEvidencia PermisoBloqueado =
		new(null, DesenlaceSeleccion.PermisoBloqueado);

	/// <summary>El dispositivo no pudo abrir la cámara o el selector.</summary>
	public static readonly SeleccionEvidencia NoDisponible =
		new(null, DesenlaceSeleccion.NoSePudoAbrir);

	public static SeleccionEvidencia Elegido(ArchivoElegido archivo) =>
		new(archivo, DesenlaceSeleccion.Elegido);

	/// <summary>Indica si el sistema no pudo ofrecer la función.</summary>
	public bool NoSePudoAbrir => Desenlace == DesenlaceSeleccion.NoSePudoAbrir;
}

/// <summary>
/// Qué contestó el sistema al pedirle un archivo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Era un booleano y no alcanzaba.</b> Con «canceló» y «negó el permiso» valiendo lo mismo, el
/// tercer toque en «Capturar foto» no hacía nada: Android deja de mostrar el diálogo tras la
/// segunda negativa, la llamada falla al instante por falta de permiso, y eso llegaba como
/// «canceló», que se atiende en silencio a propósito. El operador se quedaba pulsando un botón
/// muerto sin ninguna forma de salir, porque desde la aplicación ya no se puede volver a pedir.
/// </para>
/// <para>
/// Es la tercera vez que el mismo tipo esconde un defecto por no distinguir lo que hay que
/// distinguir: primero fue un <see langword="null"/> para todo, después «no se pudo abrir», y
/// ahora el permiso.
/// </para>
/// </remarks>
public enum DesenlaceSeleccion
{
	/// <summary>Hay archivo.</summary>
	Elegido = 1,

	/// <summary>El operador cerró el diálogo sin elegir nada.</summary>
	Cancelado = 2,

	/// <summary>
	/// Negó el permiso, y el sistema volverá a preguntar la próxima vez.
	/// </summary>
	/// <remarks>
	/// Se atiende igual que cancelar, como pide el CA 4 de JTT-1398: decir que no es una
	/// respuesta válida y la incidencia se guarda sin evidencia. Volver a tocar el botón vuelve a
	/// mostrar el diálogo, así que el operador no está atrapado.
	/// </remarks>
	PermisoNegado = 3,

	/// <summary>
	/// El sistema ya no va a preguntar más: el permiso solo se concede desde la configuración.
	/// </summary>
	/// <remarks>
	/// <b>Aquí el silencio deja de ser correcto.</b> No es que el operador haya decidido no
	/// adjuntar: es que ya no puede aunque quiera, y nada en la pantalla se lo dice.
	/// </remarks>
	PermisoBloqueado = 4,

	/// <summary>El sistema no pudo ofrecer la función. No es una respuesta del operador.</summary>
	NoSePudoAbrir = 5,
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
/// <b>Negarse no es un error.</b> El CA 4 dice que la negativa no impide guardar la incidencia sin
/// evidencia, así que la primera negativa se atiende en silencio, igual que cancelar.
/// </para>
/// <para>
/// <b>Pero dejar de poder no es lo mismo que decir que no.</b> Android deja de mostrar el diálogo
/// tras la segunda negativa, y a partir de ahí el permiso solo se concede desde la configuración
/// del sistema. Ese caso llega como <see cref="DesenlaceSeleccion.PermisoBloqueado"/> y <b>sí hay
/// que decirlo</b>, junto con la salida: sin eso el operador pulsa un botón que nunca responde.
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

	/// <summary>
	/// Lleva al operador a la pantalla del sistema donde puede conceder el permiso.
	/// </summary>
	/// <remarks>
	/// <b>Es la única salida cuando el permiso quedó bloqueado</b>: desde la aplicación ya no se
	/// puede volver a pedir. Vive aquí, junto a quien pide el permiso, y no en un servicio aparte:
	/// quien sabe que hace falta es el mismo que sabe cómo conseguirlo.
	/// </remarks>
	Task AbrirConfiguracionAsync();
}
