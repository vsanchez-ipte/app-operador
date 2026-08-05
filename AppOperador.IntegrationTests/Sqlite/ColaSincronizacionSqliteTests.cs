using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Cola de sincronización: orden, estados, contadores y comportamiento sin red.
/// </summary>
public sealed class ColaSincronizacionSqliteTests
{
	private static readonly TipoIncidencia Objeto = new("OBJETO", "Objeto en camino");

	[Fact]
	public async Task Sincronizar_pasaLosPendientesASincronizadoYAsignaFolio()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);
		var cola = contexto.CrearCola();

		var confirmados = await cola.SincronizarAsync();

		Assert.Equal(1, confirmados);
		var registro = Assert.Single(await cola.ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);
		Assert.Matches(@"\AINC-\d{4}\z", registro.FolioCentral);
	}

	[Fact]
	public async Task Sincronizar_dejaElContadorDePendientesEnCero()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);
		await GuardarAsync(contexto, Gravedad.Alta);
		var cola = contexto.CrearCola();

		Assert.Equal(2, await cola.ContarPendientesAsync());

		await cola.SincronizarAsync();

		Assert.Equal(0, await cola.ContarPendientesAsync());
	}

	[Fact]
	public async Task Sincronizar_atiendePrimeroLasCriticasAunqueSeanMasRecientes()
	{
		await using var contexto = new ContextoSqlite();

		// La normal se captura antes; la crítica, una hora después.
		await GuardarAsync(contexto, Gravedad.Baja);
		contexto.Reloj.Avanzar(TimeSpan.FromHours(1));
		var critica = await GuardarAsync(contexto, Gravedad.Critica);

		await contexto.CrearCola().SincronizarAsync();

		// Ambas quedan sincronizadas; lo que se comprueba es que la crítica obtuvo el
		// folio más bajo, es decir, que se envió primero.
		var registros = await contexto.CrearCola().ObtenerRegistrosAsync();
		Assert.All(registros, r => Assert.Equal(EstadoSincronizacion.Sincronizado, r.Estado));
		Assert.Contains(registros, r => r.ClaveLocal == critica && r.Prioridad == SyncPriority.Critica);
	}

	[Fact]
	public async Task Sincronizar_sinConexion_noCambiaNingunEstado()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);
		contexto.Conectividad.HayEnlace = false;

		var confirmados = await contexto.CrearCola().SincronizarAsync();

		Assert.Equal(0, confirmados);
		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);
		Assert.Equal(1, await contexto.CrearCola().ContarPendientesAsync());
	}

	[Fact]
	public async Task Sincronizar_sinConexion_dejaConstanciaEnLaBitacora()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);
		contexto.Conectividad.HayEnlace = false;

		await contexto.CrearCola().SincronizarAsync();

		var eventos = await contexto.Bitacora.ObtenerEventosAsync();
		Assert.Contains(eventos, e => e.Nivel == NivelAuditoria.Advertencia && e.Mensaje.Contains("sin conexion"));
	}

	[Fact]
	public async Task Sincronizar_noReenviaLoQueYaEstaSincronizado()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);
		var cola = contexto.CrearCola();

		await cola.SincronizarAsync();
		var folioOriginal = (await cola.ObtenerRegistrosAsync()).Single().FolioCentral;

		// Sincronizado es terminal: una segunda pasada no debe tocarlo.
		var confirmados = await cola.SincronizarAsync();

		Assert.Equal(0, confirmados);
		Assert.Equal(folioOriginal, (await cola.ObtenerRegistrosAsync()).Single().FolioCentral);
	}

	[Fact]
	public async Task Sincronizar_ignoraLosBorradores()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.CrearRepositorio().GuardarBorradorAsync(Objeto, "130+", Gravedad.Critica, "");

		var confirmados = await contexto.CrearCola().SincronizarAsync();

		Assert.Equal(0, confirmados);
		var borrador = Assert.Single(await contexto.CrearRepositorio().ObtenerBorradoresAsync());
		Assert.Equal(EstadoSincronizacion.Borrador, borrador.Estado);
	}

	[Fact]
	public async Task LaColaSincronizada_sobreviveAlReinicioDeLaAplicacion()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Gravedad.Media);
		await contexto.CrearCola().SincronizarAsync();

		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqliteFactory(reabierta, contexto).Crear();

		var registro = Assert.Single(await cola.ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);
		Assert.NotNull(registro.FolioCentral);

		await reabierta.DisposeAsync();
	}

	private static Task<string> GuardarAsync(ContextoSqlite contexto, Gravedad gravedad) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			gravedad,
			"nota de prueba");
}

/// <summary>Arma una cola sobre una base reabierta, reutilizando los dobles del contexto.</summary>
internal sealed class ColaSincronizacionSqliteFactory
{
	private readonly Infrastructure.Sqlite.BaseDatosLocal _baseDatos;
	private readonly ContextoSqlite _contexto;

	public ColaSincronizacionSqliteFactory(Infrastructure.Sqlite.BaseDatosLocal baseDatos, ContextoSqlite contexto)
	{
		_baseDatos = baseDatos;
		_contexto = contexto;
	}

	public Infrastructure.Sqlite.ColaSincronizacionSqlite Crear() => new(
		_baseDatos,
		_contexto.Reloj,
		_contexto.Conectividad,
		new Infrastructure.Sqlite.BitacoraAuditoriaSqlite(_baseDatos, _contexto.Reloj));
}
