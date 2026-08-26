using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// El envío de la cola, contra una base real y un Jacob controlable (JTT-1401).
/// </summary>
/// <remarks>
/// <para>
/// <b>Entran por <c>SincronizarIncidencias</c> y no por la cola.</b> Hasta JTT-1401 la
/// orquestación vivía dentro de <c>ColaSincronizacionSqlite</c> y estas pruebas la llamaban ahí;
/// al moverla a la capa de aplicación, el punto de entrada cambió. <b>Lo que comprueban es lo
/// mismo</b>, y siguen tocando SQLite de verdad: importa que los estados y el folio queden
/// escritos, no solo decididos.
/// </para>
/// <para>
/// Antes el envío lo simulaba la propia cola devolviendo siempre un folio inventado, así que
/// ningún rechazo era comprobable. Ahora Jacob es un doble que la prueba programa.
/// </para>
/// </remarks>
public sealed class ColaSincronizacionSqliteTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	// Niveles del catálogo real de Jacob: Crítico 1, Advertencia 2, Información 3
	// (JTT-1394). Sustituyen al enum Gravedad, que la app se inventaba.
	private static readonly SeveridadIncidencia Critica =
		new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Crítico", 1, "#EB1409");

	private static readonly SeveridadIncidencia Advertencia =
		new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Advertencia", 2, "#EDD611");

	private static readonly SeveridadIncidencia Informacion =
		new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Información", 3, "#120AF2");

	[Fact]
	public async Task Sincronizar_pasaLosPendientesASincronizadoYAsignaFolio()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Advertencia);

		var resultado = await contexto.CrearSincronizador().EjecutarAsync();

		Assert.Equal(1, resultado.Confirmados);
		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);
		Assert.False(string.IsNullOrWhiteSpace(registro.FolioCentral));
	}

	[Fact]
	public async Task Sincronizar_dejaElContadorDePendientesEnCero()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Advertencia);

		await contexto.CrearSincronizador().EjecutarAsync();

		Assert.Equal(0, await contexto.CrearCola().ContarPendientesAsync());
	}

	[Fact]
	public async Task Sincronizar_atiendePrimeroLasCriticasAunqueSeanMasRecientes()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Informacion);
		contexto.Reloj.Avanzar(TimeSpan.FromMinutes(5));
		await GuardarAsync(contexto, Critica);

		var jacob = new JacobControlado();
		await contexto.CrearSincronizador(jacob).EjecutarAsync();

		// El orden en que Jacob las recibió es el orden en que se atendieron.
		Assert.Equal(2, jacob.Recibidos.Count);
		var primera = jacob.Recibidos[0];
		Assert.Equal(Critica.Id, primera.IdGravedad);
	}

	[Fact]
	public async Task Sincronizar_sinConexion_noCambiaNingunEstado()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Advertencia);
		contexto.Conectividad.HayEnlace = false;

		var resultado = await contexto.CrearSincronizador().EjecutarAsync();

		Assert.Equal(MotivoNoSincroniza.SinEnlaceConJacob, resultado.MotivoBloqueo);
		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);
	}

	[Fact]
	public async Task Sincronizar_sinConexion_niSiquieraLlamaAJacob()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Advertencia);
		contexto.Conectividad.HayEnlace = false;

		var jacob = new JacobControlado();
		await contexto.CrearSincronizador(jacob).EjecutarAsync();

		// La compuerta es lo que evita quemar batería intentando contra una red que no lleva
		// a ninguna parte (CA 2).
		Assert.Empty(jacob.Recibidos);
	}

	[Fact]
	public async Task Sincronizar_noReenviaLoQueYaEstaSincronizado()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Advertencia);

		var jacob = new JacobControlado();
		await contexto.CrearSincronizador(jacob).EjecutarAsync();
		await contexto.CrearSincronizador(jacob).EjecutarAsync();

		// Sincronizado es terminal: la segunda pasada no tiene nada que hacer.
		Assert.Single(jacob.Recibidos);
	}

	[Fact]
	public async Task Sincronizar_ignoraLosBorradores()
	{
		await using var contexto = new ContextoSqlite();
		await contexto.CrearRepositorio().GuardarBorradorAsync(Objeto, "130+200", Advertencia, "nota");

		var jacob = new JacobControlado();
		var resultado = await contexto.CrearSincronizador(jacob).EjecutarAsync();

		// CA 11: un borrador no se envía nunca, ni siquiera por error de filtro.
		Assert.Empty(jacob.Recibidos);
		Assert.Equal(0, resultado.Confirmados);
		Assert.Single(await contexto.CrearRepositorio().ObtenerBorradoresAsync());
	}

	[Fact]
	public async Task LaColaSincronizada_sobreviveAlReinicioDeLaAplicacion()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);
		await contexto.CrearSincronizador().EjecutarAsync();

		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqlite(reabierta, contexto.Reloj, contexto.Sesion);

		var registro = Assert.Single(
			await cola.ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);

		await reabierta.DisposeAsync();
	}

	// ── El envío que quedó a medias ───────────────────────────────────────────────────
	//
	// Un registro entra en Enviando justo antes de la llamada a Jacob. Si el proceso muere ahí
	// —la app se cierra, se apaga el teléfono, el sistema la mata— nadie escribe el estado
	// final. Estas tres pruebas cubren lo que pasaba entonces: quedaba fuera de la cola, fuera
	// del contador y sin nadie que lo reintentara, visible como «ENVIANDO» para siempre.

	[Fact]
	public async Task UnEnvioInterrumpido_vuelveAPendienteYSeSincroniza()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);
		await DejarEnviandoAsync(contexto, clave);

        var resultado = await contexto.CrearSincronizador().EjecutarAsync();

		Assert.Equal(1, resultado.Confirmados);
		var registro = Assert.Single(
			await contexto.CrearCola().ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal(EstadoSincronizacion.Sincronizado, registro.Estado);
		Assert.False(string.IsNullOrWhiteSpace(registro.FolioCentral));
	}

	[Fact]
	public async Task UnEnvioInterrumpido_cuentaComoSinEnviar()
	{
		// El contador lo dejaba fuera, así que la pantalla decía menos de lo que había. Es la
		// peor dirección para equivocarse en una cola: el operador cree que ya salió todo.
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);
		await DejarEnviandoAsync(contexto, clave);

		Assert.Equal(1, await contexto.CrearCola().ContarPendientesAsync());
	}

	[Fact]
	public async Task UnEnvioInterrumpido_noSeLoLlevaElCierreDeLaApp()
	{
		// La recuperación tiene que servir también al caso real: la app se cerró, se vuelve a
		// abrir y la base ya venía con el registro colgado de la sesión anterior.
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);
		await DejarEnviandoAsync(contexto, clave);

		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqlite(reabierta, contexto.Reloj, contexto.Sesion);

		var recuperados = await cola.RecuperarEnviosInterrumpidosAsync();

		Assert.Equal(1, recuperados);
		var registro = Assert.Single(
			await cola.ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);

		await reabierta.DisposeAsync();
	}

	private static Task<string> GuardarAsync(ContextoSqlite contexto, SeveridadIncidencia severidad) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			severidad,
			"nota de prueba");

	/// <summary>Deja el registro en Enviando, como si el proceso hubiera muerto a media llamada.</summary>
	private static async Task DejarEnviandoAsync(ContextoSqlite contexto, string clave)
	{
		var cola = contexto.CrearCola();
		var enviable = await cola.ObtenerEnviablePorClaveAsync(clave);

		await cola.ActualizarEnvioAsync(new ActualizacionEnvio(
			enviable!.Uuid, EstadoSincronizacion.Enviando, 1, null, null));
	}
}
