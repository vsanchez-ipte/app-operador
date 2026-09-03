using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Cuántos registros se confirmaron y por qué no salieron los demás.
/// </summary>
/// <param name="Confirmados">Registros que Jacob aceptó.</param>
/// <param name="Intentados">Registros sobre los que se llegó a intentar un envío.</param>
/// <param name="MotivoBloqueo">
/// Por qué no se intentó nada, o <see langword="null"/> si sí se intentó. Es lo que la pantalla
/// le muestra al operador cuando pulsa «Sincronizar» y no pasa nada (CA 10).
/// </param>
/// <param name="FamiliaUltimoError">
/// Naturaleza del último rechazo, o <see langword="null"/> si no hubo ninguno. <b>Es lo que
/// distingue «Jacob la rechazó» de «no se pudo llegar a Jacob»</b>, que para el operador son
/// cosas muy distintas: una la puede corregir y la otra no.
/// </param>
/// <param name="MensajeUltimoError">
/// El mensaje tal como lo dio Jacob, o el del fallo local.
/// <b>Se propaga en vez de reescribirse</b>: quien rechazó sabe por qué mejor que la pantalla, y
/// un texto propio del tipo «no se aceptó» obliga al operador a adivinar qué corregir.
/// </param>
/// <param name="OmitidosEnEspera">
/// Registros que no se intentaron porque su espera de reintento no había vencido (CA 7).
/// <b>Van a salir solos.</b>
/// </param>
/// <param name="OmitidosPorCorregir">
/// Registros que no se intentaron porque su último rechazo fue funcional (CA 8).
/// <b>No van a salir hasta que alguien los corrija</b>, y esa diferencia con los anteriores es
/// justo lo que el operador necesita saber para decidir si esperar o actuar.
/// </param>
public sealed record ResultadoSincronizacion(
	int Confirmados,
	int Intentados,
	MotivoNoSincroniza? MotivoBloqueo,
	FamiliaErrorSincronizacion? FamiliaUltimoError = null,
	string? MensajeUltimoError = null,
	int OmitidosEnEspera = 0,
	int OmitidosPorCorregir = 0);

/// <summary>
/// Por qué la sincronización no llegó a intentarse (JTT-1401 CA 1 y 2).
/// </summary>
public enum MotivoNoSincroniza
{
	/// <summary>No hay sesión abierta con la que autenticarse.</summary>
	SinSesion = 1,

	/// <summary>Hay red, pero Jacob no responde.</summary>
	SinEnlaceConJacob = 2,

	/// <summary>La sesión no autoriza sincronizar.</summary>
	SinPermiso = 3,

	/// <summary>
	/// Ya hay una sincronización en marcha (JTT-1406 CA 6).
	/// </summary>
	/// <remarks>
	/// <b>No es un fallo y nada se pierde:</b> lo pendiente lo está atendiendo la tanda que ya
	/// corre. Se distingue de los demás motivos porque el operador no tiene nada que hacer al
	/// respecto —ni buscar señal, ni volver a ingresar, ni pedir permisos—, solo esperar.
	/// </remarks>
	YaEnCurso = 4,
}
