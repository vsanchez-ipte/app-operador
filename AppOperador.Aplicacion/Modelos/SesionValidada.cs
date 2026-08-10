using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Sesión móvil recién creada por Jacob CCO, tal como la devuelve
/// <c>POST ITS/AppLogin</c> (JTT-1382).
/// </summary>
/// <remarks>
/// <para>
/// <b>Solo puede nacer de una respuesta del servidor.</b> No hay forma de construirla con
/// datos inventados por la app, que es lo que exige el CA 8: una sesión validada no se
/// fabrica localmente. El token va firmado con un secreto que la app no tiene.
/// </para>
/// <para>
/// Las fechas llegan en UTC y se conservan en UTC. La conversión a hora local es cosa de la
/// presentación, no de este modelo.
/// </para>
/// </remarks>
public sealed class SesionValidada
{
	public SesionValidada(
		string sessionId,
		string accessToken,
		DateTime tokenExpiraUtc,
		string operador,
		string rol,
		UnidadVehicular unidad,
		IReadOnlyList<string> permisos,
		VigenciaOffline vigencia,
		DateTime horaServidorUtc)
	{
		SessionId = sessionId;
		AccessToken = accessToken;
		TokenExpiraUtc = tokenExpiraUtc;
		Operador = operador;
		Rol = rol;
		Unidad = unidad;
		Permisos = permisos;
		Vigencia = vigencia;
		HoraServidorUtc = horaServidorUtc;
	}

	/// <summary>Identificador de la sesión. Sirve para trazas y soporte.</summary>
	public string SessionId { get; }

	/// <summary>
	/// Token de acceso del esquema <c>MobileBearer</c>.
	/// </summary>
	/// <remarks>
	/// <b>No registrar ni persistir fuera del almacenamiento seguro.</b> Es la credencial de
	/// todas las peticiones posteriores.
	/// </remarks>
	public string AccessToken { get; }

	/// <summary>Caducidad del token, en UTC. Es distinta de la vigencia offline.</summary>
	public DateTime TokenExpiraUtc { get; }

	/// <summary>Nombre visible del operador.</summary>
	public string Operador { get; }

	/// <summary>Rol funcional con el que ingresó.</summary>
	public string Rol { get; }

	/// <summary>Unidad con la que va a operar, con su identificador técnico.</summary>
	public UnidadVehicular Unidad { get; }

	/// <summary>Capacidades activas que devuelve Jacob.</summary>
	public IReadOnlyList<string> Permisos { get; }

	/// <summary>
	/// Ventana offline, <b>calculada por el servidor</b> y adoptada sin recalcular
	/// (CA 3 y CA 4).
	/// </summary>
	public VigenciaOffline Vigencia { get; }

	/// <summary>
	/// Hora del servidor en el momento de crear la sesión.
	/// </summary>
	/// <remarks>
	/// Permite detectar que el reloj del dispositivo está desfasado. El canal móvil tolera
	/// dos minutos: más allá, cualquier petición posterior responde <c>401</c> y el motivo
	/// no sería evidente desde la app.
	/// </remarks>
	public DateTime HoraServidorUtc { get; }
}
