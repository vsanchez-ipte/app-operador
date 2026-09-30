using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

// Aún fuera del espacio privado. El contenido se abre solo al copiar, para no cargar un video en memoria.
public sealed record ArchivoElegido(
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	Func<CancellationToken, Task<Stream>> AbrirContenido,
	// El origen decide si se renombra: lo capturado llega con un GUID; lo elegido trae su nombre.
	OrigenEvidencia Origen = OrigenEvidencia.Galeria);

public sealed record EvidenciaAdjunta(
	string Uuid,
	string IncidenciaUuid,
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	string RutaArchivo,
	EstadoSincronizacion Estado,
	string? UltimoErrorCodigo = null);

public sealed record ResultadoAdjuntar
{
	private ResultadoAdjuntar(EvidenciaAdjunta? adjuntada, MotivoEvidenciaRechazada motivo)
	{
		Adjuntada = adjuntada;
		Motivo = motivo;
	}

	public EvidenciaAdjunta? Adjuntada { get; }

	public MotivoEvidenciaRechazada Motivo { get; }

	public bool Exito => Adjuntada is not null;

	public static ResultadoAdjuntar Aceptada(EvidenciaAdjunta evidencia) =>
		new(evidencia, MotivoEvidenciaRechazada.Ninguno);

	public static ResultadoAdjuntar Rechazada(MotivoEvidenciaRechazada motivo) =>
		new(null, motivo);
}

// TipoMime es el que determinó el servidor por contenido.
public sealed record EvidenciaRegistrada(
	string IdEvidencia,
	string TipoMime,
	string HashSha256,
	bool YaExistia);

// Idempotente por incidencia y contenido: reintentar no gasta cupo, y YaExistia es éxito.
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

	public EvidenciaRegistrada? Registrada { get; }

	public FamiliaErrorSincronizacion? Familia { get; }

	// Se decide por el código, nunca por el mensaje.
	public string? Codigo { get; }

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
