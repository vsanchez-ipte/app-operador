using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Bitácora local en SQLite (JTT-1392).
/// </summary>
/// <remarks>
/// <para>
/// <b>Cada línea copia quién y desde dónde en el instante de escribirla</b>: operador, rol,
/// permiso, unidad y sesión salen de la sesión abierta; el origen, de si hay enlace con el CCO.
/// Se copian en la fila y no se referencian, porque la sesión local es una sola fila que se
/// sobrescribe con cada operador.
/// </para>
/// <para>
/// <b>Se ordena por el orden de inserción, no por la hora.</b> Mover el reloj del dispositivo
/// desordenaba el historial: entradas registradas después aparecían antes. El identificador
/// autoincremental es el orden real en que se escribió cada línea y no lo mueve ni el reloj ni
/// un reinicio —el contador monotónico sí se reinicia con el dispositivo, por eso no sirve para
/// ordenar y se guarda solo como sello, igual que en las incidencias—.
/// </para>
/// <para>
/// <b>Se lee lo del operador con sesión</b> más las líneas anteriores a la versión 10 del
/// esquema —sin operador y sin origen—, que se muestran como historial previo. Una línea nueva
/// que llegara sin operador no se le muestra a nadie: se distingue de las viejas porque sí
/// tiene origen. Sin sesión no se lee nada.
/// </para>
/// </remarks>
public sealed class BitacoraAuditoriaSqlite : IAuditLog
{
	public const int EventosMaximos = 200;

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

	/// <inheritdoc />
	public async Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default)
	{
		var operador = _sesiones.Actual?.Operador;
		if (string.IsNullOrWhiteSpace(operador))
		{
			return [];
		}

		// «operador IS NULL AND origen IS NULL» es lo que reconoce una fila anterior al esquema
		// 10: la migración deja las columnas nuevas en NULL, y una fila nueva sin operador sí
		// trae origen. En SQL explícito porque la LINQ de sqlite-net no traduce bien un
		// «== null» dentro de un OR.
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var filas = await conexion.QueryAsync<EventoAuditoriaLocal>(
			"SELECT * FROM evento_auditoria " +
			"WHERE operador = ? OR (operador IS NULL AND origen IS NULL) " +
			"ORDER BY id DESC LIMIT ?",
			operador, EventosMaximos);

		return filas.Select(Convertir).ToList();
	}

	/// <inheritdoc />
	public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default) =>
		InsertarAsync(nivel, mensaje, OperacionAuditada.Otra, null, null, null, cancelacion);

	/// <inheritdoc />
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

		await conexion.InsertAsync(new EventoAuditoriaLocal
		{
			InstanteUtcTicks = _reloj.UtcAhora.Ticks,
			MonotonicoTicks = _monotonico.Transcurrido.Ticks,
			Nivel = (int)nivel,
			Mensaje = mensaje,
			Operacion = (int)operacion,
			Resultado = (int?)resultado,
			MotivoCodigo = motivoCodigo,
			// Con sesión manda la sesión; sin ella —el acceso— vale lo que diga quien registra.
			Operador = sesion?.Operador ?? operadorSinSesion,
			Rol = sesion?.Rol,
			// Todos los permisos de la sesión, no uno elegido al azar: cuál aplica depende de la
			// operación, y guardar la lista completa no obliga a adivinarlo.
			Permiso = sesion is null ? null : string.Join(",", sesion.Permisos),
			UnidadClave = sesion?.UnidadVehicular,
			SesionId = sesion?.SessionId,
			Origen = (int)(_conectividad.HayEnlace ? OrigenAuditoria.Online : OrigenAuditoria.Offline),
		});

		await RecortarAsync(conexion);
	}

	/// <inheritdoc />
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

	private static EventoAuditoria Convertir(EventoAuditoriaLocal fila) => new(
		// Se guardó como ticks UTC: al reconstruir hay que volver a sellar el Kind, porque un
		// DateTime sin Kind se compara como hora local.
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

	/// <summary>
	/// Conserva las últimas <see cref="EventosMaximos"/> líneas del dispositivo, de todos los
	/// operadores. El tope es provisional; la política definitiva es JTT-292.
	/// </summary>
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
			EventosMaximos);
}
