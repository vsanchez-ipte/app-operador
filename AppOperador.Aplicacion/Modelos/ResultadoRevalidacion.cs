using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Desenlace de revalidar la sesión contra Jacob CCO (JTT-1383).
/// </summary>
/// <remarks>
/// Tres desenlaces y no dos, porque no todos los fallos significan lo mismo: que no haya
/// red es un contratiempo y la sesión offline sigue valiendo; que Jacob niegue la sesión es
/// una revocación y hay que volver a autenticarse.
/// </remarks>
public sealed class ResultadoRevalidacion
{
	private ResultadoRevalidacion(
		bool exitoso,
		bool esRechazoDefinitivo,
		MotivoRechazoAcceso? motivo,
		string? codigoError,
		string? rol,
		UnidadVehicular? unidad,
		PermisosOperador? permisos,
		VigenciaOffline? vigencia)
	{
		Exitoso = exitoso;
		EsRechazoDefinitivo = esRechazoDefinitivo;
		Motivo = motivo;
		CodigoError = codigoError;
		Rol = rol;
		Unidad = unidad;
		Permisos = permisos;
		Vigencia = vigencia;
	}

	/// <summary>Jacob confirmó la sesión y devolvió una ventana nueva.</summary>
	public bool Exitoso { get; }

	/// <summary>
	/// Jacob negó la sesión: revocada, cuenta inactiva, sin permiso o unidad retirada.
	/// </summary>
	/// <remarks>
	/// Distinto de un fallo de red. Un rechazo definitivo obliga a autenticarse otra vez;
	/// un fallo de comunicación deja la sesión offline como estaba (CA 11).
	/// </remarks>
	public bool EsRechazoDefinitivo { get; }

	public MotivoRechazoAcceso? Motivo { get; }

	public string? CodigoError { get; }

	/// <summary>Rol vigente, que pudo cambiar desde el acceso.</summary>
	public string? Rol { get; }

	/// <summary>Unidad vigente, que pudo cambiar de estado desde el acceso.</summary>
	public UnidadVehicular? Unidad { get; }

	/// <summary>Permisos vigentes, ya cotejados contra el token.</summary>
	public PermisosOperador? Permisos { get; }

	/// <summary>Ventana offline renovada, tal como la calculó el servidor.</summary>
	public VigenciaOffline? Vigencia { get; }

	public static ResultadoRevalidacion Confirmada(
		string rol,
		UnidadVehicular unidad,
		PermisosOperador permisos,
		VigenciaOffline vigencia) =>
		new(true, false, null, null, rol, unidad, permisos, vigencia);

	/// <summary>Jacob negó la sesión. Hay que autenticarse de nuevo.</summary>
	public static ResultadoRevalidacion Negada(MotivoRechazoAcceso motivo, string? codigoError = null) =>
		new(false, true, motivo, codigoError, null, null, null, null);

	/// <summary>No se pudo preguntar. La sesión offline sigue como estaba.</summary>
	public static ResultadoRevalidacion SinRespuesta(string? codigoError = null) =>
		new(false, false, MotivoRechazoAcceso.SinComunicacion, codigoError, null, null, null, null);
}
