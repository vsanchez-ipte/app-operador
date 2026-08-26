using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>
/// Cotejo de los permisos contra lo que el token trae firmado (JTT-1379 CA 8).
/// </summary>
/// <remarks>
/// Une las dos mitades de la comprobación —leer los módulos del token y contrastarlos con
/// los permisos— para que los tres sitios que la necesitan la hagan igual: el acceso, la
/// reanudación sin conexión y la revalidación.
/// </remarks>
public static class RespaldoDePermisos
{
	/// <summary>
	/// Indica si el token respalda todos los permisos indicados.
	/// </summary>
	/// <remarks>
	/// Un token ausente o ilegible no respalda nada, y la consecuencia es bloquear: es la
	/// salida correcta ante algo que no se puede interpretar.
	/// </remarks>
	public static bool Respaldan(this ITokenClaims claims, PermisosOperador permisos, string? token)
	{
		ArgumentNullException.ThrowIfNull(claims);
		ArgumentNullException.ThrowIfNull(permisos);

		return permisos.RespaldadosPor(claims.ModulosDe(token));
	}
}
