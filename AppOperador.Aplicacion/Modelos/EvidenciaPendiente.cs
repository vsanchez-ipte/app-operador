using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Una evidencia que todavía no está confirmada por el CCO, con la incidencia a la que pertenece
/// (JTT-292 CA 4 y 6).
/// </summary>
/// <remarks>
/// Lleva la <b>clave local</b> de la incidencia y no su <c>uuid</c>: es lo que el operador ve en
/// la Cola y lo que dicta por radio. Un GUID no le dice a qué registro pertenece la foto.
/// </remarks>
public sealed record EvidenciaPendiente(
	string Uuid,
	string NombreOriginal,
	string TipoMime,
	long Bytes,
	EstadoSincronizacion Estado,
	string ClaveLocalIncidencia);
