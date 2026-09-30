using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

public sealed class ColaSincronizacionEnMemoria : ISyncQueueService
{
	private readonly AlmacenRegistrosEnMemoria _almacen;
	private readonly ISessionStore _sesiones;

	public ColaSincronizacionEnMemoria(
		AlmacenRegistrosEnMemoria almacen,
		ISessionStore sesiones)
	{
		_almacen = almacen;
		_sesiones = sesiones;
	}

	private string? OperadorActual => _sesiones.Actual?.Operador;

	public Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default)
	{
		// Los borradores se listan aparte, en la pantalla de Captura.
		IReadOnlyList<RegistroCola> registros = _almacen.Todos(OperadorActual)
			.Where(r => r.Estado != EstadoSincronizacion.Borrador)
			.ToList();

		return Task.FromResult(registros);
	}

	public Task<int> ContarPendientesAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(_almacen.Contar(EstadoSincronizacion.Pendiente, OperadorActual));

	public Task<int> RecuperarEnviosInterrumpidosAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(0);

	public Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(
		CancellationToken cancelacion = default) =>
		Task.FromResult<IReadOnlyList<IncidenciaEnviable>>([]);

	public Task<IncidenciaEnviable?> ObtenerEnviablePorClaveAsync(
		string claveLocal,
		CancellationToken cancelacion = default) =>
		Task.FromResult<IncidenciaEnviable?>(null);

	public Task ActualizarEnvioAsync(
		ActualizacionEnvio actualizacion,
		CancellationToken cancelacion = default) =>
		Task.CompletedTask;

	public Task RegistrarIntentoAsync(
		string uuid,
		bool exito,
		string? codigo,
		string? mensaje,
		CancellationToken cancelacion = default) =>
		Task.CompletedTask;
}
