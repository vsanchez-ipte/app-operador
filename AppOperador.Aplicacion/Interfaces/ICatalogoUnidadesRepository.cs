using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface ICatalogoUnidadesRepository
{
	// Las unidades que ya no llegan se conservan: las sesiones e incidencias viejas apuntan a ellas.
	Task GuardarAsync(
		IReadOnlyList<UnidadVehicular> unidades,
		DateTime sincronizadoUtc,
		CancellationToken cancelacion = default);

	// Solo las de la última sincronización.
	Task<CatalogoUnidades> ObtenerAsync(CancellationToken cancelacion = default);
}
