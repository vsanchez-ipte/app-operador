using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Creación del archivo, esquema y migración local.
/// </summary>
public sealed class BaseDatosLocalTests
{
	[Fact]
	public async Task Inicializar_creaElArchivoYSellaLaVersionDeEsquema()
	{
		await using var contexto = new ContextoSqlite();

		await contexto.BaseDatos.InicializarAsync();

		Assert.True(File.Exists(contexto.Ruta), "La inicialización debe crear el archivo de base de datos.");
		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await contexto.BaseDatos.ObtenerVersionEsquemaAsync());
	}

	[Fact]
	public async Task Inicializar_esIdempotente()
	{
		await using var contexto = new ContextoSqlite();

		// Las cuatro pestañas la invocan sin coordinarse: repetir no debe romper nada
		// ni volver a sembrar el catálogo.
		await contexto.BaseDatos.InicializarAsync();
		await contexto.BaseDatos.InicializarAsync();
		await contexto.BaseDatos.InicializarAsync();

		var tipos = await contexto.CrearRepositorio().ObtenerTiposAsync();
		Assert.Equal(6, tipos.Count);
	}

	[Fact]
	public async Task Inicializar_soportaLlamadasConcurrentes()
	{
		await using var contexto = new ContextoSqlite();

		// Reproduce el arranque real: varias pantallas cargando a la vez.
		await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => contexto.BaseDatos.InicializarAsync()));

		var tipos = await contexto.CrearRepositorio().ObtenerTiposAsync();
		Assert.Equal(6, tipos.Count);
	}

	[Fact]
	public async Task ReabrirBaseExistente_conservaLaVersionYNoDuplicaElCatalogo()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		// Como si el operador cerrara la app y volviera a abrirla.
		var segunda = contexto.ReabrirBaseDatos();
		await segunda.InicializarAsync();

		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await segunda.ObtenerVersionEsquemaAsync());

		var tipos = await new RepositorioIncidenciasSqlite(segunda, contexto.Reloj, contexto.Sesion)
			.ObtenerTiposAsync();
		Assert.Equal(6, tipos.Count);

		await segunda.DisposeAsync();
	}

	[Fact]
	public async Task CatalogoSembrado_marcaOtroComoTipoQueExigeDescripcion()
	{
		await using var contexto = new ContextoSqlite();

		var tipos = await contexto.CrearRepositorio().ObtenerTiposAsync();

		// JTT-333: el tipo se reconoce por su bandera, no comparando el nombre por texto.
		var otro = Assert.Single(tipos, t => t.ExigeDescripcion);
		Assert.Equal("OTRO", otro.Clave);
	}
}
