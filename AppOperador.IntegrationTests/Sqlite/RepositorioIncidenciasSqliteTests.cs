using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Guardado de incidencias y borradores contra una base real.
/// </summary>
public sealed class RepositorioIncidenciasSqliteTests
{
	private static readonly TipoIncidencia Objeto = new("OBJETO", "Objeto en camino");

	[Fact]
	public async Task Guardar_devuelveClaveLocalConElFormatoDeLaMaqueta()
	{
		await using var contexto = new ContextoSqlite();

		var clave = await GuardarAsync(contexto, Gravedad.Media);

		Assert.Matches(@"\ALOC-\d{6}\z", clave);
	}

	[Fact]
	public async Task Guardar_asignaClavesLocalesDistintasYCrecientes()
	{
		await using var contexto = new ContextoSqlite();

		var primera = await GuardarAsync(contexto, Gravedad.Media);
		var segunda = await GuardarAsync(contexto, Gravedad.Media);

		Assert.NotEqual(primera, segunda);
		Assert.True(string.CompareOrdinal(segunda, primera) > 0, "La clave local debe crecer.");
	}

	[Fact]
	public async Task LoGuardado_sobreviveAlReinicioDeLaAplicacion()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Gravedad.Media);

		// Instancia nueva sobre el mismo archivo: es la prueba de que persiste de verdad
		// y no solo mientras el proceso vive.
		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqlite(
			reabierta, contexto.Reloj, contexto.Conectividad, new BitacoraAuditoriaSqlite(reabierta, contexto.Reloj));

		var registros = await cola.ObtenerRegistrosAsync();

		Assert.Contains(registros, r => r.ClaveLocal == clave);
		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task Guardar_derivaLaPrioridadDeLaGravedadSegunLaReglaDeDominio()
	{
		await using var contexto = new ContextoSqlite();

		await GuardarAsync(contexto, Gravedad.Critica);
		await GuardarAsync(contexto, Gravedad.Baja);

		var registros = await contexto.CrearCola().ObtenerRegistrosAsync();

		Assert.Single(registros, r => r.Prioridad == SyncPriority.Critica);
		Assert.Single(registros, r => r.Prioridad == SyncPriority.Normal);
	}

	[Fact]
	public async Task GuardarBorrador_loDejaFueraDeLaCola()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();

		// Un kilómetro a medio escribir: el borrador lo admite, la incidencia no.
		var clave = await repositorio.GuardarBorradorAsync(null, "130+", Gravedad.Media, "");

		var borradores = await repositorio.ObtenerBorradoresAsync();
		var enCola = await contexto.CrearCola().ObtenerRegistrosAsync();

		Assert.Contains(borradores, b => b.ClaveLocal == clave);
		Assert.DoesNotContain(enCola, r => r.ClaveLocal == clave);
		Assert.Equal(0, await contexto.CrearCola().ContarPendientesAsync());
	}

	[Fact]
	public async Task GuardarBorrador_conservaElEstadoBorrador()
	{
		await using var contexto = new ContextoSqlite();

		await contexto.CrearRepositorio().GuardarBorradorAsync(Objeto, null, Gravedad.Alta, "nota");

		var borrador = Assert.Single(await contexto.CrearRepositorio().ObtenerBorradoresAsync());
		Assert.Equal(EstadoSincronizacion.Borrador, borrador.Estado);
	}

	[Fact]
	public async Task Guardar_dejaLaIncidenciaPendienteYSinFolioCentral()
	{
		await using var contexto = new ContextoSqlite();

		await GuardarAsync(contexto, Gravedad.Media);

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);
		// El folio lo asigna Jacob: antes de sincronizar no puede existir.
		Assert.Null(registro.FolioCentral);
	}

	[Fact]
	public async Task Descripcion_traeSoloElTipoSinRepetirClaseNiPrioridad()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());

		// La vista antepone "clase / prioridad / ..." al componer la tarjeta. Si el mapeo
		// los incluye también, salen duplicados en pantalla.
		Assert.Equal("Objeto en camino", registro.Descripcion);
		Assert.DoesNotContain("Incidencia", registro.Descripcion);
		Assert.DoesNotContain("Normal", registro.Descripcion);
	}

	private static Task<string> GuardarAsync(ContextoSqlite contexto, Gravedad gravedad) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			gravedad,
			"nota de prueba");
}
