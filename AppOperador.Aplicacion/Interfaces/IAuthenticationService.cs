using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Acceso del operador a la aplicación.
/// </summary>
/// <remarks>
/// El nombre está fijado en inglés por el documento de arquitectura. La implementación
/// real hablará con el canal móvil del API de Jacob (<c>/ITS/AppLogin/*</c>); mientras
/// ese canal no exista, se resuelve con un simulador.
/// </remarks>
public interface IAuthenticationService
{
	/// <summary>
	/// Obtiene las unidades que el operador puede seleccionar al acceder.
	/// </summary>
	/// <remarks>
	/// En el diseño final este catálogo lo devuelve la preautenticación, no el cliente.
	/// </remarks>
	Task<IReadOnlyList<UnidadVehicular>> ObtenerUnidadesAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Valida credenciales y unidad contra Jacob CCO y abre la sesión.
	/// </summary>
	Task<ResultadoAcceso> IngresarAsync(
		string usuario,
		string contrasena,
		UnidadVehicular unidad,
		CancellationToken cancelacion = default);

	/// <summary>
	/// Reanuda una sesión ya validada en línea, sin volver a contactar a Jacob CCO.
	/// </summary>
	/// <remarks>
	/// Solo prospera si queda ventana offline de la última validación; si no, rechaza con
	/// <see cref="MotivoRechazoAcceso.SesionOfflineExpirada"/>. **No** es un acceso sin
	/// validación previa: el primer ingreso siempre exige enlace.
	/// </remarks>
	Task<ResultadoAcceso> ContinuarSinConexionAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Cierra la sesión conservando los registros pendientes del operador.
	/// </summary>
	Task CerrarSesionAsync(CancellationToken cancelacion = default);
}
