using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite;
using SQLite;

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

		// Las cuatro pestañas la invocan sin coordinarse: repetir no debe romper nada.
		await contexto.BaseDatos.InicializarAsync();
		await contexto.BaseDatos.InicializarAsync();
		await contexto.BaseDatos.InicializarAsync();

		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await contexto.BaseDatos.ObtenerVersionEsquemaAsync());
	}

	[Fact]
	public async Task Inicializar_soportaLlamadasConcurrentes()
	{
		await using var contexto = new ContextoSqlite();

		// Reproduce el arranque real: varias pantallas cargando a la vez.
		await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => contexto.BaseDatos.InicializarAsync()));

		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await contexto.BaseDatos.ObtenerVersionEsquemaAsync());
	}

	/// <summary>
	/// JTT-1394: la base ya no siembra catálogo. Nace vacía y la llena la primera descarga.
	/// </summary>
	/// <remarks>
	/// Hasta JTT-1394 se sembraban seis tipos de la maqueta —OBJETO, VEHICULO…— que no existen
	/// en ningún servidor. Ofrecerlos dejaba capturar incidencias que Jacob iba a rechazar, y
	/// además hacía creer que el catálogo estaba resuelto.
	/// </remarks>
	[Fact]
	public async Task BaseNueva_naceSinCatalogo()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		var catalogo = await new RepositorioCatalogoSqlite(contexto.BaseDatos).ObtenerAsync();

		Assert.Empty(catalogo.Tipos);
		Assert.Empty(catalogo.Severidades);
		Assert.False(catalogo.EsUtilizable);
	}

	[Fact]
	public async Task ReabrirBaseExistente_conservaLaVersion()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		// Como si el operador cerrara la app y volviera a abrirla.
		var segunda = contexto.ReabrirBaseDatos();
		await segunda.InicializarAsync();

		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await segunda.ObtenerVersionEsquemaAsync());

		await segunda.DisposeAsync();
	}

	/// <summary>
	/// El catálogo descargado sobrevive a cerrar y reabrir la app (JTT-1394 CA 2).
	/// </summary>
	[Fact]
	public async Task CatalogoGuardado_sobreviveAReabrirLaBase()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();

		await new RepositorioCatalogoSqlite(contexto.BaseDatos).ReemplazarAsync(
			new CatalogosOperacion(
				new DateOnly(2026, 8, 20),
				[new TipoIncidencia(107, "Otro", ExigeDescripcion: true)],
				[new SeveridadIncidencia(Guid.NewGuid(), "Crítico", 1, "#EB1409")],
				[new AfectacionIncidencia(1, "Total")],
				[new CuerpoVia("A", "Cuerpo A")]));

		var segunda = contexto.ReabrirBaseDatos();
		await segunda.InicializarAsync();

		var catalogo = await new RepositorioCatalogoSqlite(segunda).ObtenerAsync();

		Assert.Equal(new DateOnly(2026, 8, 20), catalogo.Version);

		// La nota obligatoria se reconoce por la bandera, nunca por el nombre ni por el id:
		// el de «Otro» ni siquiera es el mismo en todos los ambientes (JTT-1397).
		var otro = Assert.Single(catalogo.Tipos, t => t.ExigeDescripcion);
		Assert.Equal(107, otro.Id);

		await segunda.DisposeAsync();
	}

	[Fact]
	public async Task MigrarDesdeVersionCinco_conservaElCatalogoExistente()
	{
		var ruta = Path.Combine(Path.GetTempPath(), $"appoperador-migracion-{Guid.NewGuid():N}.db3");
		try
		{
			var anterior = new BaseDatosLocal(ruta);
			await anterior.InicializarAsync();
			await new RepositorioCatalogoSqlite(anterior).ReemplazarAsync(
				new CatalogosOperacion(
					new DateOnly(2026, 8, 20),
					[new TipoIncidencia(107, "Otro", ExigeDescripcion: true)],
					[new SeveridadIncidencia(Guid.NewGuid(), "Crítico", 1, "#EB1409")],
					[new AfectacionIncidencia(1, "Total")],
					[new CuerpoVia("A", "Cuerpo A")]));
			await anterior.DisposeAsync();

			using (var directa = new SQLiteConnection(ruta))
			{
				directa.Execute("PRAGMA user_version = 5;");
			}

			var actualizada = new BaseDatosLocal(ruta);
			await actualizada.InicializarAsync();
			var catalogo = await new RepositorioCatalogoSqlite(actualizada).ObtenerAsync();

			Assert.Single(catalogo.Tipos);
			Assert.Equal(107, catalogo.Tipos[0].Id);
			Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await actualizada.ObtenerVersionEsquemaAsync());
			await actualizada.DisposeAsync();
		}
		finally
		{
			if (File.Exists(ruta))
			{
				File.Delete(ruta);
			}
		}
	}
}
