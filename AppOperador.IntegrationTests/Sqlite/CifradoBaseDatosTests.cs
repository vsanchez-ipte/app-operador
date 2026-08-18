using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Sqlite;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// JTT-1388 CA 2 y 4: la base local está cifrada, y una base en claro se migra sin perder nada.
/// </summary>
public sealed class CifradoBaseDatosTests
{
	/// <summary>Clave fija para las pruebas. En el dispositivo sale del almacén seguro.</summary>
	private sealed class ClaveFija : IDatabaseKeyProvider
	{
		public Task<string> ObtenerAsync(CancellationToken cancelacion = default) =>
			Task.FromResult("clave-de-prueba-32-bytes-abcdefgh");
	}

	private static string RutaTemporal() =>
		Path.Combine(Path.GetTempPath(), $"appoperador-cifrado-{Guid.NewGuid():N}.db3");

	/// <summary>Cuenta el catálogo sembrado, con clave o sin ella, sin pasar por la app.</summary>
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

	/// <summary>
	/// El escenario de la actualización: quien ya tenía la app tiene una base en claro con sus
	/// incidencias dentro. Al cifrarla no puede perderlas (CA 8 y 11).
	/// </summary>
	[Fact]
	public async Task Una_base_en_claro_se_cifra_conservando_los_datos()
	{
		var ruta = RutaTemporal();
		try
		{
			// Una base como la que dejó la versión anterior: sin clave y con el catálogo sembrado.
			await using (var enClaro = new BaseDatosLocal(ruta))
			{
				await enClaro.InicializarAsync();
			}

			var antes = ContarTipos(ruta, clave: null);
			Assert.Equal(6, antes);

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

	/// <summary>
	/// La migración se corta entre borrar el original y poner el cifrado en su sitio: el arranque
	/// siguiente tiene que recuperar los datos, no empezar de cero.
	/// </summary>
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

			// El estado exacto que deja el corte: sin base en la ruta y con el cifrado al lado.
			File.Move(ruta, temporal);

			await using (var siguiente = new BaseDatosLocal(ruta, new ClaveFija()))
			{
				await siguiente.InicializarAsync();
				Assert.Equal(BaseDatosLocal.VersionEsquemaActual, await siguiente.ObtenerVersionEsquemaAsync());
			}

			var clave = await new ClaveFija().ObtenerAsync();
			Assert.Equal(6, ContarTipos(ruta, clave));
			Assert.False(File.Exists(temporal));
		}
		finally
		{
			File.Delete(ruta);
			File.Delete(temporal);
		}
	}

	/// <summary>
	/// Al cifrar, la versión de esquema del archivo tiene que viajar con los datos.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>sqlcipher_export</c> traslada esquema y filas, pero no los pragmas del archivo. Si
	/// <c>user_version</c> se perdiera, la base cifrada nacería en 0.
	/// </para>
	/// <para>
	/// <b>Por qué la versión de la prueba es 99 y no la actual.</b> Con la actual el defecto no
	/// se ve: <c>MigrarAsync</c> corre justo después, encuentra un 0, lo trata como base
	/// anterior a la primera versión y vuelve a sellar el número correcto. El resultado final
	/// es el mismo y la prueba pasaría igual con el defecto dentro. Con una versión por encima
	/// de la actual —una base escrita por un APK más nuevo— la migración se detiene sin tocar
	/// nada, así que lo que quede en el archivo es exactamente lo que dejó la exportación.
	/// </para>
	/// </remarks>
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
