using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Sesión persistida entre arranques de la app (JTT-1383).
/// </summary>
public sealed class AlmacenSesionOfflineSqliteTests
{
	private static readonly DateTime Validacion = new(2026, 8, 11, 8, 0, 0, DateTimeKind.Utc);

	private static SesionOfflinePersistida Sesion() => new(
		SessionId: "s-1",
		Operador: "Juan Pérez",
		Rol: "Operador de campo",
		Unidad: new UnidadVehicular("u-1", "VEH-01", "Camioneta 01"),
		Permisos: PermisosOperador.DelServidor(["APP_OPERADOR_MOVIL"]),
		Vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
		MonotonicoAlValidar: TimeSpan.FromHours(3),
		Instalacion: new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)));

	[Fact]
	public async Task Sin_nada_guardado_no_hay_sesion()
	{
		await using var contexto = new ContextoSqlite();

		Assert.Null(await new AlmacenSesionOfflineSqlite(contexto.BaseDatos).ObtenerAsync());
	}

	[Fact]
	public async Task Guarda_y_devuelve_la_sesion_completa()
	{
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);

		await almacen.GuardarAsync(Sesion());
		var recuperada = await almacen.ObtenerAsync();

		Assert.NotNull(recuperada);
		Assert.Equal("s-1", recuperada.SessionId);
		Assert.Equal("Juan Pérez", recuperada.Operador);
		Assert.Equal("VEH-01", recuperada.Unidad.Clave);
		Assert.Equal("u-1", recuperada.Unidad.Id);
		Assert.Equal(["APP_OPERADOR_MOVIL"], recuperada.Permisos);
		Assert.Equal("1.2.0", recuperada.Instalacion.VersionAplicacion);
	}

	[Fact]
	public async Task Conserva_la_ventana_offline_tal_como_se_guardo()
	{
		// La calcula el servidor y la app no la recalcula (JTT-1382 CA 3 y 4).
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);

		await almacen.GuardarAsync(Sesion());
		var recuperada = await almacen.ObtenerAsync();

		Assert.Equal(Validacion, recuperada!.Vigencia.LastValidatedAtUtc);
		Assert.Equal(Validacion.AddHours(8), recuperada.Vigencia.OfflineUntilUtc);
	}

	[Fact]
	public async Task Conserva_la_referencia_monotonica()
	{
		// Es lo que permite detectar despues que movieron el reloj (CA 5).
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);

		await almacen.GuardarAsync(Sesion());

		Assert.Equal(TimeSpan.FromHours(3), (await almacen.ObtenerAsync())!.MonotonicoAlValidar);
	}

	[Fact]
	public async Task La_sesion_sobrevive_al_reinicio_de_la_aplicacion()
	{
		// CA 6: reanudar solo tiene sentido si la sesion resiste cerrar la app.
		await using var contexto = new ContextoSqlite();
		await new AlmacenSesionOfflineSqlite(contexto.BaseDatos).GuardarAsync(Sesion());

		var reabierta = contexto.ReabrirBaseDatos();
		var recuperada = await new AlmacenSesionOfflineSqlite(reabierta).ObtenerAsync();

		Assert.NotNull(recuperada);
		Assert.Equal("s-1", recuperada.SessionId);

		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task Guardar_de_nuevo_deja_vigente_solo_la_ultima()
	{
		// Reanudar mira la ultima validacion y nada mas. La anterior no se borra —desde el
		// esquema 11 la sesion es historial y lo capturado apunta a ella—, pero deja de ser la
		// vigente, asi que no hay forma de reanudarla.
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);

		await almacen.GuardarAsync(Sesion());
		await almacen.GuardarAsync(Sesion() with { SessionId = "s-2", Operador = "Ana López" });

		var recuperada = await almacen.ObtenerAsync();
		Assert.Equal("s-2", recuperada!.SessionId);
		Assert.Equal("Ana López", recuperada.Operador);

		var historial = await contexto.ConsultarAsync<FilaSesion>(
			"SELECT session_id AS SessionId, vigente AS Vigente FROM sesion_local ORDER BY session_id");
		Assert.Equal([("s-1", 0), ("s-2", 1)], historial.Select(h => (h.SessionId, h.Vigente)));
	}

	[Fact]
	public async Task Revalidar_la_misma_sesion_actualiza_su_fila_sin_duplicarla()
	{
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);

		await almacen.GuardarAsync(Sesion());
		await almacen.GuardarAsync(Sesion() with
		{
			Vigencia = VigenciaOffline.DelServidor(Validacion.AddHours(4), Validacion.AddHours(12)),
		});

		var recuperada = await almacen.ObtenerAsync();
		Assert.Equal(Validacion.AddHours(12), recuperada!.Vigencia.OfflineUntilUtc);
		Assert.Equal(1, await contexto.EscalarAsync<int>("SELECT COUNT(*) FROM sesion_local"));
	}

	[Fact]
	public async Task Guardar_deja_creados_el_operador_y_la_unidad_a_los_que_apunta()
	{
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);

		await almacen.GuardarAsync(Sesion());

		Assert.Equal("Operador de campo", await contexto.EscalarAsync<string>(
			"SELECT rol FROM operador_local WHERE cuenta = 'Juan Pérez'"));
		Assert.Equal("Camioneta 01", await contexto.EscalarAsync<string>(
			"SELECT descripcion FROM unidad_local WHERE clave = 'VEH-01' AND id = 'u-1'"));
	}

	[Fact]
	public async Task Limpiar_conserva_la_sesion_para_lo_que_apunta_a_ella()
	{
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);
		await almacen.GuardarAsync(Sesion());

		await almacen.LimpiarAsync();

		Assert.Null(await almacen.ObtenerAsync());
		Assert.Equal(1, await contexto.EscalarAsync<int>("SELECT COUNT(*) FROM sesion_local WHERE session_id = 's-1' AND vigente = 0"));
	}

	public sealed class FilaSesion
	{
		public string SessionId { get; set; } = "";
		public int Vigente { get; set; }
	}

	[Fact]
	public async Task Limpiar_deja_la_app_sin_sesion_que_reanudar()
	{
		await using var contexto = new ContextoSqlite();
		var almacen = new AlmacenSesionOfflineSqlite(contexto.BaseDatos);
		await almacen.GuardarAsync(Sesion());

		await almacen.LimpiarAsync();

		Assert.Null(await almacen.ObtenerAsync());
	}

	[Fact]
	public async Task Limpiar_sin_sesion_guardada_no_falla()
	{
		await using var contexto = new ContextoSqlite();

		await new AlmacenSesionOfflineSqlite(contexto.BaseDatos).LimpiarAsync();
	}
}
