namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Desenlace del primer paso del acceso (<c>POST ITS/AppLogin/Preauth</c>): o Jacob CCO
/// emitió un desafío, o rechazó con una causa.
/// </summary>
/// <remarks>
/// <para>
/// El acceso real son dos pasos: la preautenticación valida credencial, permiso y unidades
/// y devuelve un desafío de un solo uso; el segundo paso lo consume junto con la unidad
/// elegida y abre la sesión. JTT-1378 cubre únicamente el primero.
/// </para>
/// <para>
/// <b>Seguridad.</b> <see cref="ChallengeId"/> no debe registrarse en bitácora, guardarse
/// en SQLite ni escribirse en logs: es la credencial del segundo paso. Vive en memoria
/// mientras dura la operación y se descarta al salir de la pantalla.
/// </para>
/// </remarks>
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

	/// <summary>Indica si Jacob CCO emitió un desafío válido.</summary>
	public bool Exitoso { get; }

	/// <summary>
	/// Desafío de un solo uso, válido cinco minutos. Solo tiene valor si <see cref="Exitoso"/>.
	/// </summary>
	/// <remarks>No registrar ni persistir. Ver las notas de seguridad del tipo.</remarks>
	public string? ChallengeId { get; }

	/// <summary>Caducidad del desafío, en UTC.</summary>
	public DateTime? ExpiraUtc { get; }

	/// <summary>
	/// Unidades activas asignadas al operador, tal como llegan del contrato.
	/// </summary>
	/// <remarks>
	/// Se modelan y deserializan porque forman parte de la respuesta, pero JTT-1378 **no**
	/// las muestra ni permite elegir una: eso pertenece a una historia posterior.
	/// </remarks>
	public IReadOnlyList<UnidadVehicular> Unidades { get; }

	/// <summary>Causa del rechazo. Solo tiene valor cuando <see cref="Exitoso"/> es falso.</summary>
	public MotivoRechazoAcceso? Motivo { get; }

	/// <summary>
	/// Código funcional devuelto por Jacob (<c>appoperador.*</c>), para trazas de soporte.
	/// </summary>
	/// <remarks>
	/// Es un identificador de catálogo, no un dato del operador: puede registrarse sin
	/// riesgo. El <c>mensajeError</c> del API no se conserva, porque es texto para
	/// desarrollador y no el literal que exige JTT-279.
	/// </remarks>
	public string? CodigoError { get; }

	public static ResultadoPreauth Emitido(
		string challengeId,
		DateTime expiraUtc,
		IReadOnlyList<UnidadVehicular> unidades) =>
		new(true, challengeId, expiraUtc, unidades, null, null);

	public static ResultadoPreauth Rechazado(MotivoRechazoAcceso motivo, string? codigoError = null) =>
		new(false, null, null, [], motivo, codigoError);
}
