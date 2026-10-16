using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class RepositorioUnidadesSqliteTests
{
	private static readonly DateTime Primera = new(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc);
	private static readonly DateTime Segunda = Primera.AddHours(9);

	private static readonly EstadoUnidad Operativo = new("operativo", "Operativo", "#25B324");
	private static readonly EstadoUnidad Mantenimiento = new("mantenimiento", "Mantenimiento", "#120AF2");

	[Fact]
	public async Task Sin_sincronizar_no_hay_catalogo_ni_fecha()
	{
		await using var contexto = new ContextoSqlite();

		var catalogo = await new RepositorioUnidadesSqlite(contexto.BaseDatos).ObtenerAsync();

		Assert.Empty(catalogo.Unidades);
		Assert.Null(catalogo.SincronizadoUtc);
	}

	[Fact]
	public async Task Guarda_identificador_nombre_estado_y_fecha_de_sincronizacion()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioUnidadesSqlite(contexto.BaseDatos);

		await repositorio.GuardarAsync(
		[
			new UnidadVehicular("d-2", "VEH-002", "Pickup 2", Disponible: false, Mantenimiento),
			new UnidadVehicular("d-1", "VEH-001", "Pickup 1", Disponible: true, Operativo),
		], Primera);

		var catalogo = await repositorio.ObtenerAsync();

		Assert.Equal(Primera, catalogo.SincronizadoUtc);
		Assert.Equal(
		[
			new UnidadVehicular("d-1", "VEH-001", "Pickup 1", true, Operativo),
			new UnidadVehicular("d-2", "VEH-002", "Pickup 2", false, Mantenimiento),
		], catalogo.Unidades);
	}

	[Fact]
	public async Task La_siguiente_sincronizacion_actualiza_y_deja_fuera_lo_que_ya_no_llega()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioUnidadesSqlite(contexto.BaseDatos);
		await repositorio.GuardarAsync(
		[
			new UnidadVehicular("d-1", "VEH-001", "Pickup 1", true, Operativo),
			new UnidadVehicular("d-2", "VEH-002", "Pickup 2", true, Operativo),
		], Primera);

		await repositorio.GuardarAsync(
			[new UnidadVehicular("d-1", "VEH-001", "Pickup 1", false, Mantenimiento)], Segunda);

		var catalogo = await repositorio.ObtenerAsync();

		Assert.Equal(Segunda, catalogo.SincronizadoUtc);
		Assert.Equal([new UnidadVehicular("d-1", "VEH-001", "Pickup 1", false, Mantenimiento)], catalogo.Unidades);
		// La que ya no llegó se conserva: puede haber sesiones o incidencias que la usen.
		Assert.Equal(2, await contexto.EscalarAsync<int>("SELECT COUNT(*) FROM unidad_local"));
	}

	[Fact]
	public async Task Abrir_una_sesion_no_borra_el_estado_de_su_unidad()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = new RepositorioUnidadesSqlite(contexto.BaseDatos);
		var unidad = new UnidadVehicular("d-1", "VEH-001", "Pickup 1", true, Operativo);
		await repositorio.GuardarAsync([unidad], Primera);

		await new AlmacenSesionOfflineSqlite(contexto.BaseDatos).GuardarAsync(new SesionOfflinePersistida(
			SessionId: "s-1",
			Operador: "Juan Pérez",
			Rol: "Operador de campo",
			Unidad: new UnidadVehicular("d-1", "VEH-001", "Pickup 1"),
			Permisos: PermisosOperador.DelServidor(["APP_OPERADOR_MOVIL"]),
			Vigencia: VigenciaOffline.DelServidor(Primera, Primera.AddHours(8)),
			MonotonicoAlValidar: TimeSpan.FromHours(3),
			Instalacion: new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23))));

		var catalogo = await repositorio.ObtenerAsync();

		Assert.Equal([unidad], catalogo.Unidades);
	}

	[Fact]
	public async Task Una_base_en_version_11_conserva_sus_unidades_como_elegibles_y_sin_sincronizar()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.BaseDatos.InicializarAsync();
		await contexto.BaseDatos.DisposeAsync();

		await contexto.EjecutarSqlAsync("PRAGMA foreign_keys = OFF;");
		await contexto.EjecutarSqlAsync("DROP TABLE unidad_local;");
		await contexto.EjecutarSqlAsync(
			"CREATE TABLE unidad_local (clave TEXT NOT NULL PRIMARY KEY, id TEXT NOT NULL DEFAULT '', descripcion TEXT NOT NULL DEFAULT '');");
		await contexto.EjecutarSqlAsync("INSERT INTO unidad_local VALUES ('VEH-01', 'u-1', 'Camioneta 01');");
		await contexto.EjecutarSqlAsync("PRAGMA user_version = 11;");

		await using var reabierta = contexto.ReabrirBaseDatos();
		await reabierta.InicializarAsync();

		Assert.Equal(12, await reabierta.ObtenerVersionEsquemaAsync());
		Assert.Equal(1, await contexto.EscalarAsync<int>(
			"SELECT COUNT(*) FROM unidad_local WHERE clave = 'VEH-01' AND id = 'u-1' AND disponible = 1 AND sincronizado_utc_ticks = 0"));
		Assert.Empty((await new RepositorioUnidadesSqlite(reabierta).ObtenerAsync()).Unidades);
	}
}
