using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

public class RepositorioCatalogoSqliteTests
{
	private static readonly Guid IdCritico = Guid.Parse("11111111-1111-1111-1111-111111111111");
	private static readonly Guid IdAdvertencia = Guid.Parse("22222222-2222-2222-2222-222222222222");

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

	// ── Los límites de evidencia ───────────────────────────────────────────

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
		await using var contexto = new ContextoSqlite();

		var limites = (await new RepositorioCatalogoSqlite(contexto.BaseDatos)
			.ObtenerAsync()).LimitesEvidencia;

		Assert.False(limites.EstanDefinidos);
		Assert.Empty(limites.FormatosPermitidos);
	}

	[Fact]
	public async Task UnCatalogoSinLimites_noPisaLosQueYaEstaban()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioCatalogoSqlite(contexto.BaseDatos);

		await repositorio.ReemplazarAsync(CatalogoCon(new DateOnly(2026, 8, 27), LimitesReales));
		await repositorio.ReemplazarAsync(
			CatalogoCon(new DateOnly(2026, 8, 28), LimitesEvidencia.Desconocidos));

		Assert.False((await repositorio.ObtenerAsync()).LimitesEvidencia.EstanDefinidos);
	}
}
