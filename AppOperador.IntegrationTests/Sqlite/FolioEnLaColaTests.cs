using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class FolioEnLaColaTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	private static readonly SeveridadIncidencia Critica =
		new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Crítico", 1, "#EB1409");

	private const string Folio = "INC-APK-2026-0034";

	[Fact]
	public async Task Confirmada_dejaDeAparecerComoPendienteYQuedaConFolio()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);
		var jacob = new JacobControlado().Responde(Aceptada(Folio));

		await contexto.CrearSincronizador(jacob).EjecutarAsync();

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);
		Assert.Equal(Folio, registro.FolioCentral);
		Assert.Equal(Folio, registro.ReferenciaPrincipal);
		Assert.Equal(0, await contexto.CrearCola().ContarPendientesAsync());
	}

	[Fact]
	public async Task ElFolio_sobreviveAlCierreDeSesionYAlReinicio()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);
		await contexto.CrearSincronizador(new JacobControlado().Responde(Aceptada(Folio))).EjecutarAsync();

		contexto.Sesion.Limpiar();

		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqlite(reabierta, contexto.Reloj, new SesionFija());

		var registro = Assert.Single(await cola.ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal(Folio, registro.FolioCentral);
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);

		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task UnaActualizacionSinFolio_noBorraElQueYaSeHabiaConfirmado()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);
		var cola = contexto.CrearCola();

		// Antes de enviar: confirmada, la cola ya no la expone.
		var uuid = (await cola.ObtenerEnviablePorClaveAsync(clave))!.Uuid;

		await contexto.CrearSincronizador(new JacobControlado().Responde(Aceptada(Folio))).EjecutarAsync();

		await cola.ActualizarEnvioAsync(new ActualizacionEnvio(
			uuid,
			EstadoSincronizacion.Sincronizado,
			Intentos: 2,
			FolioCentral: null,
			UltimoErrorCodigo: null));

		var registro = Assert.Single(await cola.ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal(Folio, registro.FolioCentral);
	}

	[Fact]
	public async Task ActualizarElEnvio_noSeLlevaLoQueElOperadorCapturo()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);
		var cola = contexto.CrearCola();

		var antes = await cola.ObtenerEnviablePorClaveAsync(clave);
		Assert.NotNull(antes);

		await contexto.CrearSincronizador(new JacobControlado().RechazaTecnico()).EjecutarAsync();

		var despues = await cola.ObtenerEnviablePorClaveAsync(clave);
		Assert.NotNull(despues);

		Assert.Equal(antes.Uuid, despues.Uuid);
		Assert.Equal(antes.TipoId, despues.TipoId);
		Assert.Equal(antes.SeveridadId, despues.SeveridadId);
		Assert.Equal(antes.Kilometro, despues.Kilometro);
		Assert.Equal(antes.FuenteKilometro, despues.FuenteKilometro);
		Assert.Equal(antes.Nota, despues.Nota);
		Assert.Equal(antes.CapturadaUtc, despues.CapturadaUtc);
		Assert.Equal(antes.SesionOrigen, despues.SesionOrigen);

		Assert.Equal(1, despues.Intentos);
	}

	[Fact]
	public async Task ElRegistroConfirmado_conservaSuTipoSuKilometroYSuPrioridad()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await contexto.CrearRepositorio().GuardarAsync(
			Objeto, Kilometer.Crear("130+200"), KilometerSource.Manual, Critica, "nota de prueba");

		await contexto.CrearSincronizador(new JacobControlado().Responde(Aceptada(Folio))).EjecutarAsync();

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal("Objeto en camino", registro.Descripcion);
		Assert.Equal("130+200", registro.Kilometro);
		Assert.Equal(SyncPriority.Critica, registro.Prioridad);

		// La clave local no desaparece al llegar el folio; baja a línea de trazabilidad.
		Assert.Equal(clave, registro.ClaveLocal);
	}

	private static ResultadoEnvio Aceptada(string folio) =>
		ResultadoEnvio.Aceptada(new IncidenciaRegistrada(folio, new DateTime(2026, 8, 4, 12, 0, 0, DateTimeKind.Utc), false));

	private static Task<string> GuardarAsync(ContextoSqlite contexto) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			Critica,
			"nota de prueba");
}
