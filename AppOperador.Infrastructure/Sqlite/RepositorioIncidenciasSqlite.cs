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
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(severidad);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var ahora = _reloj.UtcAhora.Ticks;
		var versionCatalogo = await LeerVersionCatalogoAsync(cancelacion);
		var lecturaGps = fuenteKilometro == KilometerSource.GPS ? posicionGps : null;

		var fila = new IncidenciaLocal
		{
			Uuid = Guid.NewGuid().ToString(),
			ClaveLocal = await SiguienteClaveLocalAsync(cancelacion),
			TipoClave = tipo.Id.ToString(CultureInfo.InvariantCulture),
			TipoNombre = tipo.Nombre,
			Kilometro = kilometro.Valor,
			FuenteKilometro = (int)fuenteKilometro,
			KilometroMetros = kilometro.MetrosNormalizados,
			GpsLatitud = lecturaGps?.Latitud,
			GpsLongitud = lecturaGps?.Longitud,
			GpsPrecisionMetros = lecturaGps?.PrecisionMetros,
			GpsInstanteUtcTicks = lecturaGps?.InstanteUtc.Ticks,

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

	/// <inheritdoc />
	public async Task<BorradorIncidencia?> ObtenerBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		var fila = await BuscarBorradorPropioAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return null;
		}

		return new BorradorIncidencia(
			fila.ClaveLocal,
			// El tipo se guardó como texto invariante; si la fila es anterior a JTT-1394 trae
			// una clave de maqueta —OBJETO, VEHICULO…— que no es un entero. En ese caso se
			// devuelve sin tipo: es más honesto que reabrir el formulario con uno inventado.
			int.TryParse(fila.TipoClave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tipoId)
				? tipoId
				: null,
			fila.Kilometro,
			Guid.TryParse(fila.SeveridadId, out var severidadId) ? severidadId : null,
			fila.Nota);
	}

	/// <inheritdoc />
	public async Task<bool> ActualizarBorradorAsync(
		string claveLocal,
		TipoIncidencia? tipo,
		string? kilometro,
		SeveridadIncidencia? severidad,
		string nota,
		CancellationToken cancelacion = default)
	{
		var fila = await BuscarBorradorPropioAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return false;
		}

		fila.TipoClave = tipo?.Id.ToString(CultureInfo.InvariantCulture);
		fila.TipoNombre = tipo?.Nombre;
		fila.Kilometro = kilometro;
		fila.SeveridadId = severidad?.Id.ToString() ?? string.Empty;
		fila.SeveridadNombre = severidad?.Nivel ?? string.Empty;
		fila.SeveridadOrden = severidad?.Orden ?? 0;
		fila.Prioridad = (int)ReglaPrioridadSincronizacion.Para(severidad?.Orden ?? int.MaxValue);
		fila.Nota = nota;
		fila.ActualizadoUtcTicks = _reloj.UtcAhora.Ticks;

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.UpdateAsync(fila);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> EliminarBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		var fila = await BuscarBorradorPropioAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return false;
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.DeleteAsync(fila);
		return true;
	}

	/// <inheritdoc />
	public async Task<bool> ConvertirBorradorAsync(
		string claveLocal,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps = null,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(tipo);
		ArgumentNullException.ThrowIfNull(severidad);

		var fila = await BuscarBorradorPropioAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return false;
		}

		fila.TipoClave = tipo.Id.ToString(CultureInfo.InvariantCulture);
		fila.TipoNombre = tipo.Nombre;
		fila.Kilometro = kilometro.Valor;
		fila.FuenteKilometro = (int)fuenteKilometro;
		fila.KilometroMetros = kilometro.MetrosNormalizados;
		var lecturaGps = fuenteKilometro == KilometerSource.GPS ? posicionGps : null;
		fila.GpsLatitud = lecturaGps?.Latitud;
		fila.GpsLongitud = lecturaGps?.Longitud;
		fila.GpsPrecisionMetros = lecturaGps?.PrecisionMetros;
		fila.GpsInstanteUtcTicks = lecturaGps?.InstanteUtc.Ticks;
		fila.SeveridadId = severidad.Id.ToString();
		fila.SeveridadNombre = severidad.Nivel;
		fila.SeveridadOrden = severidad.Orden;
		fila.Prioridad = (int)ReglaPrioridadSincronizacion.Para(severidad.Orden);
		fila.Nota = nota;
		fila.Estado = (int)EstadoSincronizacion.Pendiente;
		fila.ActualizadoUtcTicks = _reloj.UtcAhora.Ticks;

		// La versión del catálogo se vuelve a sellar aquí, y no se conserva la del borrador.
		// La que cuenta para JTT-1394 CA 5 es la que estaba vigente cuando se eligieron el tipo
		// y la severidad definitivos, que es ahora: un borrador todavía no es una incidencia.
		fila.VersionCatalogo = await LeerVersionCatalogoAsync(cancelacion);

		// El permiso también: un borrador puede llevar días guardado y el permiso con el que
		// hoy se confirma no tiene por qué ser el de entonces (JTT-1385 CA 7).
		fila.PermisoOrigen = PermisoDeLaSesion();

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.UpdateAsync(fila);
		return true;
	}

	/// <summary>
	/// Busca un borrador por su clave, exigiendo que sea del operador de la sesión.
	/// </summary>
	/// <remarks>
	/// <b>El filtro por operador no es una comodidad, es la regla</b> (JTT-1388 CA 9). Sin él,
	/// quien entre después podría abrir, editar, convertir o borrar el trabajo a medio capturar
	/// del turno anterior — y al convertirlo quedaría a nombre de quien no lo escribió.
	/// Sin sesión no se devuelve nada.
	/// </remarks>
	private async Task<IncidenciaLocal?> BuscarBorradorPropioAsync(
		string claveLocal,
		CancellationToken cancelacion)
	{
		var operador = _sesion.Actual?.Operador;
		if (operador is null || string.IsNullOrWhiteSpace(claveLocal))
		{
			return null;
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var borrador = (int)EstadoSincronizacion.Borrador;

		return await conexion.Table<IncidenciaLocal>()
			.Where(i => i.ClaveLocal == claveLocal
				&& i.Estado == borrador
				&& i.Operador == operador)
			.FirstOrDefaultAsync();
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
