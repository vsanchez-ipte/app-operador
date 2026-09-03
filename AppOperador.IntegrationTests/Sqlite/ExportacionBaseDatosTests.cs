using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;
using SQLite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Exportación de diagnóstico: la base cifrada se convierte en una copia SQLite legible.
/// </summary>
/// <remarks>
/// Lo que estas pruebas protegen es que la copia sirva para lo que se pidió: abrirla en un
/// equipo, sin clave, y confiar en lo que muestra. Una copia incompleta o desfasada es peor
/// que ninguna, porque se revisa semanas después y nada delata que falte algo.
/// </remarks>
public sealed class ExportacionBaseDatosTests
{

	// Niveles del catálogo real de Jacob: Crítico 1, Advertencia 2, Información 3 (JTT-1394).
	private static readonly SeveridadIncidencia Critica =
		new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Crítico", 1, "#EB1409");

	private static readonly SeveridadIncidencia Advertencia =
		new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Advertencia", 2, "#EDD611");

	private static readonly SeveridadIncidencia Informacion =
		new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Información", 3, "#120AF2");
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	/// <summary>Clave fija para las pruebas. En el dispositivo sale del almacén seguro.</summary>
	private sealed class ClaveFija : IDatabaseKeyProvider
	{
		public Task<string> ObtenerAsync(CancellationToken cancelacion = default) =>
			Task.FromResult("clave-de-prueba-32-bytes-abcdefgh");
	}

	/// <summary>
	/// Base cifrada sobre archivo temporal, con su directorio propio para las exportaciones.
	/// </summary>
	private sealed class Escenario : IAsyncDisposable
	{
		public Escenario(string? nombreDirectorio = null)
		{
			Directorio = Path.Combine(
				Path.GetTempPath(),
				nombreDirectorio ?? $"appoperador-export-pruebas-{Guid.NewGuid():N}");

			Directory.CreateDirectory(Directorio);

			Ruta = Path.Combine(Directorio, "appoperador.db3");
			Reloj = new RelojFijo();
			Sesion = new SesionFija();
			BaseDatos = new BaseDatosLocal(Ruta, new ClaveFija());
			Exportador = new ExportadorBaseDatosSqlite(BaseDatos, Reloj, Directorio);
		}

		public string Directorio { get; }

		public string Ruta { get; }

		public RelojFijo Reloj { get; }

		public SesionFija Sesion { get; }

		public BaseDatosLocal BaseDatos { get; }

		public ExportadorBaseDatosSqlite Exportador { get; }

		/// <summary>Deja dentro una incidencia real, para que la copia tenga filas propias.</summary>
		public Task<string> GuardarIncidenciaAsync() =>
			new RepositorioIncidenciasSqlite(BaseDatos, Reloj, Sesion).GuardarAsync(
				Objeto,
				Kilometer.Crear("130+200"),
				KilometerSource.GPS,
				Advertencia,
				"nota de prueba");

		public async ValueTask DisposeAsync()
		{
			await BaseDatos.DisposeAsync();

			try
			{
				Directory.Delete(Directorio, recursive: true);
			}
			catch (IOException)
			{
			}
		}
	}

	/// <summary>Abre la copia sin clave: si hace falta una, la exportación no sirvió.</summary>
	private static SQLiteConnection AbrirSinClave(string ruta) =>
		new(ruta, SQLiteOpenFlags.ReadOnly);

	private static List<string> LeerEstructura(SQLiteConnection conexion) =>
		conexion.QueryScalars<string>(
			"""
			SELECT type || ':' || name FROM sqlite_master
			WHERE type IN ('table', 'index') AND name NOT LIKE 'sqlite_%'
			ORDER BY type, name;
			""");

	[Fact]
	public async Task La_copia_se_abre_sin_clave_y_trae_las_filas()
	{
		await using var escenario = new Escenario();
		var clave = await escenario.GuardarIncidenciaAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("QA");

		using var copia = AbrirSinClave(exportacion.RutaTemporal);

		// Desde JTT-1394 la base no siembra catálogo: nace vacío y lo llena la primera descarga.
		Assert.Equal(0, copia.ExecuteScalar<int>("SELECT COUNT(*) FROM catalogo_tipo_incidencia;"));
		Assert.Equal(1, copia.ExecuteScalar<int>("SELECT COUNT(*) FROM incidencia_local;"));
		Assert.Equal(clave, copia.ExecuteScalar<string>("SELECT clave_local FROM incidencia_local;"));
	}

	[Fact]
	public async Task La_copia_conserva_la_version_de_esquema()
	{
		await using var escenario = new Escenario();
		await escenario.BaseDatos.InicializarAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("QA");

		using var copia = AbrirSinClave(exportacion.RutaTemporal);

		// sqlcipher_export no traslada los pragmas del archivo: sin copiarla a mano, la copia
		// nacería en 0 y aparentaría venir de un esquema anterior.
		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, copia.ExecuteScalar<int>("PRAGMA user_version;"));
		Assert.Equal(BaseDatosLocal.VersionEsquemaActual, exportacion.VersionEsquema);
	}

	[Fact]
	public async Task La_copia_trae_las_mismas_tablas_e_indices_que_el_origen()
	{
		await using var escenario = new Escenario();
		await escenario.GuardarIncidenciaAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("QA");

		using var origen = new SQLiteConnection(new SQLiteConnectionString(
			escenario.Ruta, storeDateTimeAsTicks: true, key: await new ClaveFija().ObtenerAsync()));
		using var copia = AbrirSinClave(exportacion.RutaTemporal);

		Assert.Equal(LeerEstructura(origen), LeerEstructura(copia));
		// Diez desde JTT-1394: las seis de antes más severidad, afectación, cuerpo y meta.
		Assert.Equal(10, exportacion.Tablas.Count);
		Assert.Contains("incidencia_local", exportacion.Tablas);
	}

	[Fact]
	public async Task La_copia_pasa_la_revision_de_integridad()
	{
		await using var escenario = new Escenario();
		await escenario.GuardarIncidenciaAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("QA");

		using var copia = AbrirSinClave(exportacion.RutaTemporal);

		Assert.Equal("ok", copia.ExecuteScalar<string>("PRAGMA quick_check;"));
	}

	[Fact]
	public async Task El_origen_sigue_cifrado_y_utilizable_despues_de_exportar()
	{
		await using var escenario = new Escenario();
		var clave = await escenario.GuardarIncidenciaAsync();

		await using (await escenario.Exportador.ExportarAsync("QA"))
		{
		}

		// Sin la clave sigue sin abrirse: exportar no puede haber dejado el original en claro.
		Assert.Throws<SQLiteException>(() =>
		{
			using var sinClave = AbrirSinClave(escenario.Ruta);
			sinClave.ExecuteScalar<int>("PRAGMA user_version;");
		});

		// Y con la clave sigue completo: la exportación tampoco pudo llevarse nada.
		var cola = new ColaSincronizacionSqlite(
			escenario.BaseDatos,
			escenario.Reloj,
			escenario.Sesion);

		Assert.Contains(await cola.ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
	}

	[Fact]
	public async Task Una_base_recien_creada_tambien_se_exporta()
	{
		await using var escenario = new Escenario();
		await escenario.BaseDatos.InicializarAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("Local");

		using var copia = AbrirSinClave(exportacion.RutaTemporal);

		// Sin incidencias, pero con las seis tablas y el catálogo sembrado.
		Assert.Equal(0, copia.ExecuteScalar<int>("SELECT COUNT(*) FROM incidencia_local;"));
		// Desde JTT-1394 la base no siembra catálogo: nace vacío y lo llena la primera descarga.
		Assert.Equal(0, copia.ExecuteScalar<int>("SELECT COUNT(*) FROM catalogo_tipo_incidencia;"));
	}

	/// <summary>
	/// La ruta del destino va dentro del texto del <c>ATTACH</c>, que no admite parámetros.
	/// </summary>
	[Fact]
	public async Task Una_ruta_con_comilla_no_rompe_la_exportacion()
	{
		await using var escenario = new Escenario($"appoperador-export-o'brien-{Guid.NewGuid():N}");
		await escenario.GuardarIncidenciaAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("QA");

		using var copia = AbrirSinClave(exportacion.RutaTemporal);

		Assert.Equal(1, copia.ExecuteScalar<int>("SELECT COUNT(*) FROM incidencia_local;"));
	}

	[Fact]
	public async Task Liberar_la_exportacion_borra_la_copia_sin_cifrar()
	{
		await using var escenario = new Escenario();
		await escenario.BaseDatos.InicializarAsync();

		string ruta;
		await using (var exportacion = await escenario.Exportador.ExportarAsync("QA"))
		{
			ruta = exportacion.RutaTemporal;
			Assert.True(File.Exists(ruta), "La copia debe existir mientras la exportación viva.");
		}

		Assert.False(File.Exists(ruta), "Al liberar no puede quedar una copia sin cifrar.");
	}

	[Fact]
	public async Task El_nombre_sugerido_lleva_ambiente_fecha_y_esquema_pero_no_al_operador()
	{
		await using var escenario = new Escenario();
		await escenario.GuardarIncidenciaAsync();

		await using var exportacion = await escenario.Exportador.ExportarAsync("QA");

		Assert.Matches($@"\AAppOperador-QA-\d{{8}}-\d{{6}}-esquema{BaseDatosLocal.VersionEsquemaActual}\.db3\z", exportacion.NombreSugerido);

		// El nombre se ve en carpetas compartidas y en el título de las herramientas.
		Assert.DoesNotContain(escenario.Sesion.Actual!.Operador, exportacion.NombreSugerido);
		Assert.DoesNotContain(escenario.Sesion.Actual!.UnidadVehicular, exportacion.NombreSugerido);
	}

	[Fact]
	public async Task Dos_exportaciones_seguidas_no_se_pisan()
	{
		await using var escenario = new Escenario();
		await escenario.GuardarIncidenciaAsync();

		await using var primera = await escenario.Exportador.ExportarAsync("QA");
		await using var segunda = await escenario.Exportador.ExportarAsync("QA");

		Assert.NotEqual(primera.RutaTemporal, segunda.RutaTemporal);
		Assert.True(File.Exists(primera.RutaTemporal));
		Assert.True(File.Exists(segunda.RutaTemporal));
	}
}
