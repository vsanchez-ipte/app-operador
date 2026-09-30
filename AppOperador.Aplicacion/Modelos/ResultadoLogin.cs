namespace AppOperador.Aplicacion.Modelos;

public sealed class ResultadoLogin
{
	private ResultadoLogin(SesionValidada? sesion, MotivoRechazoAcceso? motivo, string? codigoError)
	{
		Sesion = sesion;
		Motivo = motivo;
		CodigoError = codigoError;
	}

	public bool Exitoso => Sesion is not null;

	public SesionValidada? Sesion { get; }

	public MotivoRechazoAcceso? Motivo { get; }

	public string? CodigoError { get; }

	public static ResultadoLogin Creada(SesionValidada sesion) => new(sesion, null, null);

	public static ResultadoLogin Rechazado(MotivoRechazoAcceso motivo, string? codigoError = null) =>
		new(null, motivo, codigoError);
}
