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

	private static CatalogosOperacion Catalogo(DateOnly version, params TipoIncidencia[] tipos) =>
		new(
			version,
			tipos.Length > 0 ? tipos : [new TipoIncidencia(11, "Choque por alcance")],
			[
				new SeveridadIncidencia(IdCritico, "Crítico", 1, "#EB1409"),
				new SeveridadIncidencia(IdAdvertencia, "Advertencia", 2, "#EDD611"),
			],
			[new AfectacionIncidencia(1, "Total")],
			[new CuerpoVia("A", "Cuerpo A")]);

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
}
