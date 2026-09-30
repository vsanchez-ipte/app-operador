using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Sqlite;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class CifradoBaseDatosTests
{
	private sealed class ClaveFija : IDatabaseKeyProvider
	{
		public Task<string> ObtenerAsync(CancellationToken cancelacion = default) =>
			Task.FromResult("clave-de-prueba-32-bytes-abcdefgh");
	}

	private static string RutaTemporal() =>
		Path.Combine(Path.GetTempPath(), $"appoperador-cifrado-{Guid.NewGuid():N}.db3");

	private static void SembrarTiposDePrueba(string ruta, string? clave)
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true, key: clave));

		conexion.Execute(
			"INSERT INTO catalogo_tipo_incidencia (id, nombre, exige_descripcion, orden) "
				+ "VALUES (11, 'Choque por alcance', 0, 0), (107, 'Otro', 1, 1);");
	}

	private static int ContarTipos(string ruta, string? clave)
	{
		using var conexion = new SQLiteConnection(
			new SQLiteConnectionString(ruta, storeDateTimeAsTicks: true, key: clave));

		return conexion.ExecuteScalar<int>("SELECT COUNT(*) FROM catalogo_tipo_incidencia;");
	}

	[Fact]
	public async Task Una_base_nueva_queda_cifrada()
	{
		var ruta = RutaTemporal();
		try
		{
			await using (var basedatos = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				await basedatos.InicializarAsync();
			}

			// Sin la clave no se puede leer: es lo que significa que esté cifrada.
			Assert.Throws<SQLiteException>(() =>
			{
				using var enClaro = new SQLiteConnection(ruta, SQLiteOpenFlags.ReadOnly);
				enClaro.ExecuteScalar<int>("PRAGMA user_version;");
			});
		}
		finally
		{
			File.Delete(ruta);
		}
	}

	[Fact]
	public async Task Una_base_cifrada_se_vuelve_a_abrir_con_la_misma_clave()
	{
		var ruta = RutaTemporal();
		try
		{
			await using (var primera = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				await primera.InicializarAsync();
			}

			await using var segunda = new BaseDatosLocal(ruta, new ClaveFija());
			Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await segunda.ObtenerVersionEsquemaAsync());
		}
		finally
		{
			File.Delete(ruta);
		}
	}

	[Fact]
	public async Task Una_base_en_claro_se_cifra_conservando_los_datos()
	{
		var ruta = RutaTemporal();
		try
		{
			// Una base como la que dejó la versión anterior: sin clave y con datos dentro.
			await using (var enClaro = new BaseDatosLocal(ruta))
			{
				await enClaro.InicializarAsync();
			}

			SembrarTiposDePrueba(ruta, clave: null);

			var antes = ContarTipos(ruta, clave: null);
			Assert.Equal(2, antes);

			// La app actualizada abre el mismo archivo, ahora con clave.
			await using (var cifrada = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				await cifrada.InicializarAsync();
				Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await cifrada.ObtenerVersionEsquemaAsync());
			}

			// Los datos siguen ahí, ahora tras la clave.
			var clave = await new ClaveFija().ObtenerAsync();
			Assert.Equal(antes, ContarTipos(ruta, clave));

			// Y ya no se deja leer sin la clave.
			Assert.Throws<SQLiteException>(() =>
			{
				using var sinClave = new SQLiteConnection(ruta, SQLiteOpenFlags.ReadOnly);
				sinClave.ExecuteScalar<int>("PRAGMA user_version;");
			});
		}
		finally
		{
			File.Delete(ruta);
			File.Delete(ruta + ".cifrando");
		}
	}

	[Fact]
	public async Task Una_migracion_interrumpida_se_repara_al_arrancar()
	{
		var ruta = RutaTemporal();
		var temporal = ruta + ".cifrando";
		try
		{
			await using (var basedatos = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				await basedatos.InicializarAsync();
			}

			SembrarTiposDePrueba(ruta, await new ClaveFija().ObtenerAsync());

			// El estado exacto que deja el corte: sin base en la ruta y con el cifrado al lado.
			File.Move(ruta, temporal);

			await using (var siguiente = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				await siguiente.InicializarAsync();
				Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await siguiente.ObtenerVersionEsquemaAsync());
			}

			var clave = await new ClaveFija().ObtenerAsync();
			Assert.Equal(2, ContarTipos(ruta, clave));
			Assert.False(File.Exists(temporal));
		}
		finally
		{
			File.Delete(ruta);
			File.Delete(temporal);
		}
	}

	[Fact]
	public async Task Al_cifrar_una_base_en_claro_se_conserva_su_version_de_esquema()
	{
		var ruta = RutaTemporal();
		const int VersionDeUnApkMasNuevo = 99;

		try
		{
			await using (var enClaro = new BaseDatosLocal(ruta))
			{
				await enClaro.InicializarAsync();
			}

			using (var directa = new SQLiteConnection(ruta))
			{
				directa.Execute($"PRAGMA user_version = {VersionDeUnApkMasNuevo};");
			}

			await using (var cifrada = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				Assert.Equal(VersionDeUnApkMasNuevo, await cifrada.ObtenerVersionEsquemaAsync());
			}
		}
		finally
		{
			File.Delete(ruta);
			File.Delete(ruta + ".cifrando");
		}
	}
}
