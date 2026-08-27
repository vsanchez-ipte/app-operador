using System.Globalization;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite.Entidades;

namespace AppOperador.Infrastructure.Sqlite;

/// <summary>
/// Copia local de los catálogos de Jacob, sobre SQLite (JTT-1394 CA 2).
/// </summary>
/// <remarks>
/// Sustituye a la siembra de seis tipos de la maqueta que la base traía hasta JTT-1394. Lo que
/// hay aquí siempre viene del servidor: si nunca se ha descargado, el catálogo está vacío y el
/// formulario lo dice, que es más honesto que ofrecer tipos que Jacob rechazaría.
/// </remarks>
public sealed class RepositorioCatalogoSqlite : ICatalogoRepository
{
	/// <summary>Formato con el que se guarda la versión, invariante de la cultura.</summary>
	private const string FormatoVersion = "yyyy-MM-dd";

	/// <summary>Separador de la lista de formatos de evidencia. No aparece en un tipo MIME.</summary>
	private const char SeparadorFormatos = ',';

	private readonly BaseDatosLocal _baseDatos;

	public RepositorioCatalogoSqlite(BaseDatosLocal baseDatos)
	{
		_baseDatos = baseDatos;
	}

	/// <inheritdoc />
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

	/// <inheritdoc />
	public async Task ReemplazarAsync(
		CatalogosOperacion catalogos,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(catalogos);

		var conexion = await _baseDatos.ObtenerConexionListaAsync(cancelacion);

		// Borrar y volver a insertar, no fusionar: un valor retirado del catálogo tiene que
		// desaparecer de la lista. Va en una transacción para que un corte a media escritura no
		// deje al operador con media lista, que sería peor que con la anterior completa.
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

	/// <summary>
	/// Convierte una fila de severidad, o <see langword="null"/> si su identificador no se puede
	/// leer.
	/// </summary>
	/// <remarks>
	/// El <c>Guid</c> se guarda como texto y el archivo local es editable por quien tenga el
	/// dispositivo con root. Una fila ilegible se descarta en vez de romper la carga del
	/// catálogo entero.
	/// </remarks>
	private static SeveridadIncidencia? LeerSeveridad(SeveridadLocal fila) =>
		Guid.TryParse(fila.Id, out var id)
			? new SeveridadIncidencia(id, fila.Nivel, fila.Orden, fila.Hexadecimal)
			: null;

	/// <summary>
	/// Lee los límites de evidencia guardados; incompletos equivalen a no tenerlos (JTT-1398).
	/// </summary>
	/// <remarks>
	/// Una base de antes de esta versión trae las tres columnas en su valor por omisión, así que
	/// el operador que actualice sin haber vuelto a descargar el catálogo <b>no podrá adjuntar
	/// hasta la primera conexión</b>. Es lo correcto: la alternativa es validar con números que
	/// la app se inventó.
	/// </remarks>
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

	/// <summary>Lee la versión guardada; una fecha ilegible equivale a no tener catálogo.</summary>
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
