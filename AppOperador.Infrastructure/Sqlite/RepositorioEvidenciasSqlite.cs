using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

// Solo lee y escribe filas: no valida, no copia archivos y no decide.
public sealed class RepositorioEvidenciasSqlite : IRepositorioEvidencias
{
	private readonly BaseDatosLocal _baseDatos;

	public RepositorioEvidenciasSqlite(BaseDatosLocal baseDatos)
	{
		_baseDatos = baseDatos;
	}

	public async Task AgregarAsync(EvidenciaAdjunta evidencia, CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(evidencia);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		await conexion.InsertAsync(new EvidenciaLocal
		{
			Uuid = evidencia.Uuid,
			IncidenciaUuid = evidencia.IncidenciaUuid,
			RutaArchivo = evidencia.RutaArchivo,
			NombreOriginal = evidencia.NombreOriginal,
			TipoMedio = evidencia.TipoMime,
			Bytes = evidencia.Bytes,
			Estado = (int)evidencia.Estado,
			CreadoUtcTicks = DateTime.UtcNow.Ticks,
			Intentos = 0,
		});
	}

	public async Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var filas = await conexion.Table<EvidenciaLocal>()
			.Where(e => e.IncidenciaUuid == incidenciaUuid)
			.OrderBy(e => e.CreadoUtcTicks)
			.ToListAsync();

		return [.. filas.Select(Convertir)];
	}

	public async Task<int> ContarDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		return await conexion.Table<EvidenciaLocal>()
			.Where(e => e.IncidenciaUuid == incidenciaUuid)
			.CountAsync();
	}

	public async Task<EvidenciaAdjunta?> ObtenerAsync(
		string uuid,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var fila = await conexion.FindAsync<EvidenciaLocal>(uuid);

		return fila is null ? null : Convertir(fila);
	}

	public async Task EliminarAsync(string uuid, CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.DeleteAsync<EvidenciaLocal>(uuid);
	}

	public async Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerPendientesDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var sincronizado = (int)EstadoSincronizacion.Sincronizado;

		// Lo fallido también cuenta: la subida es idempotente por contenido y reintentar no gasta cupo.
		var filas = await conexion.Table<EvidenciaLocal>()
			.Where(e => e.IncidenciaUuid == incidenciaUuid && e.Estado != sincronizado)
			.OrderBy(e => e.CreadoUtcTicks)
			.ToListAsync();

		return [.. filas.Select(Convertir)];
	}

	public async Task ActualizarEnvioAsync(
		string uuid,
		EstadoSincronizacion estado,
		string? codigoError,
		string? mensaje = null,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var fila = await conexion.FindAsync<EvidenciaLocal>(uuid);

		if (fila is null)
		{
			return;
		}

		fila.Estado = (int)estado;
		fila.Intentos++;
		fila.UltimoErrorCodigo = codigoError;

		// Cada actualización es un intento: la fila cuenta cuántos van y la bitácora guarda qué respondió Jacob.
		await conexion.RunInTransactionAsync(tx =>
		{
			tx.Update(fila);
			tx.Insert(new IntentoEvidencia
			{
				EvidenciaUuid = uuid,
				InstanteUtcTicks = DateTime.UtcNow.Ticks,
				Exito = estado == EstadoSincronizacion.Sincronizado,
				CodigoTexto = codigoError,
				Mensaje = mensaje,
			});
		});
	}

	public async Task<IReadOnlyList<EvidenciaPendiente>> ObtenerPendientesDelOperadorAsync(
		string operador,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(operador))
		{
			return [];
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		// Se filtra por el estado de la evidencia: una incidencia sincronizada puede tener una foto sin subir.
		var filas = await conexion.QueryAsync<FilaEvidenciaPendiente>(
			"SELECT e.uuid AS Uuid, e.nombre_original AS NombreOriginal, e.tipo_medio AS TipoMime, " +
			"e.bytes AS Bytes, e.estado AS Estado, i.clave_local AS ClaveLocalIncidencia, " +
			"e.ultimo_error_codigo AS UltimoErrorCodigo " +
			"FROM evidencia_local e " +
			"INNER JOIN incidencia_local i ON i.uuid = e.incidencia_uuid " +
			"WHERE i.operador = ? AND e.estado != ? " +
			"ORDER BY e.creado_utc_ticks",
			operador,
			(int)EstadoSincronizacion.Sincronizado);

		return filas
			.Select(f => new EvidenciaPendiente(
				f.Uuid, f.NombreOriginal, f.TipoMime, f.Bytes,
				(EstadoSincronizacion)f.Estado, f.ClaveLocalIncidencia, f.UltimoErrorCodigo))
			.ToList();
	}

	public async Task<IReadOnlyList<EvidenciaRezagada>> ObtenerRezagadasDelOperadorAsync(
		string operador,
		CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(operador))
		{
			return [];
		}

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		return await ConsultaEvidenciasRezagadas.EjecutarAsync(conexion, operador);
	}

	private sealed class FilaEvidenciaPendiente
	{
		public string Uuid { get; set; } = string.Empty;
		public string NombreOriginal { get; set; } = string.Empty;
		public string TipoMime { get; set; } = string.Empty;
		public long Bytes { get; set; }
		public int Estado { get; set; }
		public string ClaveLocalIncidencia { get; set; } = string.Empty;
		public string? UltimoErrorCodigo { get; set; }
	}

	private static EvidenciaAdjunta Convertir(EvidenciaLocal fila) => new(
		fila.Uuid,
		fila.IncidenciaUuid,
		fila.NombreOriginal,
		fila.TipoMedio,
		fila.Bytes,
		fila.RutaArchivo,
		(EstadoSincronizacion)fila.Estado,
		fila.UltimoErrorCodigo);
}
