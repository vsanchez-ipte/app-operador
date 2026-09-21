using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// El folio central en la cola local (JTT-1403), contra una base real.
/// </summary>
/// <remarks>
/// <b>Qué se comprueba aquí y qué no.</b> Que el folio encabece la tarjeta es el CA 1 y vive en
/// <c>RegistroCola</c>, con sus pruebas unitarias. Lo que solo se puede afirmar contra la base es
/// lo demás: que el folio quede <b>escrito</b> y no solo mostrado, que sobreviva al cierre de
/// sesión y al reinicio, y que actualizar el envío no se lleve por delante lo que el operador
/// capturó.
/// </remarks>
public sealed class FolioEnLaColaTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	private static readonly SeveridadIncidencia Critica =
		new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Crítico", 1, "#EB1409");

	private const string Folio = "INC-APK-2026-0034";

	[Fact]
	public async Task Confirmada_dejaDeAparecerComoPendienteYQuedaConFolio()
	{
		// CA 3 y CA 4: la cola muestra el folio y el registro deja de estar pendiente. Las dos
		// mitades juntas, porque un registro con folio que siguiera contando como pendiente
		// haría que el operador lo mandara otra vez.
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
		// CA 5. Es la secuencia real: el operador confirma, cierra sesión al terminar el turno,
		// la app se reinicia y alguien vuelve a entrar. Si el folio se perdiera ahí, el registro
		// quedaría sin la única referencia que el CCO puede buscar, y el operador no tendría con
		// qué preguntar por él.
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
		// CA 5 por el otro lado. Sincronizado es terminal, así que hoy la orquestación no vuelve
		// a tocar un registro confirmado; la guarda está en la cola porque el día que algo lo
		// haga —un reenvío de evidencia, una migración— un folio en blanco lo borraría en
		// silencio, y no habría de dónde recuperarlo: lo emite el servidor una sola vez.
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);
		var cola = contexto.CrearCola();

		// El UUID se lee antes de enviar: una vez confirmada, la incidencia deja de ser
		// enviable y la cola ya no lo expone.
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
		// CA 6, en la parte que hoy se puede afirmar. Se compara el registro enviable antes y
		// después de un intento fallido: nota, kilómetro, fuente, fecha de captura y sesión de
		// origen tienen que llegar idénticos. Es lo que separa "escribir cinco columnas" de
		// "reescribir la fila", y solo se nota cuando alguien reordena el mapeo.
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

		// Y lo que sí tenía que cambiar, cambió: si no, la prueba pasaría con un envío que nunca
		// ocurrió.
		Assert.Equal(1, despues.Intentos);
	}

	[Fact]
	public async Task ElRegistroConfirmado_conservaSuTipoSuKilometroYSuPrioridad()
	{
		// CA 6 visto desde la cola: lo que el operador lee de un registro ya confirmado sigue
		// siendo lo que capturó, no un resumen recompuesto con lo que devolvió el servidor.
		await using var contexto = new ContextoSqlite();
		var clave = await contexto.CrearRepositorio().GuardarAsync(
			Objeto, Kilometer.Crear("130+200"), KilometerSource.Manual, Critica, "nota de prueba");

		await contexto.CrearSincronizador(new JacobControlado().Responde(Aceptada(Folio))).EjecutarAsync();

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal("Objeto en camino", registro.Descripcion);
		Assert.Equal("130+200", registro.Kilometro);
		Assert.Equal(SyncPriority.Critica, registro.Prioridad);

		// CA 2: la clave local no desaparece al llegar el folio; baja a línea de trazabilidad.
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
