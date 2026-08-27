using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// JTT-1394 CA 2: la copia local del catálogo que sostiene la captura sin conexión.
/// </summary>
public class RepositorioCatalogoSqliteTests
{
	private static readonly Guid IdCritico = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid IdAdvertencia = Guid.Parse("22222222-2222-2222-2222-222222222222");

	/// <summary>Los que publica el servidor hoy, para las pruebas que los necesitan.</summary>
	private static readonly LimitesEvidencia LimitesReales =
		new(["image/jpeg", "image/png", "image/bmp", "application/pdf"], 5, 3);

	private static CatalogosOperacion Catalogo(DateOnly version, params TipoIncidencia[] tipos) =>
		CatalogoCon(version, LimitesEvidencia.Desconocidos, tipos);

	private static CatalogosOperacion CatalogoCon(
		DateOnly version,
		LimitesEvidencia limites,
		params TipoIncidencia[] tipos) =>
		new(
			version,
			tipos.Length > 0 ? tipos : [new TipoIncidencia(11, "Choque por alcance")],
			[
				new SeveridadIncidencia(IdCritico, "Crítico", 1, "#EB1409"),
				new SeveridadIncidencia(IdAdvertencia, "Advertencia", 2, "#EDD611"),
			],
			[new AfectacionIncidencia(1, "Total")],
			[new CuerpoVia("A", "Cuerpo A")],
			limites);

	[Fact]
	public async Task SinDescargar_devuelveCatalogoVacio()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		var catalogo = await repositorio.ObtenerAsync();

		Assert.Empty(catalogo.Tipos);
		Assert.False(catalogo.EsUtilizable);
	}

	[Fact]
	public async Task Reemplazar_guardaLasCuatroListasYLaVersion()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(Catalogo(new DateOnly(2026, 8, 20)));
		var catalogo = await repositorio.ObtenerAsync();

		Assert.Equal(new DateOnly(2026, 8, 20), catalogo.Version);
		Assert.Single(catalogo.Tipos);
		Assert.Equal(2, catalogo.Severidades.Count);
		Assert.Single(catalogo.Afectaciones);
		Assert.Single(catalogo.Cuerpos);
		Assert.True(catalogo.EsUtilizable);
	}

	/// <summary>
	/// Un tipo retirado del catálogo tiene que desaparecer, no acumularse.
	/// </summary>
	/// <remarks>
	/// Es la razón por la que se reemplaza en vez de fusionar: fusionando, un tipo que Producto
	/// retire seguiría ofreciéndose en el desplegable para siempre.
	/// </remarks>
	[Fact]
	public async Task Reemplazar_retiraLoQueYaNoViene()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(Catalogo(
			new DateOnly(2026, 8, 20),
			new TipoIncidencia(11, "Choque por alcance"),
			new TipoIncidencia(39, "Caída de material")));

		await repositorio.ReemplazarAsync(Catalogo(
			new DateOnly(2026, 8, 21),
			new TipoIncidencia(11, "Choque por alcance")));

		var catalogo = await repositorio.ObtenerAsync();

		var tipo = Assert.Single(catalogo.Tipos);
		Assert.Equal(11, tipo.Id);
		Assert.Equal(new DateOnly(2026, 8, 21), catalogo.Version);
	}

	/// <summary>
	/// Las severidades se leen ordenadas: de ese orden sale la prioridad de sincronización.
	/// </summary>
	[Fact]
	public async Task Severidades_seLeenOrdenadasPorGravedad()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(Catalogo(new DateOnly(2026, 8, 20)));
		var catalogo = await repositorio.ObtenerAsync();

		Assert.Equal([1, 2], catalogo.Severidades.Select(s => s.Orden));
		Assert.Equal(IdCritico, catalogo.Severidades[0].Id);
	}

	/// <summary>
	/// La bandera de nota obligatoria sobrevive al viaje por SQLite (JTT-1397).
	/// </summary>
	[Fact]
	public async Task ExigeDescripcion_seConserva()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(Catalogo(
			new DateOnly(2026, 8, 20),
			new TipoIncidencia(11, "Choque por alcance"),
			new TipoIncidencia(107, "Otro", ExigeDescripcion: true)));

		var catalogo = await repositorio.ObtenerAsync();

		var otro = Assert.Single(catalogo.Tipos, t => t.ExigeDescripcion);
		Assert.Equal(107, otro.Id);
	}

	// ── Los límites de evidencia (JTT-1398) ───────────────────────────────────────────
	//
	// Viajan con el catálogo porque es lo que sostiene el CA 2 de JTT-1394: el formulario
	// sigue funcionando sin conexión, y validar un adjunto también.

	[Fact]
	public async Task LosLimitesDeEvidencia_sobrevivenAlViajePorSqlite()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(
			CatalogoCon(new DateOnly(2026, 8, 27), LimitesReales));

		var limites = (await repositorio.ObtenerAsync()).LimitesEvidencia;

		Assert.True(limites.EstanDefinidos);
		Assert.Equal(5, limites.TamanoMaximoMb);
		Assert.Equal(3, limites.MaximoArchivosPorIncidencia);
		Assert.Equal(4, limites.FormatosPermitidos.Count);
		Assert.Contains("application/pdf", limites.FormatosPermitidos);
	}

	[Fact]
	public async Task SinDescargar_losLimitesQuedanDesconocidos()
	{
		// Es el estado de una base recién creada, y también el de una que se actualizó desde
		// una versión anterior sin haber vuelto a conectarse. No se puede adjuntar hasta que
		// el servidor diga con qué números validar.
		await using var contexto = new ContextoSqlite();

		var limites = (await new RepositorioCatalogoSqlite(contexto.BaseDatos)
			.ObtenerAsync()).LimitesEvidencia;

		Assert.False(limites.EstanDefinidos);
		Assert.Empty(limites.FormatosPermitidos);
	}

	[Fact]
	public async Task UnCatalogoSinLimites_noPisaLosQueYaEstaban()
	{
		// Al revés de lo que parece: si una descarga nueva no los trae, se guarda «desconocido»
		// y la app deja de admitir adjuntos. Es lo correcto —el servidor manda— pero conviene
		// que esté fijado, porque la alternativa silenciosa sería conservar unos límites que el
		// servidor ya no declara.
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(CatalogoCon(new DateOnly(2026, 8, 27), LimitesReales));
		await repositorio.ReemplazarAsync(
			CatalogoCon(new DateOnly(2026, 8, 28), LimitesEvidencia.Desconocidos));

		Assert.False((await repositorio.ObtenerAsync()).LimitesEvidencia.EstanDefinidos);
	}
}
