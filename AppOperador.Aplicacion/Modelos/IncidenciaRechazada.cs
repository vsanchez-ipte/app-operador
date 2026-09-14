namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Una incidencia que el CCO rechazó y que el operador puede corregir desde el formulario
/// (JTT-291 CA 7 y 8).
/// </summary>
/// <remarks>
/// <para>
/// Es la misma forma que <see cref="BorradorIncidencia"/> —lo que hace falta para reponer el
/// formulario— más el motivo del rechazo, que es lo que el operador necesita leer mientras
/// corrige. Sin él tendría que volver a la Cola a mirar qué le dijo Jacob.
/// </para>
/// <para>
/// <b>Corregirla no viola la inmutabilidad central</b> (JTT-1407 CA 5): un registro rechazado
/// nunca llegó a crearse en Jacob, así que lo que se reenvía es un alta con el mismo
/// identificador, y la evidencia adjunta sigue siendo suya porque está atada a ese identificador.
/// </para>
/// </remarks>
public sealed record IncidenciaRechazada(
	string Uuid,
	string ClaveLocal,
	int? TipoId,
	string? Kilometro,
	Guid? SeveridadId,
	string Nota,
	string? UltimoErrorCodigo,
	string? UltimoErrorMensaje);
