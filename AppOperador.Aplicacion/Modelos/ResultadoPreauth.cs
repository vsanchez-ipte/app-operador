namespace AppOperador.Aplicacion.Modelos;

public sealed class ResultadoPreauth
{
	private ResultadoPreauth(
		bool exitoso,
		string? challengeId,
		DateTime? expiraUtc,
		IReadOnlyList<UnidadVehicular> unidades,
		MotivoRechazoAcceso? motivo,
		string? codigoError)
	{
		Exitoso = exitoso;
		ChallengeId = challengeId;
		ExpiraUtc = expiraUtc;
		Unidades = unidades;
		Motivo = motivo;
		CodigoError = codigoError;
	}

	public bool Exitoso { get; }

	// Credencial del segundo paso: no se registra, no se persiste ni se escribe en logs.
	public string? ChallengeId { get; }

	public DateTime? ExpiraUtc { get; }

	public IReadOnlyList<UnidadVehicular> Unidades { get; }

	public MotivoRechazoAcceso? Motivo { get; }

	public string? CodigoError { get; }

	public static ResultadoPreauth Emitido(
		string challengeId,
		DateTime expiraUtc,
		IReadOnlyList<UnidadVehicular> unidades) =>
		new(true, challengeId, expiraUtc, unidades, null, null);

	public static ResultadoPreauth Rechazado(MotivoRechazoAcceso motivo, string? codigoError = null) =>
		new(false, null, null, [], motivo, codigoError);
}
