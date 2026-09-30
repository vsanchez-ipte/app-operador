namespace AppOperador.Aplicacion.Interfaces;

public interface IDatabaseKeyProvider
{
	// Siempre la misma clave: si cambiara, la base dejaría de abrirse y se perderían los pendientes.
	Task<string> ObtenerAsync(CancellationToken cancelacion = default);
}
