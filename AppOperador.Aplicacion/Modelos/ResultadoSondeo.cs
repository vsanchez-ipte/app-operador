namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Desenlace de sondear el enlace con Jacob CCO (JTT-1391 CA 3).
/// </summary>
/// <remarks>
/// <para>
/// Sustituye al <c>bool</c> que devolvía la sonda. Un booleano solo podía decir «no hay
/// enlace», y con eso la app mostraba «Sin conexión» tanto cuando no se alcanzaba al servidor
/// como cuando el servidor contestaba un <c>404</c>. Son cosas distintas: la primera la
/// resuelve el operador esperando o moviéndose; la segunda no la resuelve él.
/// </para>
/// <para>
/// <see cref="Detalle"/> es texto técnico y va al registro, <b>no a la pantalla</b>. Lo que ve
/// el operador lo decide quien presenta, a partir de <see cref="Causa"/>.
/// </para>
/// </remarks>
public sealed class ResultadoSondeo
{
	private ResultadoSondeo(bool hayEnlace, CausaSinEnlace causa, int? codigoHttp, string detalle)
	{
		HayEnlace = hayEnlace;
		Causa = causa;
		CodigoHttp = codigoHttp;
		Detalle = detalle;
	}

	/// <summary>Indica si se puede operar contra Jacob CCO.</summary>
	public bool HayEnlace { get; }

	/// <summary>Por qué no hay enlace. <see cref="CausaSinEnlace.Ninguna"/> cuando sí lo hay.</summary>
	public CausaSinEnlace Causa { get; }

	/// <summary>Código de estado, cuando el servidor llegó a contestar.</summary>
	public int? CodigoHttp { get; }

	/// <summary>Descripción técnica para el registro.</summary>
	public string Detalle { get; }

	/// <summary>
	/// Indica si el servidor contestó, aunque su respuesta no sirviera.
	/// </summary>
	/// <remarks>
	/// Es la distinción que faltaba: separa un problema de comunicación de uno del servidor.
	/// </remarks>
	public bool ServidorRespondio => CodigoHttp is not null;

	/// <summary>Jacob contestó que la sesión sigue viva.</summary>
	public static ResultadoSondeo Alcanzado(int codigoHttp = 200) =>
		new(true, CausaSinEnlace.Ninguna, codigoHttp, "Jacob CCO confirmó la sesión.");

	/// <summary>
	/// Se da por bueno el enlace sin sondear, porque todavía no hay sesión con qué preguntar.
	/// </summary>
	/// <remarks>
	/// La sonda va autenticada. Antes de entrar no hay token, y declarar que no hay enlace
	/// pondría el aviso de modo offline delante de quien ni siquiera ha intentado acceder.
	/// </remarks>
	public static ResultadoSondeo SegunLaRed() =>
		new(true, CausaSinEnlace.Ninguna, null, "Sin sesión todavía: se cree a la red del dispositivo.");

	/// <summary>No se alcanzó al servidor.</summary>
	public static ResultadoSondeo SinTransporte(string detalle) =>
		new(false, CausaSinEnlace.SinTransporte, null, detalle);

	/// <summary>No hay sesión abierta, así que no hay sonda que enviar.</summary>
	public static ResultadoSondeo SinSesion() =>
		new(false, CausaSinEnlace.SinSesion, null, "No hay token de sesión con el que sondear.");

	/// <summary>
	/// Traduce el código de estado que devolvió Jacob.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>401</c> y <c>403</c> van juntos: en ambos el servidor está diciendo que no acepta
	/// esta sesión, y el remedio es el mismo.
	/// </para>
	/// <para>
	/// Todo lo demás que no sea éxito es un problema del servidor. <b>No se distingue entre
	/// <c>404</c> y <c>5xx</c></b>: para quien usa la app son lo mismo —no lo puede arreglar—
	/// y el código exacto queda en <see cref="CodigoHttp"/> para quien sí puede.
	/// </para>
	/// </remarks>
	public static ResultadoSondeo Desde(int codigoHttp) => codigoHttp switch
	{
		>= 200 and < 300 => Alcanzado(codigoHttp),

		401 or 403 => new(
			false,
			CausaSinEnlace.SesionRechazada,
			codigoHttp,
			$"Jacob CCO no acepta la sesión (HTTP {codigoHttp})."),

		_ => new(
			false,
			CausaSinEnlace.RespuestaDeError,
			codigoHttp,
			$"Jacob CCO respondió HTTP {codigoHttp} en la sonda de estado."),
	};
}
