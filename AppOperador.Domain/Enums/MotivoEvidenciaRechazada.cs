namespace AppOperador.Domain.Enums;

/// <summary>
/// Por qué no se pudo adjuntar un archivo a la incidencia (JTT-1398).
/// </summary>
/// <remarks>
/// <b>Cada motivo se corrige de forma distinta</b>, y por eso son valores separados y no un
/// «no se pudo». Al operador que llenó el cupo hay que decirle que quite uno; al que eligió un
/// formato que no entra, que elija otro archivo; al que todavía no ha conectado la app, que no
/// es culpa suya. Es la misma razón por la que <c>MotivoSinKilometro</c> distingue sus cinco
/// causas en vez de dar un aviso genérico.
/// </remarks>
public enum MotivoEvidenciaRechazada
{
	/// <summary>El archivo se puede adjuntar.</summary>
	Ninguno = 0,

	/// <summary>
	/// La app todavía no sabe qué admite el servidor.
	/// </summary>
	/// <remarks>
	/// Pasa antes de la primera descarga del catálogo, y también al actualizar desde una versión
	/// anterior sin haberse conectado. <b>No es culpa del operador y se resuelve solo</b> en
	/// cuanto haya conexión: el aviso tiene que decir eso y no «archivo inválido».
	/// </remarks>
	LimitesDesconocidos = 1,

	/// <summary>La incidencia ya tiene el máximo de archivos que el servidor admite.</summary>
	CupoLleno = 2,

	/// <summary>El servidor no admite ese tipo de archivo.</summary>
	FormatoNoAdmitido = 3,

	/// <summary>El archivo pasa del tamaño máximo por archivo.</summary>
	DemasiadoGrande = 4,

	/// <summary>El archivo no tiene contenido.</summary>
	/// <remarks>
	/// Una captura que salió mal. Subirla gastaría una plaza del cupo para no mostrar nada, y el
	/// operador creería que documentó el hecho.
	/// </remarks>
	ArchivoVacio = 5,

	/// <summary>No hay espacio libre suficiente en el dispositivo para guardarlo (JTT-289 CA 8).</summary>
	/// <remarks>
	/// Se comprueba antes de copiar, no después de fallar la copia: una copia a medias en un
	/// disco lleno deja un archivo truncado que después se subiría como si estuviera entero.
	/// Con video se comprueba incluso antes de grabar, porque su tamaño no se conoce hasta el
	/// final y el operador habría grabado para nada.
	/// </remarks>
	SinEspacio = 6,
}
