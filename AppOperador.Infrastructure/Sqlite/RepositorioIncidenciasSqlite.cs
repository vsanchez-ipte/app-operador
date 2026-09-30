using System.Globalization;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

public sealed class RepositorioIncidenciasSqlite : IIncidentRepository
{
	private readonly BaseDatosLocal _baseDatos;
	private readonly IClock _reloj;
	private readonly ISessionStore _sesion;
	private readonly IMonotonicClock? _monotonico;

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
		var sesion = _sesion.Actual;
		var sesionId = await ReferenciasSesion.AsegurarAsync(conexion, sesion);

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

			// Nombre y orden del momento, para que el histórico no cambie si se edita el catálogo.
			SeveridadId = severidad.Id.ToString(),
			SeveridadNombre = severidad.Nivel,
			SeveridadOrden = severidad.Orden,

			Prioridad = (int)ReglaPrioridadSincronizacion.Para(severidad.Orden),

			VersionCatalogo = versionCatalogo,
			Nota = nota,
			Estado = (int)EstadoSincronizacion.Pendiente,
			Operador = sesion?.Operador,
			UnidadVehicular = sesion?.UnidadVehicular,
			CreadoUtcTicks = ahora,
			ActualizadoUtcTicks = ahora,

			// Sello que no se mueve con el reloj, para ordenar lo capturado.
			MonotonicoTicks = _monotonico?.Transcurrido.Ticks ?? 0,
			SesionOrigen = sesionId,

			// Al crear: una incidencia offline puede enviarse cuando el permiso ya cambió.
			PermisoOrigen = PermisoDeLaSesion(),
		};

		await conexion.InsertAsync(fila);
		return fila.ClaveLocal;
	}

	// De la tabla y no de la sesión: la sesión trae la versión de su propia descarga.
	private async Task<string> LeerVersionCatalogoAsync(CancellationToken cancelacion)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var meta = await conexion.FindAsync<CatalogoMetaLocal>(CatalogoMetaLocal.ClaveUnica);

		return meta?.Version ?? string.Empty;
	}

	// Hoy Jacob solo emite el permiso general; si llegan permisos finos, aquí se decide cuál sellar.
	private string PermisoDeLaSesion()
	{
		var permisos = _sesion.Actual?.Permisos;

		return permisos is not null && permisos.Contiene(ReglaCapacidades.PermisoAppOperadorMovil)
			? ReglaCapacidades.PermisoAppOperadorMovil
			: string.Empty;
	}

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
		var sesion = _sesion.Actual;
		var sesionId = await ReferenciasSesion.AsegurarAsync(conexion, sesion);

		var fila = new IncidenciaLocal
		{
			Uuid = Guid.NewGuid().ToString(),
			ClaveLocal = await SiguienteClaveLocalAsync(cancelacion),
			TipoClave = tipo?.Id.ToString(CultureInfo.InvariantCulture),
			TipoNombre = tipo?.Nombre,
			// Texto crudo: un borrador admite un kilómetro a medio escribir.
			Kilometro = kilometro,
			FuenteKilometro = (int)KilometerSource.Manual,

			SeveridadId = severidad?.Id.ToString() ?? string.Empty,
			SeveridadNombre = severidad?.Nivel ?? string.Empty,
			SeveridadOrden = severidad?.Orden ?? 0,
			Prioridad = (int)ReglaPrioridadSincronizacion.Para(
				severidad?.Orden ?? int.MaxValue),
			VersionCatalogo = versionCatalogo,
			Nota = nota,
			Estado = (int)EstadoSincronizacion.Borrador,
			Operador = sesion?.Operador,
			UnidadVehicular = sesion?.UnidadVehicular,
			CreadoUtcTicks = ahora,
			ActualizadoUtcTicks = ahora,
			SesionOrigen = sesionId,
		};

		await conexion.InsertAsync(fila);
		return fila.ClaveLocal;
	}

	// Solo los del operador de la sesión: un borrador sigue siendo de quien lo escribió.
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

		// Lambda y no grupo de métodos: con la sobrecarga indexada de Select deja de compilar.
		return filas.Select(fila => MapeoIncidencia.ARegistroCola(fila)).ToList();
	}

	public async Task<BorradorIncidencia?> ObtenerBorradorAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		var fila = await BuscarBorradorPropioAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return null;
		}

		// Una fila vieja puede traer una clave de maqueta que no es entero: se reabre sin tipo.
		return new BorradorIncidencia(
			fila.Uuid,
			fila.ClaveLocal,
			int.TryParse(fila.TipoClave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tipoId)
				? tipoId
				: null,
			fila.Kilometro,
			Guid.TryParse(fila.SeveridadId, out var severidadId) ? severidadId : null,
			fila.Nota);
	}

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

		await PasarAPendienteAsync(fila, tipo, kilometro, fuenteKilometro, severidad, nota, posicionGps, cancelacion);
		return true;
	}

	public async Task<IncidenciaRechazada?> ObtenerRechazadaAsync(
		string claveLocal,
		CancellationToken cancelacion = default)
	{
		var fila = await BuscarFallidaPropiaAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return null;
		}

		return new IncidenciaRechazada(
			fila.Uuid,
			fila.ClaveLocal,
			int.TryParse(fila.TipoClave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tipoId)
				? tipoId
				: null,
			fila.Kilometro,
			Guid.TryParse(fila.SeveridadId, out var severidadId) ? severidadId : null,
			fila.Nota,
			fila.UltimoErrorCodigo,
			await UltimoMensajeDeFalloAsync(fila.Uuid, cancelacion));
	}

	public async Task<bool> CorregirRechazadaAsync(
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

		var fila = await BuscarFallidaPropiaAsync(claveLocal, cancelacion);
		if (fila is null)
		{
			return false;
		}

		// Para el operador es un envío nuevo: se reinician intentos y último código.
		fila.Intentos = 0;
		fila.UltimoErrorCodigo = null;

		await PasarAPendienteAsync(fila, tipo, kilometro, fuenteKilometro, severidad, nota, posicionGps, cancelacion);
		return true;
	}

	// Catálogo, permiso y sesión se sellan con los vigentes ahora, que son los que cuentan.
	private async Task PasarAPendienteAsync(
		IncidenciaLocal fila,
		TipoIncidencia tipo,
		Kilometer kilometro,
		KilometerSource fuenteKilometro,
		SeveridadIncidencia severidad,
		string nota,
		PosicionDispositivo? posicionGps,
		CancellationToken cancelacion)
	{
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
		fila.VersionCatalogo = await LeerVersionCatalogoAsync(cancelacion);
		fila.PermisoOrigen = PermisoDeLaSesion();

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		fila.SesionOrigen = await ReferenciasSesion.AsegurarAsync(conexion, _sesion.Actual) ?? fila.SesionOrigen;
		await conexion.UpdateAsync(fila);
	}

	// Un rechazo del turno anterior no lo corrige quien entre después.
	private async Task<IncidenciaLocal?> BuscarFallidaPropiaAsync(
		string claveLocal,
		CancellationToken cancelacion)
	{
		var operador = _sesion.Actual?.Operador;
		if (operador is null || string.IsNullOrWhiteSpace(claveLocal))
		{
			return null;
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var fallido = (int)EstadoSincronizacion.Fallido;

		return await conexion.Table<IncidenciaLocal>()
			.Where(i => i.ClaveLocal == claveLocal
				&& i.Estado == fallido
				&& i.Operador == operador)
			.FirstOrDefaultAsync();
	}

	private async Task<string?> UltimoMensajeDeFalloAsync(string uuid, CancellationToken cancelacion)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var ultimo = await conexion.Table<IntentoIncidencia>()
			.Where(i => i.IncidenciaUuid == uuid && !i.Exito)
			.OrderByDescending(i => i.InstanteUtcTicks)
			.FirstOrDefaultAsync();

		return ultimo?.Mensaje;
	}

	// El filtro por operador es la regla: si no, otro turno podría convertirlo a su nombre.
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

	// Desde la base y no en memoria: si se reiniciara, dos incidencias compartirían clave.
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
