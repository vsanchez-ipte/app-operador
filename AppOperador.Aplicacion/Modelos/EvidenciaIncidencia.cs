using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Archivo que el operador acaba de elegir, todavía fuera del espacio privado de la app.
/// </summary>
/// <remarks>
/// <para>
/// <b>Es lo que devuelve el selector, no lo que se guarda.</b> Vive donde el sistema lo dejó
/// —la galería, la carpeta de la cámara, una descarga— y ese sitio no es de la app: el archivo
/// puede desaparecer, cambiar o dejar de ser accesible en cuanto el operador toque otra cosa.
/// Por eso se copia antes de registrarlo (JTT-1398 CA 5 y 9).
/// </para>
/// <para>
/// <b>El contenido se lee en el momento de copiar y no antes.</b> Un video de quince megabytes
/// cargado en memoria para acabar rechazado por formato es memoria gastada en un teléfono de
/// campo; por eso viaja como una función que abre el flujo, no como un arreglo de bytes.
/// </para>
/// </remarks>
/// <param name="NombreOriginal">Como lo nombró el dispositivo. Es lo que el operador reconoce.</param>
/// <param name="TipoMime">Lo que el dispositivo declara. <b>El servidor lo determina otra vez por contenido.</b></param>
/// <param name="Bytes">Tamaño, para validar antes de copiar.</param>
/// <param name="AbrirContenido">Abre el flujo de lectura. Se invoca una sola vez, al copiar.</param>
public sealed record ArchivoElegido(
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	Func<CancellationToken, Task<Stream>> AbrirContenido);

/// <summary>
/// Evidencia ya guardada en el espacio privado y registrada en la cola local (JTT-1398).
/// </summary>
/// <param name="Uuid">Identidad propia. Es también la clave de idempotencia frente al servidor.</param>
/// <param name="IncidenciaUuid">La incidencia a la que documenta.</param>
/// <param name="NombreOriginal">Lo que se le muestra al operador en la lista.</param>
/// <param name="TipoMime">Tipo declarado al adjuntar.</param>
/// <param name="Bytes">Tamaño del archivo copiado.</param>
/// <param name="RutaArchivo">Dónde quedó dentro del espacio privado. Nunca sale de la app.</param>
/// <param name="Estado">Dónde va en su propio camino de sincronización.</param>
public sealed record EvidenciaAdjunta(
	string Uuid,
	string IncidenciaUuid,
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	string RutaArchivo,
	EstadoSincronizacion Estado);

/// <summary>
/// Lo que ocurrió al intentar adjuntar un archivo (JTT-1398).
/// </summary>
/// <remarks>
/// Un tipo y no una excepción, por lo mismo que en <c>ResultadoEnvio</c>: que un archivo no
/// entre no es excepcional —pasa a diario con un formato o un cupo lleno— y es la mitad del
/// trabajo de adjuntar.
/// </remarks>
public sealed record ResultadoAdjuntar
{
	private ResultadoAdjuntar(EvidenciaAdjunta? adjuntada, MotivoEvidenciaRechazada motivo)
	{
		Adjuntada = adjuntada;
		Motivo = motivo;
	}

	/// <summary>La evidencia registrada, o <see langword="null"/> si no se pudo.</summary>
	public EvidenciaAdjunta? Adjuntada { get; }

	/// <summary>Por qué no se pudo. <see cref="MotivoEvidenciaRechazada.Ninguno"/> si sí se pudo.</summary>
	public MotivoEvidenciaRechazada Motivo { get; }

	public bool Exito => Adjuntada is not null;

	public static ResultadoAdjuntar Aceptada(EvidenciaAdjunta evidencia) =>
		new(evidencia, MotivoEvidenciaRechazada.Ninguno);

	public static ResultadoAdjuntar Rechazada(MotivoEvidenciaRechazada motivo) =>
		new(null, motivo);
}
