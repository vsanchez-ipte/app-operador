using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Cola de sincronización simulada.
/// </summary>
/// <remarks>
/// Lista lo guardado y cuenta pendientes; <b>no participa en el envío</b>. La orquestación
/// vive en <c>SincronizarIncidencias</c> desde JTT-1401, y el recorrido simulado se elimina
/// completo en rama propia: basta con que compile.
/// </remarks>
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

	/// <summary>Operador de la sesión abierta. Sin sesión, la cola se ve vacía.</summary>
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

	// ── Envío (JTT-1401) ──────────────────────────────────────────────────────────────
	//
	// Sin implementar a propósito. El recorrido simulado se elimina completo en rama propia
	// —decisión del 21-ago—: nadie lo prueba y basta con que compile. Lo que vale para estos
	// criterios es ColaSincronizacionSqlite con SincronizarIncidencias encima, que sí tienen
	// pruebas. Una cola vacía de enviables es la respuesta honesta de un almacén que no
	// participa en el envío.

	public Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(
		CancellationToken cancelacion = default) =>
		Task.FromResult<IReadOnlyList<IncidenciaEnviable>>([]);

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
