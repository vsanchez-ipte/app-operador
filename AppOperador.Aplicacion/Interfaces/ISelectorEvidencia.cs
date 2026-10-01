using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public enum OrigenEvidencia
{
	Camara = 1,

	Galeria = 2,

	// Poder grabar no significa que el servidor lo acepte: mirar también LimitesEvidencia.AdmiteVideo.
	Video = 3,

	// La galería no ofrece PDF, que el servidor sí admite.
	Archivo = 4,
}

// Cancelar y no poder abrir llegan distintos: solo lo segundo se avisa.
public sealed record SeleccionEvidencia(
	IReadOnlyList<ArchivoElegido> Archivos,
	DesenlaceSeleccion Desenlace,
	int Ilegibles = 0)
{
	// Respuesta válida: no se avisa.
	public static readonly SeleccionEvidencia Cancelada =
		new([], DesenlaceSeleccion.Cancelado);

	// El sistema volverá a preguntar: tampoco se avisa.
	public static readonly SeleccionEvidencia PermisoNegado =
		new([], DesenlaceSeleccion.PermisoNegado);

	// El sistema ya no preguntará: hay que decirlo y ofrecer la configuración.
	public static readonly SeleccionEvidencia PermisoBloqueado =
		new([], DesenlaceSeleccion.PermisoBloqueado);

	public static readonly SeleccionEvidencia NoDisponible =
		new([], DesenlaceSeleccion.NoSePudoAbrir);

	public static SeleccionEvidencia Elegido(ArchivoElegido archivo) =>
		new([archivo], DesenlaceSeleccion.Elegido);

	// El cupo no se aplica aquí: quien adjunta comprueba cada archivo contra el catálogo.
	public static SeleccionEvidencia Elegidos(IReadOnlyList<ArchivoElegido> archivos, int ilegibles = 0) =>
		archivos.Count == 0 ? Cancelada : new(archivos, DesenlaceSeleccion.Elegido, ilegibles);

	public ArchivoElegido? Archivo => Archivos.Count > 0 ? Archivos[0] : null;

	public bool NoSePudoAbrir => Desenlace == DesenlaceSeleccion.NoSePudoAbrir;
}

// No basta un booleano: cancelar, negar y quedar bloqueado piden respuestas distintas.
public enum DesenlaceSeleccion
{
	Elegido = 1,

	Cancelado = 2,

	// Se atiende igual que cancelar: volver a tocar el botón vuelve a mostrar el diálogo.
	PermisoNegado = 3,

	// Solo se concede desde la configuración: aquí el silencio deja de ser correcto.
	PermisoBloqueado = 4,

	NoSePudoAbrir = 5,
}

// El permiso se pide al usar la función, no al iniciar sesión. Devuelve el archivo sin copiarlo.
public interface ISelectorEvidencia
{
	// Un botón que no puede funcionar es peor que no ofrecerlo.
	bool Disponible(OrigenEvidencia origen);

	// topeBytes solo aplica al video: al grabar, pasarse del tope ya no tiene arreglo al terminar.
	Task<SeleccionEvidencia> ElegirAsync(
		OrigenEvidencia origen,
		long topeBytes = 0,
		CancellationToken cancelacion = default);

	// Única salida cuando el permiso quedó bloqueado.
	Task AbrirConfiguracionAsync();
}
