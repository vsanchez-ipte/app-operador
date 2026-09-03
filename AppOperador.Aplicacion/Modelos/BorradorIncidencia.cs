namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Contenido editable de un borrador, tal como se devuelve al reabrirlo (JTT-1399 CA 8).
/// </summary>
/// <remarks>
/// <para>
/// <b>No es un <see cref="RegistroCola"/>.</b> Aquél es el resumen que lista la pantalla de Cola
/// —clase, prioridad, descripción—; éste es lo que hay que volver a poner dentro del formulario
/// para que el operador siga escribiendo donde se quedó.
/// </para>
/// <para>
/// Todo viene anulable menos la nota porque <b>un borrador admite datos incompletos</b>
/// (CA 1): puede no tener tipo, ni severidad, ni kilómetro. El kilómetro viaja como texto crudo
/// y no como <c>Kilometer</c> por la misma razón — un <c>130+</c> a medio escribir es un
/// borrador válido y sería un <c>Kilometer</c> inválido.
/// </para>
/// </remarks>
/// <param name="ClaveLocal">Identificador local del borrador, <c>LOC-######</c>.</param>
/// <param name="TipoId">Identificador del tipo elegido, si ya se eligió.</param>
/// <param name="Kilometro">Kilómetro tal como se escribió, sin validar.</param>
/// <param name="SeveridadId">Identificador del nivel elegido, si ya se eligió.</param>
/// <param name="Nota">Nota capturada hasta ahora. Vacía si no se ha escrito nada.</param>
/// <param name="Uuid">
/// Identidad del registro. <b>Es la clave de idempotencia frente al servidor</b> y la que ata
/// las evidencias a su incidencia (JTT-1398): la clave local sirve para que el operador la
/// reconozca, pero lo que viaja y lo que relaciona es el UUID. Se conserva al convertir el
/// borrador en incidencia, así que la evidencia adjuntada antes de guardar sigue siendo suya.
/// </param>
public sealed record BorradorIncidencia(
	string Uuid,
	string ClaveLocal,
	int? TipoId,
	string? Kilometro,
	Guid? SeveridadId,
	string Nota);
