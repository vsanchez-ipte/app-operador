namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Desenlace del segundo paso del acceso (<c>POST ITS/AppLogin</c>): o Jacob CCO creó la
/// sesión, o la rechazó con una causa.
/// </summary>
/// <remarks>
/// Mismo criterio que <see cref="ResultadoPreauth"/>: un rechazo es un desenlace esperado y
/// viaja en el resultado, no como excepción.
/// </remarks>
public sealed class ResultadoLogin
{
	private ResultadoLogin(SesionValidada? sesion, MotivoRechazoAcceso? motivo, string? codigoError)
	{
		Sesion = sesion;
		Motivo = motivo;
		CodigoError = codigoError;
	}

	/// <summary>Indica si Jacob CCO creó la sesión.</summary>
	public bool Exitoso => Sesion is not null;

	/// <summary>Sesión creada. Solo tiene valor cuando <see cref="Exitoso"/>.</summary>
	public SesionValidada? Sesion { get; }

	/// <summary>Causa del rechazo. Solo tiene valor cuando no fue exitoso.</summary>
	public MotivoRechazoAcceso? Motivo { get; }

	/// <summary>
	/// Código funcional devuelto por Jacob (<c>appoperador.*</c>), para trazas de soporte.
	/// </summary>
	public string? CodigoError { get; }

	public static ResultadoLogin Creada(SesionValidada sesion) => new(sesion, null, null);

	public static ResultadoLogin Rechazado(MotivoRechazoAcceso motivo, string? codigoError = null) =>
		new(null, motivo, codigoError);
}
