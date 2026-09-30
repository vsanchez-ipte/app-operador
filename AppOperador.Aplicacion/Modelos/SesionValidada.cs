using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

public sealed class SesionValidada
{
	public SesionValidada(
		string sessionId,
		string accessToken,
		DateTime tokenExpiraUtc,
		string operador,
		string rol,
		UnidadVehicular unidad,
		PermisosOperador permisos,
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

	public string SessionId { get; }

	// Credencial de todas las peticiones: solo va al almacenamiento seguro, nunca a logs.
	public string AccessToken { get; }

	public DateTime TokenExpiraUtc { get; }

	public string Operador { get; }

	public string Rol { get; }

	public UnidadVehicular Unidad { get; }

	public PermisosOperador Permisos { get; }

	public VigenciaOffline Vigencia { get; }

	// Detecta relojes desfasados: el canal móvil tolera dos minutos y después responde 401.
	public DateTime HoraServidorUtc { get; }
}
