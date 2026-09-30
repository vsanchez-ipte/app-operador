using System.Globalization;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

public sealed class RepositorioCatalogoSqlite : ICatalogoRepository
{
	private const string FormatoVersion = "yyyy-MM-dd";

	private const char SeparadorFormatos = ',';

	private readonly BaseDatosLocal _baseDatos;

	public RepositorioCatalogoSqlite(BaseDatosLocal baseDatos)
	{
		_baseDatos = baseDatos;
	}

	public async Task<CatalogosOperacion> ObtenerAsync(CancellationToken cancelacion = default)
	{
		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		var tipos = await conexion.Table<TipoIncidenciaLocal>().OrderBy(t => t.Orden).ToListAsync();
		var severidades = await conexion.Table<SeveridadLocal>().OrderBy(s => s.Orden).ToListAsync();
		var afectaciones = await conexion.Table<AfectacionLocal>().OrderBy(a => a.Id).ToListAsync();
		var cuerpos = await conexion.Table<CuerpoLocal>().OrderBy(c => c.Clave).ToListAsync();
		var meta = await conexion.FindAsync<CatalogoMetaLocal>(CatalogoMetaLocal.ClaveUnica);

		return new CatalogosOperacion(
			LeerVersion(meta?.Version),
			[.. tipos.Select(t => new TipoIncidencia(t.Id, t.Nombre, t.ExigeDescripcion))],
			[.. severidades.Select(LeerSeveridad).OfType<SeveridadIncidencia>()],
			[.. afectaciones.Select(a => new AfectacionIncidencia(a.Id, a.Nombre))],
			[.. cuerpos.Select(c => new CuerpoVia(c.Clave, c.Nombre))],
			LeerLimites(meta));
	}

	public async Task ReemplazarAsync(
		CatalogosOperacion catalogos,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(catalogos);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		// Reemplaza, no fusiona; en transacción para no dejar media lista.
		await conexion.RunInTransactionAsync(tx =>
		{
			tx.DeleteAll<TipoIncidenciaLocal>();
			tx.DeleteAll<SeveridadLocal>();
			tx.DeleteAll<AfectacionLocal>();
			tx.DeleteAll<CuerpoLocal>();

			var orden = 0;
			foreach (var tipo in catalogos.Tipos)
			{
				tx.Insert(new TipoIncidenciaLocal
				{
					Id = tipo.Id,
					Nombre = tipo.Nombre,
					ExigeDescripcion = tipo.ExigeDescripcion,
					Orden = orden++,
				});
			}

			foreach (var severidad in catalogos.Severidades)
			{
				tx.Insert(new SeveridadLocal
				{
					Id = severidad.Id.ToString(),
					Nivel = severidad.Nivel,
					Orden = severidad.Orden,
					Hexadecimal = severidad.Hexadecimal,
				});
			}

			foreach (var afectacion in catalogos.Afectaciones)
			{
				tx.Insert(new AfectacionLocal { Id = afectacion.Id, Nombre = afectacion.Nombre });
			}

			foreach (var cuerpo in catalogos.Cuerpos)
			{
				tx.Insert(new CuerpoLocal { Clave = cuerpo.Clave, Nombre = cuerpo.Nombre });
			}

			tx.InsertOrReplace(new CatalogoMetaLocal
			{
				Clave = CatalogoMetaLocal.ClaveUnica,
				Version = catalogos.Version.ToString(FormatoVersion, CultureInfo.InvariantCulture),
				EvidenciaFormatos = string.Join(
					SeparadorFormatos, catalogos.LimitesEvidencia.FormatosPermitidos),
				EvidenciaTamanoMaximoMb = catalogos.LimitesEvidencia.TamanoMaximoMb,
				EvidenciaMaximoArchivos = catalogos.LimitesEvidencia.MaximoArchivosPorIncidencia,
			});
		});
	}

	// Una fila ilegible se descarta en vez de romper todo el catálogo.
	private static SeveridadIncidencia? LeerSeveridad(SeveridadLocal fila) =>
		Guid.TryParse(fila.Id, out var id)
			? new SeveridadIncidencia(id, fila.Nivel, fila.Orden, fila.Hexadecimal)
			: null;

	// Incompletos equivalen a desconocidos: no se valida con números inventados.
	private static LimitesEvidencia LeerLimites(CatalogoMetaLocal? meta)
	{
		if (meta is null)
		{
			return LimitesEvidencia.Desconocidos;
		}

		var formatos = meta.EvidenciaFormatos
			.Split(SeparadorFormatos, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		var limites = new LimitesEvidencia(
			formatos,
			meta.EvidenciaTamanoMaximoMb,
			meta.EvidenciaMaximoArchivos);

		return limites.EstanDefinidos ? limites : LimitesEvidencia.Desconocidos;
	}

	private static DateOnly LeerVersion(string? guardada) =>
		DateOnly.TryParseExact(
			guardada ?? string.Empty,
			FormatoVersion,
			CultureInfo.InvariantCulture,
			DateTimeStyles.None,
			out var version)
			? version
			: default;
}
