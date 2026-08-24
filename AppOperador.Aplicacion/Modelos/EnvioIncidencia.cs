using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Lo que la app manda a Jacob para registrar una incidencia (JTT-1401).
/// </summary>
/// <remarks>
/// <para>
/// <b>Lo que no está aquí es tan importante como lo que está.</b> El operador que sincroniza, su
/// rol, la sesión actual y el vehículo <b>no viajan</b>: el servidor los toma del token. Si se
/// mandaran en el cuerpo se ignorarían, y es justo lo que impide registrar a nombre de otro
/// (JTT-1404 CA 6). La plaza de cobro y el tramo tampoco: el servidor los deriva del kilómetro.
/// </para>
/// <para>
/// <b><see cref="Cuerpo"/> y <see cref="IdAfectacion"/> los exige el modelo de Jacob y el
/// formulario no los captura</b>, porque ningún criterio los pide —se verificó contra los once
/// de JTT-1393—. Se rellenan del catálogo local al enviar. Ver
/// <c>RellenoCamposNoCapturados</c>, que es la única pieza que hay que borrar el día que el API
/// deje de exigirlos.
/// </para>
/// </remarks>
/// <param name="Uuid">Llave de idempotencia que generó el dispositivo. Reenviarla no duplica.</param>
/// <param name="IdTipoIncidencia">Identificador del tipo en el catálogo de Jacob.</param>
/// <param name="IdGravedad">Identificador del nivel de severidad.</param>
/// <param name="IdAfectacion">Grado de cierre de la vía. Rellenado, no capturado.</param>
/// <param name="Km">Kilómetro en decimal, entre 120.000 y 148.000.</param>
/// <param name="FuenteKilometro"><c>GPS</c> o <c>MANUAL</c>, exactamente.</param>
/// <param name="Cuerpo">Cuerpo de la vía: <c>A</c>, <c>B</c>, <c>C</c> o <c>D</c>. Rellenado.</param>
/// <param name="Nota">Nota del operador.</param>
/// <param name="FchCapturaCampo">Cuándo capturó el operador, en UTC. El backend nunca la reemplaza.</param>
/// <param name="IdSesionOrigen">Sesión en la que se capturó, si se conoce.</param>
public sealed record EnvioIncidencia(
	string Uuid,
	int IdTipoIncidencia,
	Guid? IdGravedad,
	int IdAfectacion,
	decimal Km,
	string FuenteKilometro,
	string Cuerpo,
	string Nota,
	DateTime FchCapturaCampo,
	Guid? IdSesionOrigen);

/// <summary>
/// Lo que Jacob contesta cuando acepta una incidencia.
/// </summary>
/// <param name="Folio">Folio central, con la forma <c>INC-APK-2026-0034</c>.</param>
/// <param name="FchRecepcionCentral">Cuándo llegó a Jacob, según el reloj del servidor.</param>
/// <param name="YaExistia">
/// <see langword="true"/> si el <c>uuid</c> ya estaba registrado.
/// <b>No es un error</b>: significa que la respuesta anterior se perdió en la carretera y la app
/// hizo lo correcto al reenviar. Devuelve el mismo folio.
/// </param>
public sealed record IncidenciaRegistrada(
	string Folio,
	DateTime FchRecepcionCentral,
	bool YaExistia);

/// <summary>
/// Resultado de un intento de envío, en la forma que el caso de uso necesita para decidir.
/// </summary>
/// <remarks>
/// <b>Un solo tipo para las dos salidas</b>, y no excepciones para el fallo: que Jacob rechace un
/// registro no es excepcional, es la mitad del trabajo de sincronizar. Con excepciones, el CA 13
/// —que una falla no bloquee a las demás— dependería de acordarse de envolver cada envío en un
/// <c>try</c>.
/// </remarks>
public sealed record ResultadoEnvio
{
	private ResultadoEnvio(
		IncidenciaRegistrada? registrada,
		FamiliaErrorSincronizacion? familia,
		string? codigo,
		string? mensaje)
	{
		Registrada = registrada;
		Familia = familia;
		Codigo = codigo;
		Mensaje = mensaje;
	}

	/// <summary>Datos de la incidencia aceptada, o <see langword="null"/> si fue rechazada.</summary>
	public IncidenciaRegistrada? Registrada { get; }

	/// <summary>Naturaleza del rechazo, que decide si se reintenta.</summary>
	public FamiliaErrorSincronizacion? Familia { get; }

	/// <summary>Código estable de Jacob. <b>Es por lo que se decide, nunca por el mensaje.</b></summary>
	public string? Codigo { get; }

	/// <summary>Mensaje para mostrarle al operador (CA 10).</summary>
	public string? Mensaje { get; }

	public bool Exito => Registrada is not null;

	public static ResultadoEnvio Aceptada(IncidenciaRegistrada registrada) =>
		new(registrada, null, null, null);

	public static ResultadoEnvio Rechazada(
		FamiliaErrorSincronizacion familia,
		string codigo,
		string mensaje) =>
		new(null, familia, codigo, mensaje);
}
