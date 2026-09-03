using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Estado del servicio de ubicación del dispositivo y del permiso de la app sobre él, con
/// las dos salidas que el operador tiene para desbloquearlo.
/// </summary>
/// <remarks>
/// <para>
/// Es un puerto <b>distinto</b> de <see cref="ILocationService"/> y no lo sustituye. Aquel
/// traduce una lectura de GPS al kilómetro del negocio —captura de incidencias, JTT-280—;
/// este solo responde si la app <i>puede</i> usar la ubicación. Son responsabilidades
/// separadas: la de aquí condiciona el acceso (JTT-279 PR3, JTT-1380) y no obtiene ni
/// almacena coordenadas.
/// </para>
/// <para>
/// El nombre sigue en inglés por coherencia con el resto de la carpeta, que el documento de
/// arquitectura publicó así; los miembros van en español como en todas las demás.
/// </para>
/// </remarks>
public interface ILocationPermissionService
{
	/// <summary>
	/// Consulta el estado actual sin pedirle nada al operador ni mostrar diálogos.
	/// </summary>
	Task<EstadoUbicacion> ConsultarEstadoAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Pide el permiso al sistema y devuelve el estado resultante.
	/// </summary>
	/// <remarks>
	/// Devuelve el estado <b>ya reevaluado</b>, no lo que contestó el diálogo: entre conceder
	/// el permiso y poder ubicarse todavía está el interruptor del sistema, que puede seguir
	/// apagado.
	/// </remarks>
	Task<EstadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Abre la ficha de configuración de la app, donde se cambia el permiso bloqueado.
	/// </summary>
	/// <returns><see langword="true"/> si se pudo abrir.</returns>
	Task<bool> AbrirAjustesDeLaAppAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Abre la configuración de ubicación del dispositivo, donde se enciende el servicio.
	/// </summary>
	/// <returns><see langword="true"/> si se pudo abrir.</returns>
	Task<bool> AbrirAjustesDeUbicacionAsync(CancellationToken cancelacion = default);
}
