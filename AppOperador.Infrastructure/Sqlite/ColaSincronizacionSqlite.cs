using System.Globalization;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Cola de sincronización respaldada en SQLite.
/// </summary>
/// <remarks>
/// <b>Solo persiste.</b> Guarda la cola, los estados, el contador de intentos, el último
/// código de error y la bitácora de cada intento. <b>No decide nada</b>: quién se envía,
/// en qué orden se reintenta y qué se queda esperando lo resuelve
/// <c>SincronizarIncidencias</c>, en la capa de aplicación.
///
/// <b>La cola es de quien tiene la sesión abierta</b> (JTT-1390 CA 7). Los registros de
/// otros operadores siguen guardados y conservan su identidad, pero no se listan, no se
/// cuentan y no se envían con el token de alguien más.
/// </remarks>
public sealed class ColaSincronizacionSqlite : ISyncQueueService
{
	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly ISessionStore _sesiones;

	/// <remarks>
	/// Ya no recibe <c>IConnectivityService</c> ni <c>IAuditLog</c>: la compuerta de enlace y la
	/// bitácora del envío se movieron a <c>SincronizarIncidencias</c>, que es donde se decide.
	/// Una cola que consulta la red para poder guardar era la señal de que aquí vivía algo que
	/// no le tocaba.
	/// </remarks>
	public ColaSincronizacionSqlite(
		BaseDatosLocal baseDatos,
		IClock reloj,
		ISessionStore sesiones)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_sesiones = sesiones;
	}

	/// <summary>
	/// Operador de la sesión abierta, o <see langword="null"/> si no hay ninguna.
	/// </summary>
	/// <remarks>
	/// Sin sesión la cola se ve vacía. No es que se haya borrado: es que nadie tiene derecho
	/// a verla todavía.
	/// </remarks>
	private string? OperadorActual => _sesiones.Actual?.Operador;

	/// <inheritdoc />
	public async Task<IReadOnlyList<RegistroCola>> ObtenerRegistrosAsync(CancellationToken cancelacion = default)
	{
		var operador = OperadorActual;
		if (operador is null)
		{
			return [];
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var borrador = (int)EstadoSincronizacion.Borrador;

		// Los borradores no son parte de la cola: nunca se envían.
		var filas = await conexion.Table<IncidenciaLocal>()
			.Where(i => i.Estado != borrador && i.Operador == operador)
			.OrderByDescending(i => i.CreadoUtcTicks)
			.ToListAsync();

		return filas.Select(MapeoIncidencia.ARegistroCola).ToList();
	}

	/// <inheritdoc />
	public async Task<int> ContarPendientesAsync(CancellationToken cancelacion = default)
	{
		var operador = OperadorActual;
		if (operador is null)
		{
			return 0;
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var pendiente = (int)EstadoSincronizacion.Pendiente;
		var fallido = (int)EstadoSincronizacion.Fallido;

		// Fallido también cuenta: sigue esperando envío, solo que ya falló una vez.
		return await conexion.Table<IncidenciaLocal>()
			.Where(i => (i.Estado == pendiente || i.Estado == fallido) && i.Operador == operador)
			.CountAsync();
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<IncidenciaEnviable>> ObtenerEnviablesAsync(
		CancellationToken cancelacion = default)
	{
		var operador = OperadorActual;
		if (operador is null)
		{
			// Sin sesión no hay con qué autenticarse ante Jacob. Los pendientes esperan.
			return [];
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var pendiente = (int)EstadoSincronizacion.Pendiente;
		var fallido = (int)EstadoSincronizacion.Fallido;

		// Primero la prioridad y después la antigüedad: una incidencia crítica se envía antes
		// que una normal capturada antes que ella. Los borradores no aparecen porque su estado
		// no es ninguno de los dos, y lo ya sincronizado tampoco.
		//
		// Solo lo del operador de la sesión: mandar lo de otro con este token se lo atribuiría
		// a quien no lo capturó.
		var filas = await conexion.Table<IncidenciaLocal>()
			.Where(i => (i.Estado == pendiente || i.Estado == fallido) && i.Operador == operador)
			.OrderByDescending(i => i.Prioridad)
			.ThenBy(i => i.CreadoUtcTicks)
			.ToListAsync();

		return filas.Select(AEnviable).ToList();
	}

	/// <inheritdoc />
	public async Task<IncidenciaEnviable?> ObtenerEnviablePorClaveAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		var operador = OperadorActual;
		if (operador is null || string.IsNullOrWhiteSpace(claveLocal))
		{
			return null;
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var pendiente = (int)EstadoSincronizacion.Pendiente;
		var fallido = (int)EstadoSincronizacion.Fallido;

		// Mismos estados que la consulta de la cola: un borrador no se envía y lo sincronizado
		// no se reenvía. El filtro por operador vale aquí igual que allá.
		var fila = await conexion.Table<IncidenciaLocal>()
			.Where(i => i.ClaveLocal == claveLocal
				&& (i.Estado == pendiente || i.Estado == fallido)
				&& i.Operador == operador)
			.FirstOrDefaultAsync();

		return fila is null ? null : AEnviable(fila);
	}

	/// <inheritdoc />
	public async Task ActualizarEnvioAsync(
		ActualizacionEnvio actualizacion,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(actualizacion);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var fila = await conexion.FindAsync<IncidenciaLocal>(actualizacion.Uuid);
		if (fila is null)
		{
			return;
		}

		fila.Estado = (int)actualizacion.Estado;
		fila.Intentos = actualizacion.Intentos;
		fila.UltimoErrorCodigo = actualizacion.UltimoErrorCodigo;
		fila.ActualizadoUtcTicks = _reloj.UtcAhora.Ticks;

		// El folio solo se escribe cuando llega: un reintento fallido no puede borrar el que
		// ya se había confirmado.
		if (actualizacion.FolioCentral is not null)
		{
			fila.FolioCentral = actualizacion.FolioCentral;
		}

		await conexion.UpdateAsync(fila);
	}

	/// <inheritdoc />
	public async Task RegistrarIntentoAsync(
		string uuid,
		bool exito,
		string? codigo,
		string? mensaje,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		await conexion.InsertAsync(new IntentoSincronizacion
		{
			RegistroUuid = uuid,
			Clase = (int)ClaseRegistro.Incidencia,
			InstanteUtcTicks = _reloj.UtcAhora.Ticks,
			Exito = exito,
			CodigoTexto = codigo,
			Mensaje = mensaje,
		});
	}

	/// <summary>
	/// Traduce una fila al modelo con el que trabaja la orquestación.
	/// </summary>
	/// <remarks>
	/// El tipo se guardó como texto invariante; las filas anteriores a JTT-1394 traen claves de
	/// la maqueta que no son enteros y salen sin tipo. <b>No son enviables</b>: apuntan a tipos
	/// que no existen en ningún servidor.
	/// </remarks>
	private static IncidenciaEnviable AEnviable(IncidenciaLocal fila) => new(
		fila.Uuid,
		fila.ClaveLocal,
		int.TryParse(fila.TipoClave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tipoId)
			? tipoId
			: null,
		Guid.TryParse(fila.SeveridadId, out var severidadId) ? severidadId : null,
		fila.Kilometro,
		(KilometerSource)fila.FuenteKilometro,
		fila.Nota,
		new DateTime(fila.CreadoUtcTicks, DateTimeKind.Utc),
		fila.SesionOrigen,
		(EstadoSincronizacion)fila.Estado,
		fila.Intentos,
		new DateTime(fila.ActualizadoUtcTicks, DateTimeKind.Utc),
		fila.UltimoErrorCodigo);
}
