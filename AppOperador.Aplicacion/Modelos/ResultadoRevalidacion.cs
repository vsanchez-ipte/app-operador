using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

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

	public bool Exitoso { get; }

	// Rechazo definitivo obliga a autenticarse; un fallo de red deja la sesión offline como estaba.
	public bool EsRechazoDefinitivo { get; }

	public MotivoRechazoAcceso? Motivo { get; }

	public string? CodigoError { get; }

	public string? Rol { get; }

	public UnidadVehicular? Unidad { get; }

	public PermisosOperador? Permisos { get; }

	public VigenciaOffline? Vigencia { get; }

	public static ResultadoRevalidacion Confirmada(
		string rol,
		UnidadVehicular unidad,
		PermisosOperador permisos,
		VigenciaOffline vigencia) =>
		new(true, false, null, null, rol, unidad, permisos, vigencia);

	public static ResultadoRevalidacion Negada(MotivoRechazoAcceso motivo, string? codigoError = null) =>
		new(false, true, motivo, codigoError, null, null, null, null);

	public static ResultadoRevalidacion SinRespuesta(string? codigoError = null) =>
		new(false, false, MotivoRechazoAcceso.SinComunicacion, codigoError, null, null, null, null);
}
