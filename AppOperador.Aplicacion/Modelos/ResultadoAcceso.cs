namespace AppOperador.Aplicacion.Modelos;

public sealed class ResultadoAcceso
{
	private ResultadoAcceso(bool autorizado, MotivoRechazoAcceso? motivo, SesionOperador? sesion)
	{
		Autorizado = autorizado;
		Motivo = motivo;
		Sesion = sesion;
	}

	public bool Autorizado { get; }

	public MotivoRechazoAcceso? Motivo { get; }

	public SesionOperador? Sesion { get; }

	public static ResultadoAcceso Autorizar(SesionOperador sesion) => new(true, null, sesion);

	public static ResultadoAcceso Rechazar(MotivoRechazoAcceso motivo) => new(false, motivo, null);
}
