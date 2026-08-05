using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Cola de sincronización respaldada en SQLite.
/// </summary>
/// <remarks>
/// <b>Lo que ya es real:</b> la cola, los estados, el orden por prioridad, el contador de
/// intentos y la bitácora de cada envío. Todo persiste y sobrevive al cierre de la app.
///
/// <b>Lo que todavía no:</b> el envío en sí. El endpoint móvil de Jacob llega con
/// JTT-1347. Hasta entonces <see cref="ConfirmarEnJacobAsync"/> simula la respuesta
/// central; es el único punto que hay que sustituir, y está aislado a propósito.
///
/// Las transiciones de estado no se escriben a mano: las autoriza
/// <see cref="ReglaTransicionSincronizacion"/>, que es la regla de dominio. Si un cambio
/// no es válido, se registra el intento y el registro se queda donde estaba.
/// </remarks>
public sealed class ColaSincronizacionSqlite : ISyncQueueService
{
	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly IConnectivityService _conectividad;
	private readonly IAuditLog _bitacora;

	public ColaSincronizacionSqlite(
		BaseDatosLocal baseDatos,
		IClock reloj,
		IConnectivityService conectividad,
		IAuditLog bitacora)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_conectividad = conectividad;
		_bitacora = bitacora;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var borrador = (int)EstadoSincronizacion.Borrador;

		// Los borradores no son parte de la cola: nunca se envían.
		var filas = await conexion.Table<IncidenciaLocal>()
			.Where(i => i.Estado != borrador)
			.OrderByDescending(i => i.CreadoUtcTicks)
			.ToListAsync();

		return filas.Select(MapeoIncidencia.ARegistroCola).ToList();
	}

	/// <inheritdoc />
	public async Task<int> ContarPendientesAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var pendiente = (int)EstadoSincronizacion.Pendiente;
		var fallido = (int)EstadoSincronizacion.Fallido;

		// Fallido también cuenta: sigue esperando envío, solo que ya falló una vez.
		return await conexion.Table<IncidenciaLocal>()
			.Where(i => i.Estado == pendiente || i.Estado == fallido)
			.CountAsync();
	}

	/// <inheritdoc />
	public async Task<int> SincronizarAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		if (!_conectividad.HayEnlace)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"Sync cancelado: sin conexion.",
				cancelacion);
			return 0;
		}

		var pendientes = await ObtenerEnviablesOrdenadosAsync(conexion);
		var confirmados = 0;

		foreach (var fila in pendientes)
		{
			cancelacion.ThrowIfCancellationRequested();

			if (!TransicionarA(fila, EstadoSincronizacion.Enviando))
			{
				continue;
			}

			fila.Intentos++;
			await GuardarAsync(conexion, fila);

			var (exito, folio, codigo, mensaje) = await ConfirmarEnJacobAsync(fila, cancelacion);

			var destino = exito ? EstadoSincronizacion.Sincronizado : EstadoSincronizacion.Fallido;
			if (TransicionarA(fila, destino))
			{
				fila.FolioCentral = exito ? folio : fila.FolioCentral;
				await GuardarAsync(conexion, fila);
			}

			await RegistrarIntentoAsync(conexion, fila, exito, codigo, mensaje);

			if (exito)
			{
				confirmados++;
			}
		}

		await _bitacora.RegistrarAsync(
			NivelAuditoria.Info,
			$"Sync intentado: {confirmados}/{pendientes.Count} registros creados en Incidencias.",
			cancelacion);

		return confirmados;
	}

	/// <summary>
	/// Registros que pueden enviarse, en el orden en que deben atenderse.
	/// </summary>
	/// <remarks>
	/// Primero la prioridad y después la antigüedad: una incidencia crítica se envía antes
	/// que una normal capturada antes que ella. <c>SyncPriority.Critica</c> vale más que
	/// <c>Normal</c>, de ahí el orden descendente.
	/// </remarks>
	private static async Task<List<IncidenciaLocal>> ObtenerEnviablesOrdenadosAsync(
		SQLite.SQLiteAsyncConnection conexion)
	{
		var pendiente = (int)EstadoSincronizacion.Pendiente;
		var fallido = (int)EstadoSincronizacion.Fallido;

		return await conexion.Table<IncidenciaLocal>()
			.Where(i => i.Estado == pendiente || i.Estado == fallido)
			.OrderByDescending(i => i.Prioridad)
			.ThenBy(i => i.CreadoUtcTicks)
			.ToListAsync();
	}

	/// <summary>
	/// Aplica una transición solo si la regla de dominio la autoriza.
	/// </summary>
	private bool TransicionarA(IncidenciaLocal fila, EstadoSincronizacion destino)
	{
		var origen = (EstadoSincronizacion)fila.Estado;

		if (!ReglaTransicionSincronizacion.EsTransicionValida(origen, destino))
		{
			return false;
		}

		fila.Estado = (int)destino;
		fila.ActualizadoUtcTicks = _reloj.UtcAhora.Ticks;
		return true;
	}

	private static Task GuardarAsync(SQLite.SQLiteAsyncConnection conexion, IncidenciaLocal fila) =>
		conexion.UpdateAsync(fila);

	private async Task RegistrarIntentoAsync(
		SQLite.SQLiteAsyncConnection conexion,
		IncidenciaLocal fila,
		bool exito,
		int? codigo,
		string? mensaje)
	{
		await conexion.InsertAsync(new IntentoSincronizacion
		{
			RegistroUuid = fila.Uuid,
			Clase = (int)ClaseRegistro.Incidencia,
			InstanteUtcTicks = _reloj.UtcAhora.Ticks,
			Exito = exito,
			Codigo = codigo,
			Mensaje = mensaje,
		});
	}

	/// <summary>
	/// Punto de sustitución para JTT-1347: el envío real al canal móvil de Jacob.
	/// </summary>
	/// <remarks>
	/// Hoy simula una confirmación inmediata y devuelve un folio <c>INC-####</c>. Cuando
	/// exista el endpoint, aquí entra el cliente HTTP enviando <c>fila.Uuid</c> como
	/// llave de idempotencia, y el resto de la clase no cambia.
	/// </remarks>
	private Task<(bool Exito, string Folio, int? Codigo, string? Mensaje)> ConfirmarEnJacobAsync(
		IncidenciaLocal fila,
		CancellationToken cancelacion)
	{
		_ = fila;
		_ = cancelacion;

		var folio = $"INC-{Random.Shared.Next(1000, 9999)}";
		return Task.FromResult<(bool, string, int?, string?)>((true, folio, 201, "Simulado: pendiente de JTT-1347."));
	}
}
