namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Desenlace de un intento de acceso: o se autorizó, o se rechazó con una causa.
/// </summary>
/// <remarks>
/// No se usan excepciones para el rechazo: negar el acceso es un resultado esperado del
/// caso de uso, no una condición excepcional.
/// </remarks>
public sealed class ResultadoAcceso
{
	private ResultadoAcceso(bool autorizado, MotivoRechazoAcceso? motivo, SesionOperador? sesion)
	{
		Autorizado = autorizado;
		Motivo = motivo;
		Sesion = sesion;
	}

	/// <summary>Indica si el operador quedó dentro de la app.</summary>
	public bool Autorizado { get; }

	/// <summary>Causa del rechazo. Solo tiene valor cuando <see cref="Autorizado"/> es falso.</summary>
	public MotivoRechazoAcceso? Motivo { get; }

	/// <summary>Sesión resultante. Solo tiene valor cuando <see cref="Autorizado"/> es verdadero.</summary>
	public SesionOperador? Sesion { get; }

	public static ResultadoAcceso Autorizar(SesionOperador sesion) => new(true, null, sesion);

	public static ResultadoAcceso Rechazar(MotivoRechazoAcceso motivo) => new(false, motivo, null);
}
