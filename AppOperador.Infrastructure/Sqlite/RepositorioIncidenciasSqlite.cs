using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Persistencia de incidencias sobre SQLite.
/// </summary>
/// <remarks>
/// Sustituye al simulador en memoria: lo guardado aquí sobrevive al cierre de la app.
/// Guardar nunca consulta la red, conforme al principio "offline primero".
/// </remarks>
public sealed class RepositorioIncidenciasSqlite : IIncidentRepository
{
	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly ISessionStore _sesion;

	public RepositorioIncidenciasSqlite(BaseDatosLocal baseDatos, IClock reloj, ISessionStore sesion)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_sesion = sesion;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<TipoIncidencia>> ObtenerTiposAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var filas = await conexion.Table<TipoIncidenciaLocal>()
			.Where(t => t.Activo)
			.OrderBy(t => t.Orden)
			.ToListAsync();

		return filas
			.Select(t => new TipoIncidencia(t.Clave, t.Nombre, t.ExigeDescripcion))
			.ToList();
	}

	/// <inheritdoc />
	public async Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		Gravedad gravedad,
		string nota,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var ahora = _reloj.UtcAhora.Ticks;

		var fila = new IncidenciaLocal
		{
			Uuid = Guid.NewGuid().ToString(),
			ClaveLocal = await SiguienteClaveLocalAsync(cancelacion),
			TipoClave = tipo.Clave,
			TipoNombre = tipo.Nombre,
			Kilometro = kilometro.Valor,
			FuenteKilometro = (int)fuenteKilometro,
			Gravedad = (int)gravedad,
			// La prioridad no se decide aquí: la fija la regla de dominio.
			Prioridad = (int)ReglaPrioridadSincronizacion.Para(gravedad),
			Nota = nota,
			Estado = (int)EstadoSincronizacion.Pendiente,
			Operador = _sesion.Actual?.Operador ?? string.Empty,
			UnidadVehicular = _sesion.Actual?.UnidadVehicular ?? string.Empty,
			CreadoUtcTicks = ahora,
			ActualizadoUtcTicks = ahora,
		};

		await conexion.InsertAsync(fila);
		return fila.ClaveLocal;
	}

	/// <inheritdoc />
	public async Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		Gravedad gravedad,
		string nota,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var ahora = _reloj.UtcAhora.Ticks;

		var fila = new IncidenciaLocal
		{
			Uuid = Guid.NewGuid().ToString(),
			ClaveLocal = await SiguienteClaveLocalAsync(cancelacion),
			TipoClave = tipo?.Clave,
			TipoNombre = tipo?.Nombre,
			// Un borrador admite un kilómetro a medio escribir: por eso se guarda el
			// texto crudo y no un Kilometer, que rechazaría cualquier valor incompleto.
			Kilometro = kilometro,
			FuenteKilometro = (int)KilometerSource.Manual,
			Gravedad = (int)gravedad,
			Prioridad = (int)ReglaPrioridadSincronizacion.Para(gravedad),
			Nota = nota,
			Estado = (int)EstadoSincronizacion.Borrador,
			Operador = _sesion.Actual?.Operador ?? string.Empty,
			UnidadVehicular = _sesion.Actual?.UnidadVehicular ?? string.Empty,
			CreadoUtcTicks = ahora,
			ActualizadoUtcTicks = ahora,
		};

		await conexion.InsertAsync(fila);
		return fila.ClaveLocal;
	}

	/// <inheritdoc />
	public async Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var borrador = (int)EstadoSincronizacion.Borrador;

		var filas = await conexion.Table<IncidenciaLocal>()
			.Where(i => i.Estado == borrador)
			.OrderByDescending(i => i.CreadoUtcTicks)
			.ToListAsync();

		return filas.Select(MapeoIncidencia.ARegistroCola).ToList();
	}

	/// <summary>
	/// Calcula la siguiente clave <c>LOC-######</c> a partir de la última guardada.
	/// </summary>
	/// <remarks>
	/// El consecutivo se deriva de la base y no de un contador en memoria: si se reiniciara
	/// en cada arranque, dos incidencias de sesiones distintas compartirían clave.
	/// </remarks>
	private async Task<string> SiguienteClaveLocalAsync(CancellationToken cancelacion)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var ultima = await conexion.ExecuteScalarAsync<string?>(
			"SELECT clave_local FROM incidencia_local ORDER BY clave_local DESC LIMIT 1;");

		var consecutivo = ultima is not null && int.TryParse(ultima.AsSpan(4), out var numero)
			? numero + 1
			: 673_527;

		return $"LOC-{consecutivo:D6}";
	}
}
