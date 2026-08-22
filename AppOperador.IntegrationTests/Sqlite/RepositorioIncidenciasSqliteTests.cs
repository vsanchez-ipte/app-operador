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
	public async Task Guardar_devuelveClaveLocalConElFormatoDeLaMaqueta()
	{
		await using var contexto = new ContextoSqlite();

		var clave = await GuardarAsync(contexto, Advertencia);

		Assert.Matches(@"\ALOC-\d{6}\z", clave);
	}

	[Fact]
	public async Task Guardar_asignaClavesLocalesDistintasYCrecientes()
	{
		await using var contexto = new ContextoSqlite();

		var primera = await GuardarAsync(contexto, Advertencia);
		var segunda = await GuardarAsync(contexto, Advertencia);

		Assert.NotEqual(primera, segunda);
		Assert.True(string.CompareOrdinal(segunda, primera) > 0, "La clave local debe crecer.");
	}

	[Fact]
	public async Task LoGuardado_sobreviveAlReinicioDeLaAplicacion()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);

		// Instancia nueva sobre el mismo archivo: es la prueba de que persiste de verdad
		// y no solo mientras el proceso vive.
		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqlite(
			reabierta, contexto.Reloj, contexto.Conectividad,
			new BitacoraAuditoriaSqlite(reabierta, contexto.Reloj), contexto.Sesion);

		var registros = await cola.ObtenerRegistrosAsync();

		Assert.Contains(registros, r => r.ClaveLocal == clave);
		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task Guardar_derivaLaPrioridadDeLaGravedadSegunLaReglaDeDominio()
	{
		await using var contexto = new ContextoSqlite();

		await GuardarAsync(contexto, Critica);
		await GuardarAsync(contexto, Informacion);

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
		var clave = await repositorio.GuardarBorradorAsync(null, "130+", Advertencia, "");

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

		await contexto.CrearRepositorio().GuardarBorradorAsync(Objeto, null, Advertencia, "nota");

		var borrador = Assert.Single(await contexto.CrearRepositorio().ObtenerBorradoresAsync());
		Assert.Equal(EstadoSincronizacion.Borrador, borrador.Estado);
	}

	[Fact]
	public async Task Guardar_dejaLaIncidenciaPendienteYSinFolioCentral()
	{
		await using var contexto = new ContextoSqlite();

		await GuardarAsync(contexto, Advertencia);

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);
		// El folio lo asigna Jacob: antes de sincronizar no puede existir.
		Assert.Null(registro.FolioCentral);
	}

	[Fact]
	public async Task Descripcion_traeSoloElTipoSinRepetirClaseNiPrioridad()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto, Advertencia);

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());

		// La vista antepone "clase / prioridad / ..." al componer la tarjeta. Si el mapeo
		// los incluye también, salen duplicados en pantalla.
		Assert.Equal("Objeto en camino", registro.Descripcion);
		Assert.DoesNotContain("Incidencia", registro.Descripcion);
		Assert.DoesNotContain("Normal", registro.Descripcion);
	}

	// ── Ciclo de vida del borrador (JTT-1399 CA 8 y 9) ────────────────────────────────

	[Fact]
	public async Task Borrador_seReabreConLoQueSeHabiaCapturado()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await repositorio.GuardarBorradorAsync(Objeto, "130+", Advertencia, "a medias");

		var borrador = await repositorio.ObtenerBorradorAsync(clave);

		Assert.NotNull(borrador);
		Assert.Equal(Objeto.Id, borrador.TipoId);
		// El kilómetro vuelve tal cual se escribió, incompleto incluido: es lo que hace que el
		// operador pueda seguir donde se quedó en vez de volver a teclearlo.
		Assert.Equal("130+", borrador.Kilometro);
		Assert.Equal(Advertencia.Id, borrador.SeveridadId);
		Assert.Equal("a medias", borrador.Nota);
	}

	[Fact]
	public async Task Borrador_seEditaSinExigirQueEsteCompleto()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await repositorio.GuardarBorradorAsync(null, null, null, "");

		var actualizado = await repositorio.ActualizarBorradorAsync(clave, Objeto, "131+", null, "avanzando");

		Assert.True(actualizado);
		var borrador = await repositorio.ObtenerBorradorAsync(clave);
		Assert.Equal(Objeto.Id, borrador!.TipoId);
		Assert.Null(borrador.SeveridadId);
		Assert.Equal("avanzando", borrador.Nota);
	}

	[Fact]
	public async Task Borrador_eliminadoDejaDeExistir()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await repositorio.GuardarBorradorAsync(Objeto, "130+200", Advertencia, "nota");

		Assert.True(await repositorio.EliminarBorradorAsync(clave));

		Assert.Null(await repositorio.ObtenerBorradorAsync(clave));
		Assert.Empty(await repositorio.ObtenerBorradoresAsync());
	}

	[Fact]
	public async Task Convertir_dejaElBorradorComoPendienteYConservaSuClaveLocal()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await repositorio.GuardarBorradorAsync(Objeto, "130+", Advertencia, "nota");

		var convertido = await repositorio.ConvertirBorradorAsync(
			clave, Objeto, Kilometer.Crear("130+200"), KilometerSource.Manual, Critica, "nota final");

		Assert.True(convertido);

		// La misma clave que tenía como borrador: convertir es cambiar de estado, no crear otro
		// registro. Si cambiara, el operador vería desaparecer un LOC- y aparecer otro.
		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(clave, registro.ClaveLocal);
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);

		// Y deja de ser borrador: no puede estar en las dos listas a la vez.
		Assert.Empty(await repositorio.ObtenerBorradoresAsync());
	}

	[Fact]
	public async Task Convertir_recalculaLaPrioridadConLaSeveridadDefinitiva()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();

		// Nace con severidad no crítica y se confirma como crítica: la prioridad tiene que
		// seguir a la severidad con la que se confirmó, no a la que tenía a medio capturar.
		var clave = await repositorio.GuardarBorradorAsync(Objeto, "130+", Informacion, "nota");

		await repositorio.ConvertirBorradorAsync(
			clave, Objeto, Kilometer.Crear("130+200"), KilometerSource.Manual, Critica, "nota final");

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(SyncPriority.Critica, registro.Prioridad);
	}

	[Fact]
	public async Task Borrador_deOtroOperadorNoSePuedeAbrirNiEditarNiBorrarNiConvertir()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await contexto.CrearRepositorioDe(new SesionFija("otro"))
			.GuardarBorradorAsync(Objeto, "130+200", Advertencia, "trabajo ajeno");

		// El repositorio del contexto usa un operador distinto (JTT-1388 CA 9).
		var mio = contexto.CrearRepositorio();

		Assert.Null(await mio.ObtenerBorradorAsync(clave));
		Assert.False(await mio.ActualizarBorradorAsync(clave, Objeto, "131+000", Advertencia, "mío"));
		Assert.False(await mio.EliminarBorradorAsync(clave));
		Assert.False(await mio.ConvertirBorradorAsync(
			clave, Objeto, Kilometer.Crear("130+200"), KilometerSource.Manual, Critica, "mío"));
	}

	[Fact]
	public async Task Convertir_noAlcanzaAUnaIncidenciaQueYaEstaEnLaCola()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await GuardarAsync(contexto, Advertencia);

		// Ya es Pendiente, no borrador: convertirla otra vez la regresaría al principio de su
		// ciclo y podría reabrir para edición algo que quizá ya viajó a Jacob.
		var convertido = await repositorio.ConvertirBorradorAsync(
			clave, Objeto, Kilometer.Crear("131+000"), KilometerSource.Manual, Critica, "otra");

		Assert.False(convertido);
	}

	private static Task<string> GuardarAsync(ContextoSqlite contexto, SeveridadIncidencia severidad) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			severidad,
			"nota de prueba");
}
