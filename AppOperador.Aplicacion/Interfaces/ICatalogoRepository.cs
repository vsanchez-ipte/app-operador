using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface ICatalogoRepository
{
	Task<CatalogosOperacion> ObtenerAsync(CancellationToken cancelacion = default);

	// Reemplaza, no mezcla: un tipo retirado del catálogo tiene que desaparecer.
	Task ReemplazarAsync(CatalogosOperacion catalogos, CancellationToken cancelacion = default);
}
