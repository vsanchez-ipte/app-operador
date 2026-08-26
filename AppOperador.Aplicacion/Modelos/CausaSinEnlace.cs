namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Por qué no se puede operar contra Jacob CCO.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque «no hay enlace» agrupaba situaciones con causas y remedios distintos, y eso
/// mandó a buscar un problema de red donde no lo había: con la app apuntada a un servidor que
/// no publicaba la ruta del canal móvil, el <c>404</c> se mostraba como falta de conexión.
/// </para>
/// <para>
/// La diferencia que importa es <b>si el servidor llegó a contestar</b>. Si no contestó, el
/// operador puede hacer algo: esperar, moverse, buscar señal. Si contestó con un error, no hay
/// nada que él pueda hacer y el aviso debe llevarlo a soporte, no a la antena.
/// </para>
/// </remarks>
public enum CausaSinEnlace
{
	/// <summary>Hay enlace. No hay causa que explicar.</summary>
	Ninguna = 0,

	/// <summary>
	/// No se alcanzó al servidor: sin red, conexión rechazada o sin respuesta a tiempo.
	/// </summary>
	SinTransporte,

	/// <summary>
	/// El servidor contestó que no acepta esta sesión (<c>401</c> o <c>403</c>).
	/// </summary>
	/// <remarks>
	/// Hubo comunicación. Lo que terminó es la sesión, y el remedio es volver a autenticarse.
	/// </remarks>
	SesionRechazada,

	/// <summary>
	/// El servidor contestó con un error que no depende del operador (<c>404</c>, <c>5xx</c>…).
	/// </summary>
	/// <remarks>
	/// Un <c>404</c> en la ruta del canal móvil significa que el servidor no la publica: es un
	/// asunto de despliegue. Reportarlo como falta de conexión esconde la causa real.
	/// </remarks>
	RespuestaDeError,

	/// <summary>No hay sesión abierta, así que no hay sonda autenticada que enviar.</summary>
	/// <remarks>
	/// Defensivo: quien sondea ya comprueba que haya token antes de llamar. Si aparece, es que
	/// alguien llamó sin sesión.
	/// </remarks>
	SinSesion,
}
