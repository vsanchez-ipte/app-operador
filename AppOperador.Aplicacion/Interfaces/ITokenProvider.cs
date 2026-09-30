namespace AppOperador.Aplicacion.Interfaces;

// El token va al almacenamiento seguro, nunca a SQLite: la base local viaja en los respaldos.
public interface ITokenProvider
{
	Task GuardarAsync(string accessToken, CancellationToken cancelacion = default);

	Task<string?> ObtenerAsync(CancellationToken cancelacion = default);

	// No toca los pendientes de la cola.
	Task LimpiarAsync(CancellationToken cancelacion = default);
}
