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
public sealed record ResultadoSincronizacion(
	int Confirmados,
	int Intentados,
	MotivoNoSincroniza? MotivoBloqueo,
	FamiliaErrorSincronizacion? FamiliaUltimoError = null,
	string? MensajeUltimoError = null);

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
}
