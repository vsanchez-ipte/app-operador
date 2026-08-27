using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Las evidencias de cada incidencia, sobre SQLite (JTT-1398).
/// </summary>
/// <remarks>
/// La tabla existía desde el primer esquema y nadie escribía en ella: este es el primero que lo
/// hace. Solo lee y escribe filas —no valida, no copia archivos y no decide nada—, igual que
/// <see cref="ColaSincronizacionSqlite"/> quedó después del refactor de JTT-1401.
/// </remarks>
public sealed class RepositorioEvidenciasSqlite : IRepositorioEvidencias
{
	private readonly BaseDatosLocal _baseDatos;

	public RepositorioEvidenciasSqlite(BaseDatosLocal baseDatos)
	{
		_baseDatos = baseDatos;
	}

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <inheritdoc />
	public async Task<int> ContarDeIncidenciaAsync(
		string incidenciaUuid,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		return await conexion.Table<EvidenciaLocal>()
			.Where(e => e.IncidenciaUuid == incidenciaUuid)
			.CountAsync();
	}

	/// <inheritdoc />
	public async Task<EvidenciaAdjunta?> ObtenerAsync(
		string uuid,
		CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		var fila = await conexion.FindAsync<EvidenciaLocal>(uuid);

		return fila is null ? null : Convertir(fila);
	}

	/// <inheritdoc />
	public async Task EliminarAsync(string uuid, CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);
		await conexion.DeleteAsync<EvidenciaLocal>(uuid);
	}

	private static EvidenciaAdjunta Convertir(EvidenciaLocal fila) => new(
		fila.Uuid,
		fila.IncidenciaUuid,
		fila.NombreOriginal,
		fila.TipoMedio,
		fila.Bytes,
		fila.RutaArchivo,
		(EstadoSincronizacion)fila.Estado);
}
