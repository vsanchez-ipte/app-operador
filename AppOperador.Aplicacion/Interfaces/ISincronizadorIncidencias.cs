using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

public interface ISincronizadorIncidencias
{
	Task<ResultadoSincronizacion> EjecutarAsync(CancellationToken cancelacion = default);

	// Solo la recién capturada: el operador está parado en el incidente y no debe esperar a la cola.
	Task<ResultadoSincronizacion> EnviarUnaAsync(
		string claveLocal,
		CancellationToken cancelacion = default);
}
