using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

public static class ReglaCapacidades
{
	public const string PermisoAppOperadorMovil = "APP_OPERADOR_MOVIL";

	// Debe coincidir con el API; si difiere, la captura se apaga sin ningún error visible.
	public const string PermisoCapturaIncidencias = "APP_OPERADOR_CAPTURA";

	// Sin sesión no se concede nada.
	public static bool Concede(PermisosOperador? permisos, CapacidadOperador capacidad)
	{
		if (permisos is null || permisos.Count == 0)
		{
			return false;
		}

		if (permisos.Contiene(CodigoDe(capacidad)))
		{
			return true;
		}

		// El permiso general no suple a los que Jacob emite por separado.
		return !ExigePermisoPropio(capacidad)
			&& permisos.Contiene(PermisoAppOperadorMovil);
	}

	// Agregar aquí las capacidades que Jacob empiece a emitir por separado.
	private static bool ExigePermisoPropio(CapacidadOperador capacidad) =>
		capacidad is CapacidadOperador.RegistrarIncidencia
			or CapacidadOperador.AdjuntarEvidencia;

	// Solo el de captura existe en el servidor; no inventar uno para evidencia o quedaría bloqueada.
	private static string CodigoDe(CapacidadOperador capacidad) => capacidad switch
	{
		CapacidadOperador.RegistrarIncidencia => PermisoCapturaIncidencias,
		CapacidadOperador.AdjuntarEvidencia => PermisoCapturaIncidencias,
		CapacidadOperador.ConsultarCola => "APP_OPERADOR_COLA",
		CapacidadOperador.Sincronizar => "APP_OPERADOR_SINCRONIZACION",
		_ => string.Empty,
	};
}
