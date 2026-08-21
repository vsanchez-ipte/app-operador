using System.Globalization;
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
	private readonly IMonotonicClock? _monotonico;

	/// <param name="monotonico">
	/// Contador con el que se sella cada captura (JTT-1383 CA 12). Opcional para no obligar
	/// a las pruebas que solo miran la persistencia a proporcionarlo.
	/// </param>
	public RepositorioIncidenciasSqlite(
		BaseDatosLocal baseDatos,
		IClock reloj,
		ISessionStore sesion,
		IMonotonicClock? monotonico = null)
	{
		_baseDatos = baseDatos;
		_reloj = reloj;
		_sesion = sesion;
		_monotonico = monotonico;
	}

	/// <inheritdoc />
	public async Task<string> GuardarAsync(
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(severidad);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var ahora = _reloj.UtcAhora.Ticks;
		var versionCatalogo = await LeerVersionCatalogoAsync(cancelacion);

		var fila = new IncidenciaLocal
		{
			Uuid = Guid.NewGuid().ToString(),
			ClaveLocal = await SiguienteClaveLocalAsync(cancelacion),
			TipoClave = tipo.Id.ToString(CultureInfo.InvariantCulture),
			TipoNombre = tipo.Nombre,
			Kilometro = kilometro.Valor,
			FuenteKilometro = (int)fuenteKilometro,

			// Del nivel se guardan las tres cosas: el identificador para enviarlo, y el nombre
			// y el orden del momento para que el histórico no cambie si el catálogo se edita.
			SeveridadId = severidad.Id.ToString(),
			SeveridadNombre = severidad.Nivel,
			SeveridadOrden = severidad.Orden,

			// La prioridad no se decide aquí: la fija la regla de dominio, a partir del orden.
			Prioridad = (int)ReglaPrioridadSincronizacion.Para(severidad.Orden),

			// Versión del catálogo con la que se capturó (JTT-1394 CA 5).
			VersionCatalogo = versionCatalogo,
			Nota = nota,
			Estado = (int)EstadoSincronizacion.Pendiente,
			Operador = _sesion.Actual?.Operador ?? string.Empty,
			UnidadVehicular = _sesion.Actual?.UnidadVehicular ?? string.Empty,
			CreadoUtcTicks = ahora,
			ActualizadoUtcTicks = ahora,

			// Sello de origen (JTT-1383 CA 12): la fecha del dispositivo va arriba, la
			// monotónica aquí y la sesión de la que salió el registro. Con el reloj solo no
			// se podría ordenar lo capturado si alguien lo movió a media jornada.
			MonotonicoTicks = _monotonico?.Transcurrido.Ticks ?? 0,
			SesionOrigen = _sesion.Actual?.SessionId ?? string.Empty,

			// Con qué permiso se autorizó (JTT-1385 CA 7). Se sella al crear porque una
			// incidencia offline puede enviarse horas después, cuando el permiso ya cambió.
			PermisoOrigen = PermisoDeLaSesion(),
		};

		await conexion.InsertAsync(fila);
		return fila.ClaveLocal;
	}

	/// <summary>
	/// Versión del catálogo guardado, para sellarla en la incidencia (JTT-1394 CA 5).
	/// </summary>
	/// <remarks>
	/// Se lee de la tabla de catálogo y no de la sesión a propósito. La sesión trae la versión
	/// de <b>su</b> descarga, y una incidencia capturada sin conexión puede enviarse días
	/// después, cuando ya se bajó otra: sellarla desde la sesión declararía una versión que el
	/// operador no usó. Vacío si nunca se ha descargado el catálogo.
	/// </remarks>
	private async Task<string> LeerVersionCatalogoAsync(CancellationToken cancelacion)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var meta = await conexion.FindAsync<CatalogoMetaLocal>(CatalogoMetaLocal.ClaveUnica);

		return meta?.Version ?? string.Empty;
	}

	/// <summary>
	/// Permiso con el que la sesión autoriza capturar, para sellarlo en el registro.
	/// </summary>
	/// <remarks>
	/// Se toma el permiso funcional de la App Operador, que es el único que Jacob emite hoy. Si
	/// mañana concede capacidades finas, aquí es donde hay que decidir cuál se sella.
	/// </remarks>
	private string PermisoDeLaSesion()
	{
		var permisos = _sesion.Actual?.Permisos;

		return permisos is not null && permisos.Contiene(ReglaCapacidades.PermisoAppOperadorMovil)
			? ReglaCapacidades.PermisoAppOperadorMovil
			: string.Empty;
	}

	/// <inheritdoc />
	public async Task<string> GuardarBorradorAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var ahora = _reloj.UtcAhora.Ticks;
		var versionCatalogo = await LeerVersionCatalogoAsync(cancelacion);

		var fila = new IncidenciaLocal
		{
			Uuid = Guid.NewGuid().ToString(),
			ClaveLocal = await SiguienteClaveLocalAsync(cancelacion),
			TipoClave = tipo?.Id.ToString(CultureInfo.InvariantCulture),
			TipoNombre = tipo?.Nombre,
			// Un borrador admite un kilómetro a medio escribir: por eso se guarda el
			// texto crudo y no un Kilometer, que rechazaría cualquier valor incompleto.
			Kilometro = kilometro,
			FuenteKilometro = (int)KilometerSource.Manual,

			// Un borrador puede no tener severidad elegida todavía; se sella lo que haya.
			SeveridadId = severidad?.Id.ToString() ?? string.Empty,
			SeveridadNombre = severidad?.Nivel ?? string.Empty,
			SeveridadOrden = severidad?.Orden ?? 0,
			Prioridad = (int)ReglaPrioridadSincronizacion.Para(
				severidad?.Orden ?? int.MaxValue),
			VersionCatalogo = versionCatalogo,
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
	/// <remarks>
	/// <para>
	/// <b>Solo los del operador de la sesión</b> (JTT-1388 CA 9). Un borrador es trabajo a medio
	/// capturar y sigue siendo de quien lo escribió: al entrar otro operador no debe encontrarse
	/// con lo que dejó el anterior, ni verlo ni poder retomarlo como suyo.
	/// </para>
	/// <para>
	/// Sin sesión abierta no se devuelve nada, igual que en la cola: no es que se hayan borrado,
	/// es que todavía nadie tiene derecho a verlos.
	/// </para>
	/// </remarks>
	public async Task<IReadOnlyList<RegistroCola>> ObtenerBorradoresAsync(CancellationToken cancelacion = default)
	{
		var operador = _sesion.Actual?.Operador;
		if (operador is null)
		{
			return [];
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var borrador = (int)EstadoSincronizacion.Borrador;

		var filas = await conexion.Table<IncidenciaLocal>()
			.Where(i => i.Estado == borrador && i.Operador == operador)
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
