using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Servicios;

public static class RespaldoDePermisos
{
	public static bool Respaldan(this ITokenClaims claims, PermisosOperador permisos, string? token)
	{
		ArgumentNullException.ThrowIfNull(claims);
		ArgumentNullException.ThrowIfNull(permisos);

		return permisos.RespaldadosPor(claims.ModulosDe(token));
	}
}
