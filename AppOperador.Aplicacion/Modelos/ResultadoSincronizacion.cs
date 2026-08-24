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
public sealed record ResultadoSincronizacion(
	int Confirmados,
	int Intentados,
	MotivoNoSincroniza? MotivoBloqueo);

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
