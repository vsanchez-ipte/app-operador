using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Bitácora local: persistencia, orden, recorte y manejo de UTC.
/// </summary>
public sealed class BitacoraAuditoriaSqliteTests
{
	[Fact]
	public async Task Registrar_persisteElEventoYLoDevuelveEnUtc()
	{
		await using var contexto = new ContextoSqlite();

		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "Enlace CCO activo.");

		var evento = Assert.Single(await contexto.Bitacora.ObtenerEventosAsync());
		Assert.Equal("Enlace CCO activo.", evento.Mensaje);
		Assert.Equal(NivelAuditoria.Info, evento.Nivel);

		// El Kind es lo que más fácil se pierde al pasar por SQLite: sin Utc, la vista
		// convertiría a hora local partiendo de una hora que ya era local.
		Assert.Equal(DateTimeKind.Utc, evento.InstanteUtc.Kind);
		Assert.Equal(contexto.Reloj.UtcAhora, evento.InstanteUtc);
	}

	[Fact]
	public async Task ObtenerEventos_devuelveDelMasRecienteAlMasAntiguo()
	{
		await using var contexto = new ContextoSqlite();

		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "primero");
		contexto.Reloj.Avanzar(TimeSpan.FromMinutes(5));
		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Advertencia, "segundo");

		var eventos = await contexto.Bitacora.ObtenerEventosAsync();

		Assert.Equal("segundo", eventos[0].Mensaje);
		Assert.Equal("primero", eventos[1].Mensaje);
	}

	[Fact]
	public async Task LosEventos_sobrevivenAlReinicioDeLaAplicacion()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "antes de cerrar");

		var reabierta = contexto.ReabrirBaseDatos();
		var bitacora = contexto.CrearBitacora(reabierta);

		var evento = Assert.Single(await bitacora.ObtenerEventosAsync());
		Assert.Equal("antes de cerrar", evento.Mensaje);

		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task Registrar_recortaLaBitacoraAlTopeYConservaLosMasRecientes()
	{
		await using var contexto = new ContextoSqlite();

		// Diez por encima del tope: la tabla no puede crecer sin límite en un dispositivo
		// que pasa semanas sin mantenimiento.
		for (var i = 0; i < BitacoraAuditoriaSqlite.EventosMaximos + 10; i++)
		{
			contexto.Reloj.Avanzar(TimeSpan.FromSeconds(1));
			await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, $"evento {i}");
		}

		var eventos = await contexto.Bitacora.ObtenerEventosAsync();

		Assert.Equal(BitacoraAuditoriaSqlite.EventosMaximos, eventos.Count);
		Assert.Equal($"evento {BitacoraAuditoriaSqlite.EventosMaximos + 9}", eventos[0].Mensaje);
		Assert.DoesNotContain(eventos, e => e.Mensaje == "evento 0");
	}

	// ---------- JTT-1392: quién y desde dónde, en cada línea ----------

	[Fact]
	public async Task Registrar_copiaEnLaLineaAlOperadorSuUnidadSuSesionYElOrigen()
	{
		await using var contexto = new ContextoSqlite();
		contexto.Conectividad.HayEnlace = false;

		await contexto.Bitacora.RegistrarAsync(
			OperacionAuditada.Sincronizacion, ResultadoAuditoria.Rechazo, "sin enlace",
			motivoCodigo: "appincidencias.error.tecnico");

		var evento = Assert.Single(await contexto.Bitacora.ObtenerEventosAsync());
		Assert.Equal(OperacionAuditada.Sincronizacion, evento.Operacion);
		Assert.Equal(ResultadoAuditoria.Rechazo, evento.Resultado);
		Assert.Equal("appincidencias.error.tecnico", evento.MotivoCodigo);
		Assert.Equal(NivelAuditoria.Advertencia, evento.Nivel);
		Assert.Equal(contexto.Sesion.Actual!.Operador, evento.Operador);
		Assert.Equal(contexto.Sesion.Actual.Rol, evento.Rol);
		Assert.Equal(contexto.Sesion.Actual.UnidadVehicular, evento.UnidadClave);
		Assert.Equal(contexto.Sesion.Actual.SessionId, evento.SesionId);
		Assert.Equal(OrigenAuditoria.Offline, evento.Origen);
		Assert.Equal(contexto.Monotonico.Transcurrido.Ticks, evento.MonotonicoTicks);
	}

	[Fact]
	public async Task ObtenerEventos_devuelveSoloLoDelOperadorConSesion_yLoAnteriorSinOperador()
	{
		// El Perfil es del operador (D3). Lo del turno anterior sigue guardado, pero no se le
		// muestra; lo que se escribió antes del esquema 10 no tiene operador y se muestra a todos.
		await using var contexto = new ContextoSqlite();
		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "de admin");

		var otro = new SesionFija("otro");
		await contexto.CrearBitacoraDe(otro).RegistrarAsync(NivelAuditoria.Info, "de otro");
		await contexto.CrearBitacoraDe(new SesionSinNadie()).RegistrarAsync(NivelAuditoria.Info, "historial previo");

		var deAdmin = (await contexto.Bitacora.ObtenerEventosAsync()).Select(e => e.Mensaje).ToArray();
		var deOtro = (await contexto.CrearBitacoraDe(otro).ObtenerEventosAsync()).Select(e => e.Mensaje).ToArray();

		Assert.Equal(["historial previo", "de admin"], deAdmin);
		Assert.Equal(["historial previo", "de otro"], deOtro);
	}

	[Fact]
	public async Task ObtenerEventos_sinSesionNoDevuelveNada()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "algo");

		Assert.Empty(await contexto.CrearBitacoraDe(new SesionSinNadie()).ObtenerEventosAsync());
	}

	[Fact]
	public async Task ElOrden_noLoMueveElRelojDelDispositivo()
	{
		// Hallazgo del 12-ago: atrasar la hora del teléfono desordenaba el historial. El orden
		// es el de escritura, que ni el reloj ni un reinicio pueden mover.
		await using var contexto = new ContextoSqlite();
		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "primero");
		contexto.Reloj.Avanzar(TimeSpan.FromHours(-3));
		await contexto.Bitacora.RegistrarAsync(NivelAuditoria.Info, "segundo, con el reloj atrasado");

		var mensajes = (await contexto.Bitacora.ObtenerEventosAsync()).Select(e => e.Mensaje).ToArray();

		Assert.Equal(["segundo, con el reloj atrasado", "primero"], mensajes);
	}

	[Fact]
	public async Task SinSesion_laLineaSaleANombreDeQuienDigaQuienRegistra()
	{
		// El acceso escribe antes de que exista sesión: se atribuye al correo que se intentó.
		await using var contexto = new ContextoSqlite();
		var sinSesion = contexto.CrearBitacoraDe(new SesionSinNadie());

		await sinSesion.RegistrarAsync(
			OperacionAuditada.Autenticacion, ResultadoAuditoria.Rechazo, "denegado",
			motivoCodigo: "appoperador.credencial.invalida", operador: "op@ipte.com.mx");

		var evento = Assert.Single(await contexto.CrearBitacoraDe(new SesionFija("op@ipte.com.mx")).ObtenerEventosAsync());
		Assert.Equal("op@ipte.com.mx", evento.Operador);
		Assert.Null(evento.SesionId);
	}
}

/// <summary>Sesión que no existe: antes del acceso, o después de cerrarla.</summary>
public sealed class SesionSinNadie : ISessionStore
{
	public SesionOperador? Actual => null;

	public void Guardar(SesionOperador sesion) { }

	public void Limpiar() { }
}
