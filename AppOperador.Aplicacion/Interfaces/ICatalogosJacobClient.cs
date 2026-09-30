using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface ICatalogosJacobClient
{
	// Nulo si no se pudo traer o si llegó sin tipos o sin severidades: se sigue con la copia local.
	Task<CatalogosOperacion?> ObtenerVigentesAsync(
		string accessToken,
		CancellationToken cancelacion = default);
}
