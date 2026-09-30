namespace AppOperador.Aplicacion.Modelos;

public sealed class ResultadoSondeo
{
	private ResultadoSondeo(bool hayEnlace, CausaSinEnlace causa, int? codigoHttp, string detalle)
	{
		HayEnlace = hayEnlace;
		Causa = causa;
		CodigoHttp = codigoHttp;
		Detalle = detalle;
	}

	public bool HayEnlace { get; }

	public CausaSinEnlace Causa { get; }

	public int? CodigoHttp { get; }

	// Texto técnico para el registro, no para la pantalla.
	public string Detalle { get; }

	public bool ServidorRespondio => CodigoHttp is not null;

	public static ResultadoSondeo Alcanzado(int codigoHttp = 200) =>
		new(true, CausaSinEnlace.Ninguna, codigoHttp, "Jacob CCO confirmó la sesión.");

	// Sin sesión no hay sonda autenticada; no se muestra «offline» a quien aún no entra.
	public static ResultadoSondeo SegunLaRed() =>
		new(true, CausaSinEnlace.Ninguna, null, "Sin sesión todavía: se cree a la red del dispositivo.");

	public static ResultadoSondeo Observado(bool jacobRespondio) => jacobRespondio
		? new(true, CausaSinEnlace.Ninguna, null, "Jacob CCO contestó a una petición del acceso.")
		: new(false, CausaSinEnlace.SinTransporte, null, "Una petición del acceso no alcanzó a Jacob CCO.");

	public static ResultadoSondeo SinTransporte(string detalle) =>
		new(false, CausaSinEnlace.SinTransporte, null, detalle);

	public static ResultadoSondeo SinSesion() =>
		new(false, CausaSinEnlace.SinSesion, null, "No hay token de sesión con el que sondear.");

	public static ResultadoSondeo Desde(int codigoHttp) => codigoHttp switch
	{
		>= 200 and < 300 => Alcanzado(codigoHttp),

		// 401 y 403 van juntos; 404 y 5xx también: el operador no puede arreglarlos.
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
