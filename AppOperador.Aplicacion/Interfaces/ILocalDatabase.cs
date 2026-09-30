namespace AppOperador.Aplicacion.Interfaces;

public interface ILocalDatabase
{
	// Idempotente: las pantallas la llaman sin coordinarse.
	Task InicializarAsync(CancellationToken cancelacion = default);

	Task<int> ObtenerVersionEsquemaAsync(CancellationToken cancelacion = default);

	string RutaArchivo { get; }
}
