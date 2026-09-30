using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

// Se ordena por id, no por hora: el reloj se puede mover y el monotónico se reinicia.
public sealed class BitacoraAuditoriaSqlite : IAuditLog
{
	// De todos los operadores: unos 300 KB, varias semanas de turno.
	public const int EventosConservados = 1000;

	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly IMonotonicClock _monotonico;
	private readonly ISessionStore _sesiones;
	private readonly IConnectivityService _conectividad;

	public BitacoraAuditoriaSqlite(
		BaseDatosLocal baseDatos,
		IClock reloj,
		IMonotonicClock monotonico,
		ISessionStore sesiones,
		IConnectivityService conectividad)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_monotonico = monotonico;
		_sesiones = sesiones;
		_conectividad = conectividad;
	}

	public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default) =>
		ObtenerEventosAsync(0, EventosConservados, cancelacion);

	public async Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(
		int omitir, int cantidad, CancellationToken cancelacion = default)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(omitir);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);

		var operador = _sesiones.Actual?.Operador;
		if (string.IsNullOrWhiteSpace(operador))
		{
			return [];
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		// Sin operador ni origen = fila anterior al esquema 10. SQL a mano: la LINQ de sqlite-net traduce mal «== null» en un OR.
		var filas = await conexion.QueryAsync<EventoAuditoriaLocal>(
			"SELECT * FROM evento_auditoria " +
			"WHERE operador = ? OR (operador IS NULL AND origen IS NULL) " +
			"ORDER BY id DESC LIMIT ? OFFSET ?",
			operador, cantidad, omitir);

		return filas.Select(Convertir).ToList();
	}

	public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default) =>
		InsertarAsync(nivel, mensaje, OperacionAuditada.Otra, null, null, null, cancelacion);

	public Task RegistrarAsync(
		OperacionAuditada operacion,
		ResultadoAuditoria resultado,
		string mensaje,
		string? motivoCodigo = null,
		string? operador = null,
		CancellationToken cancelacion = default) =>
		InsertarAsync(
			resultado == ResultadoAuditoria.Rechazo ? NivelAuditoria.Advertencia : NivelAuditoria.Info,
			mensaje, operacion, resultado, motivoCodigo, operador, cancelacion);

	private async Task InsertarAsync(
		NivelAuditoria nivel,
		string mensaje,
		OperacionAuditada operacion,
		ResultadoAuditoria? resultado,
		string? motivoCodigo,
		string? operadorSinSesion,
		CancellationToken cancelacion)
	{
		var sesion = _sesiones.Actual;
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var sesionId = await ReferenciasSesion.AsegurarAsync(conexion, sesion);

		await conexion.InsertAsync(new EventoAuditoriaLocal
		{
			InstanteUtcTicks = _reloj.UtcAhora.Ticks,
			MonotonicoTicks = _monotonico.Transcurrido.Ticks,
			Nivel = (int)nivel,
			Mensaje = mensaje,
			Operacion = (int)operacion,
			Resultado = (int?)resultado,
			MotivoCodigo = motivoCodigo,
			Operador = sesion?.Operador ?? operadorSinSesion,
			Rol = sesion?.Rol,
			// Todos los permisos: cuál aplica depende de la operación.
			Permiso = sesion is null ? null : string.Join(",", sesion.Permisos),
			UnidadClave = sesion?.UnidadVehicular,
			SesionId = sesionId,
			Origen = (int)(_conectividad.HayEnlace ? OrigenAuditoria.Online : OrigenAuditoria.Offline),
		});

		await RecortarAsync(conexion);
	}

	public async Task AtribuirAsync(string alias, string operador, CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(operador) || alias == operador)
		{
			return;
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.ExecuteAsync(
			"UPDATE evento_auditoria SET operador = ? WHERE operador = ?", operador, alias);
	}

	// Se vuelve a sellar el Kind: sin él, el DateTime se compara como hora local.
	private static EventoAuditoria Convertir(EventoAuditoriaLocal fila) => new(
		new DateTime(fila.InstanteUtcTicks, DateTimeKind.Utc),
		(NivelAuditoria)fila.Nivel,
		fila.Mensaje,
		(OperacionAuditada)fila.Operacion,
		(ResultadoAuditoria?)fila.Resultado,
		fila.MotivoCodigo,
		fila.Operador,
		fila.Rol,
		fila.Permiso,
		fila.UnidadClave,
		fila.SesionId,
		(OrigenAuditoria)(fila.Origen ?? (int)OrigenAuditoria.Desconocido),
		fila.MonotonicoTicks);

	private static Task RecortarAsync(SQLite.SQLiteAsyncConnection conexion) =>
		conexion.ExecuteAsync(
			"""
			DELETE FROM evento_auditoria
			WHERE id NOT IN (
				SELECT id FROM evento_auditoria
				ORDER BY id DESC
				LIMIT ?
			);
			""",
			EventosConservados);
}
