using AppOperador.Domain.Reglas;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Datos de la sesión que la app necesita mostrar y evaluar mientras el operador trabaja.
/// </summary>
/// <remarks>
/// La vigencia se delega en <see cref="VigenciaOffline"/>, que es la regla de dominio:
/// aquí no se recalculan las ocho horas.
/// </remarks>
public sealed class SesionOperador
{
	public SesionOperador(
		string operador,
		string rol,
		string unidadVehicular,
		VigenciaOffline vigencia,
		IReadOnlyList<string> permisos,
		string versionAplicacion,
		DateOnly versionCatalogos)
	{
		Operador = operador;
		Rol = rol;
		UnidadVehicular = unidadVehicular;
		Vigencia = vigencia;
		Permisos = permisos;
		VersionAplicacion = versionAplicacion;
		VersionCatalogos = versionCatalogos;
	}

	/// <summary>Nombre de la cuenta del operador en Jacob CCO.</summary>
	public string Operador { get; }

	/// <summary>Rol funcional con el que ingresó.</summary>
	public string Rol { get; }

	/// <summary>Unidad seleccionada al acceder, por ejemplo <c>VEH-01</c>.</summary>
	public string UnidadVehicular { get; }

	/// <summary>Ventana de trabajo sin conexión derivada de la última validación en línea.</summary>
	public VigenciaOffline Vigencia { get; }

	/// <summary>Permisos efectivos, tal como se muestran en el perfil.</summary>
	public IReadOnlyList<string> Permisos { get; }

	/// <summary>Versión de la aplicación instalada.</summary>
	public string VersionAplicacion { get; }

	/// <summary>Fecha del último catálogo descargado.</summary>
	public DateOnly VersionCatalogos { get; }
}
