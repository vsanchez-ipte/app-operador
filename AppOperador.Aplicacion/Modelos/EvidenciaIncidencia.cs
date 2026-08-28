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
/// <param name="UltimoErrorCodigo">
/// Último código con el que Jacob la rechazó, o <see langword="null"/> si nunca falló. Es lo que
/// decide si se vuelve a intentar: lo funcional no se reintenta (JTT-1401 CA 8).
/// </param>
public sealed record EvidenciaAdjunta(
	string Uuid,
	string IncidenciaUuid,
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	string RutaArchivo,
	EstadoSincronizacion Estado,
	string? UltimoErrorCodigo = null);

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

/// <summary>Lo que Jacob contestó al recibir una evidencia (JTT-1398 CA 11).</summary>
/// <param name="IdEvidencia">Identificador que le asignó el servidor.</param>
/// <param name="TipoMime">El que el <b>servidor</b> determinó por contenido, no el que se envió.</param>
/// <param name="HashSha256">Del contenido guardado. Sirve para comprobar que llegó completo.</param>
/// <param name="YaExistia">
/// El servidor ya tenía este archivo para esta incidencia.
/// <b>Es éxito, no error</b>: ver <see cref="ResultadoEnvioEvidencia"/>.
/// </param>
public sealed record EvidenciaRegistrada(
	string IdEvidencia,
	string TipoMime,
	string HashSha256,
	bool YaExistia);

/// <summary>
/// Resultado de subir una evidencia (JTT-1398 CA 11).
/// </summary>
/// <remarks>
/// <para>
/// Un tipo y no excepciones, igual que <c>ResultadoEnvio</c>: que el servidor rechace un archivo
/// —formato, tamaño, cupo— es parte del trabajo de subir, no algo excepcional.
/// </para>
/// <para>
/// <b>La subida es idempotente por incidencia y contenido</b>, no por nombre. Reenviar el mismo
/// archivo devuelve el mismo identificador con <c>yaExistia</c> en verdadero, sin crear otra
/// evidencia ni gastar un lugar del cupo. Eso cambia cómo se trata el reintento: <b>antes cada
/// reintento creaba otra fila, y con el tope en tres bastaban tres reintentos de una sola foto
/// para agotarle el cupo al operador</b>. Ahora reintentar es seguro.
/// </para>
/// <para>
/// Y por lo mismo, <b>con el cupo lleno el reintento sigue pasando</b>: reenviar algo ya
/// guardado responde éxito. <c>appevidencias.archivos.demasiados</c> solo aparece con un archivo
/// nuevo.
/// </para>
/// </remarks>
public sealed record ResultadoEnvioEvidencia
{
	private ResultadoEnvioEvidencia(
		EvidenciaRegistrada? registrada,
		FamiliaErrorSincronizacion? familia,
		string? codigo,
		string? mensaje)
	{
		Registrada = registrada;
		Familia = familia;
		Codigo = codigo;
		Mensaje = mensaje;
	}

	/// <summary>Lo que el servidor guardó, o <see langword="null"/> si la rechazó.</summary>
	public EvidenciaRegistrada? Registrada { get; }

	/// <summary>Naturaleza del rechazo, que decide si se reintenta.</summary>
	public FamiliaErrorSincronizacion? Familia { get; }

	/// <summary>Código estable de Jacob. <b>Se decide por él, nunca por el mensaje.</b></summary>
	public string? Codigo { get; }

	/// <summary>Mensaje para mostrarle al operador.</summary>
	public string? Mensaje { get; }

	public bool Exito => Registrada is not null;

	public static ResultadoEnvioEvidencia Aceptada(EvidenciaRegistrada registrada) =>
		new(registrada, null, null, null);

	public static ResultadoEnvioEvidencia Rechazada(
		FamiliaErrorSincronizacion familia,
		string codigo,
		string mensaje) =>
		new(null, familia, codigo, mensaje);
}
