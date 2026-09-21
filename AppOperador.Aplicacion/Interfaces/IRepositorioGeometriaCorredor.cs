using AppOperador.Domain.Entidades;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// De dónde sale la geometría del corredor con la que se calcula el kilómetro.
/// </summary>
/// <remarks>
/// <para>
/// <b>Existe para que la geometría pueda dejar de venir dentro del paquete.</b> Hoy viaja
/// embebida en la app —es lo único entregable sin trabajo del API—, pero el corredor **crece**:
/// el KMZ recibido cubre Tijuana–Tecate y el CA 3 de JTT-1395 habla de Tijuana–Mexicali. Con la
/// geometría leída directamente donde haga falta, ampliarla obligaría a publicar una versión
/// nueva en las tiendas; detrás de este contrato, el día que el canal móvil la publique
/// —como ya hace con los catálogos en JTT-1394— **se cambia la implementación y nada más**.
/// </para>
/// <para>
/// <b>Es asíncrono aunque hoy lea un recurso incrustado</b>, justamente por eso: una versión que
/// la descargue y la cachee no puede ser síncrona, y cambiar la firma después obligaría a tocar
/// el caso de uso y sus pruebas.
/// </para>
/// </remarks>
public interface IRepositorioGeometriaCorredor
{
	/// <summary>
	/// Devuelve la geometría vigente del corredor.
	/// </summary>
	/// <remarks>
	/// <b>Nunca devuelve nulo.</b> Sin geometría no hay kilómetro por GPS y la app tendría que
	/// decidir qué hacer con esa nada en cada punto donde la use; una implementación que no pueda
	/// entregarla debe fallar al construirse, no al consultarse.
	/// </remarks>
	Task<GeometriaCorredor> ObtenerAsync(CancellationToken cancelacion = default);
}
