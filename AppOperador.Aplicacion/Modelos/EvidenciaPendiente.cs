using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

// Con la clave local de la incidencia y no su uuid: es lo que el operador ve en la Cola.
public sealed record EvidenciaPendiente(
	string Uuid,
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	EstadoSincronizacion Estado,
	string ClaveLocalIncidencia,
	string? UltimoErrorCodigo = null)
{
	public string EstadoLegible => Estado switch
	{
		EstadoSincronizacion.Fallido when CodigosErrorJacob.EsFuncional(UltimoErrorCodigo) =>
			"rechazada por el CCO, no se enviará",
		EstadoSincronizacion.Fallido => "con error, se reintentará",
		_ => "pendiente de enviar",
	};
}
