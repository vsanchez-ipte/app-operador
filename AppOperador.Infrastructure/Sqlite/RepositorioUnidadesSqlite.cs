using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

public sealed class RepositorioUnidadesSqlite : ICatalogoUnidadesRepository
{
	private readonly BaseDatosLocal _baseDatos;

	public RepositorioUnidadesSqlite(BaseDatosLocal baseDatos) => _baseDatos = baseDatos;

	public async Task GuardarAsync(
		IReadOnlyList<UnidadVehicular> unidades,
		DateTime sincronizadoUtc,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(unidades);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		await conexion.RunInTransactionAsync(tx =>
		{
			foreach (var unidad in unidades)
			{
				tx.Execute(
					"""
					INSERT INTO "unidad_local"
						("clave", "id", "descripcion", "estado_clave", "estado_nombre", "estado_color",
						 "disponible", "sincronizado_utc_ticks")
					VALUES (?, ?, ?, ?, ?, ?, ?, ?)
					ON CONFLICT("clave") DO UPDATE SET
						"id" = excluded."id",
						"descripcion" = excluded."descripcion",
						"estado_clave" = excluded."estado_clave",
						"estado_nombre" = excluded."estado_nombre",
						"estado_color" = excluded."estado_color",
						"disponible" = excluded."disponible",
						"sincronizado_utc_ticks" = excluded."sincronizado_utc_ticks";
					""",
					unidad.Clave,
					unidad.Id,
					unidad.Descripcion,
					unidad.Estado?.Clave,
					unidad.Estado?.Nombre,
					unidad.Estado?.Color,
					unidad.Disponible,
					sincronizadoUtc.Ticks);
			}
		});
	}

	public async Task<CatalogoUnidades> ObtenerAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var filas = await conexion.QueryAsync<UnidadLocal>(
			"""
			SELECT * FROM "unidad_local"
			WHERE "sincronizado_utc_ticks" > 0
			  AND "sincronizado_utc_ticks" = (SELECT MAX("sincronizado_utc_ticks") FROM "unidad_local")
			ORDER BY "clave";
			""");

		if (filas.Count == 0)
		{
			return new CatalogoUnidades([], null);
		}

		var sincronizado = new DateTime(filas[0].SincronizadoUtcTicks, DateTimeKind.Utc);

		return new CatalogoUnidades([.. filas.Select(Convertir)], sincronizado);
	}

	private static UnidadVehicular Convertir(UnidadLocal fila) =>
		new(
			fila.Id,
			fila.Clave,
			fila.Descripcion,
			fila.Disponible,
			string.IsNullOrWhiteSpace(fila.EstadoClave) || string.IsNullOrWhiteSpace(fila.EstadoNombre)
				? null
				: new EstadoUnidad(fila.EstadoClave, fila.EstadoNombre, fila.EstadoColor ?? string.Empty));
}
