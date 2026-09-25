using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using SQLite;

namespace AppOperador.Infrastructure.Sqlite;

internal static class ConsultaEvidenciasRezagadas
{
	public static async Task<IReadOnlyList<EvidenciaRezagada>> EjecutarAsync(
		SQLiteAsyncConnection conexion,
		string operador)
	{
		var sincronizado = (int)EstadoSincronizacion.Sincronizado;

		var filas = await conexion.QueryAsync<Fila>(
			"SELECT e.uuid AS Uuid, e.incidencia_uuid AS IncidenciaUuid, " +
			"e.nombre_original AS NombreOriginal, e.tipo_medio AS TipoMime, e.bytes AS Bytes, " +
			"e.ruta_archivo AS RutaArchivo, e.estado AS Estado, " +
			"e.ultimo_error_codigo AS UltimoErrorCodigo, e.intentos AS Intentos, " +
			"(SELECT MAX(t.instante_utc_ticks) FROM intento_evidencia t " +
			" WHERE t.evidencia_uuid = e.uuid) AS UltimoIntentoTicks " +
			"FROM evidencia_local e " +
			"INNER JOIN incidencia_local i ON i.uuid = e.incidencia_uuid " +
			"WHERE i.operador = ? AND i.estado = ? AND e.estado != ? " +
			"ORDER BY e.creado_utc_ticks",
			operador,
			sincronizado,
			sincronizado);

		return filas.Select(ARezagada).ToList();
	}

	private static EvidenciaRezagada ARezagada(Fila f) => new(
		new EvidenciaAdjunta(
			f.Uuid, f.IncidenciaUuid, f.NombreOriginal, f.TipoMime, f.Bytes,
			f.RutaArchivo, (EstadoSincronizacion)f.Estado, f.UltimoErrorCodigo),
		f.Intentos,
		f.UltimoIntentoTicks is { } ticks ? new DateTime(ticks, DateTimeKind.Utc) : null);

	private sealed class Fila
	{
		public string Uuid { get; set; } = string.Empty;
		public string IncidenciaUuid { get; set; } = string.Empty;
		public string NombreOriginal { get; set; } = string.Empty;
		public string TipoMime { get; set; } = string.Empty;
		public long Bytes { get; set; }
		public string RutaArchivo { get; set; } = string.Empty;
		public int Estado { get; set; }
		public string? UltimoErrorCodigo { get; set; }
		public int Intentos { get; set; }
		public long? UltimoIntentoTicks { get; set; }
	}
}
