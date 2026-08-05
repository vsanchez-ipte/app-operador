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
		var bitacora = new BitacoraAuditoriaSqlite(reabierta, contexto.Reloj);

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
}
