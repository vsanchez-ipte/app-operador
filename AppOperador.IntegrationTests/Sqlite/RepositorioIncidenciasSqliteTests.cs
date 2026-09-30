using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class RepositorioIncidenciasSqliteTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	// Niveles del catálogo real de Jacob: Crítico 1, Advertencia 2, Información 3.
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

		// Instancia nueva sobre el mismo archivo: persiste de verdad, no solo en memoria.
		var reabierta = contexto.ReabrirBaseDatos();
		var cola = new ColaSincronizacionSqlite(reabierta, contexto.Reloj, contexto.Sesion);

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

		Assert.Equal("Objeto en camino", registro.Descripcion);
		Assert.DoesNotContain("Incidencia", registro.Descripcion);
		Assert.DoesNotContain("Normal", registro.Descripcion);
	}

	[Fact]
	public async Task GuardarDesdeGps_conservaMetrosYPosicionOriginalParaAuditoria()
	{
		await using var contexto = new ContextoSqlite();
		var posicion = PosicionDispositivo.Crear(
			32.5275769,
			-116.6861878,
			8.5,
			new DateTime(2026, 8, 24, 20, 15, 0, DateTimeKind.Utc));

		await contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			Advertencia,
			"nota de prueba",
			posicionGps: posicion);

		var enviable = Assert.Single(await contexto.CrearCola().ObtenerEnviablesAsync());

		Assert.Equal(130_200, enviable.KilometroMetros);
		Assert.Equal(posicion, enviable.PosicionGps);
	}

	[Fact]
	public async Task GuardarManual_noConservaUnaPosicionGps()
	{
		await using var contexto = new ContextoSqlite();
		var posicionAccidental = PosicionDispositivo.Crear(
			32.5275769,
			-116.6861878,
			8,
			new DateTime(2026, 8, 24, 20, 15, 0, DateTimeKind.Utc));

		await contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.Manual,
			Advertencia,
			"nota de prueba",
			posicionGps: posicionAccidental);

		var enviable = Assert.Single(await contexto.CrearCola().ObtenerEnviablesAsync());

		Assert.Equal(130_200, enviable.KilometroMetros);
		Assert.Null(enviable.PosicionGps);
	}

	// ── Persistencia de la cola local ──────────────────────────

	[Fact]
	public async Task CerrarSesion_noEliminaLoPendienteNiLosBorradores()
	{
		await using var contexto = new ContextoSqlite();
		var clavePendiente = await GuardarAsync(contexto, Advertencia);
		var claveBorrador = await contexto.CrearRepositorio()
			.GuardarBorradorAsync(Objeto, "130+", Advertencia, "a medias");

		contexto.Sesion.Limpiar();

		var reabierta = contexto.ReabrirBaseDatos();
		var sesionNueva = new SesionFija();
		var cola = new ColaSincronizacionSqlite(reabierta, contexto.Reloj, sesionNueva);
		var repositorio = new RepositorioIncidenciasSqlite(reabierta, contexto.Reloj, sesionNueva);

		Assert.Contains(await cola.ObtenerRegistrosAsync(), r => r.ClaveLocal == clavePendiente);
		Assert.Contains(await repositorio.ObtenerBorradoresAsync(), r => r.ClaveLocal == claveBorrador);

		await reabierta.DisposeAsync();
	}

	[Fact]
	public async Task SinSesion_laColaNoDevuelveNadaPeroNoSeHaBorradoNada()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);

		contexto.Sesion.Limpiar();

		Assert.Empty(await contexto.CrearCola().ObtenerRegistrosAsync());

		var conSesion = contexto.CrearColaDe(new SesionFija());
		Assert.Contains(await conSesion.ObtenerRegistrosAsync(), r => r.ClaveLocal == clave);
	}

	// ── Ciclo de vida del borrador ────────────────────────────────

	[Fact]
	public async Task Borrador_seReabreConLoQueSeHabiaCapturado()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await repositorio.GuardarBorradorAsync(Objeto, "130+", Advertencia, "a medias");

		var borrador = await repositorio.ObtenerBorradorAsync(clave);

		Assert.NotNull(borrador);
		Assert.Equal(Objeto.Id, borrador.TipoId);
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

		// El repositorio del contexto usa un operador distinto.
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

		var convertido = await repositorio.ConvertirBorradorAsync(
			clave, Objeto, Kilometer.Crear("131+000"), KilometerSource.Manual, Critica, "otra");

		Assert.False(convertido);
	}

	// ---------- Corregir un rechazo ----------

	[Fact]
	public async Task Rechazada_seAbreConSusDatosYConElMotivoQueDioJacob()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);
		await contexto.CrearSincronizador(new JacobControlado().RechazaFuncional()).EjecutarAsync();

		var rechazada = await contexto.CrearRepositorio().ObtenerRechazadaAsync(clave);

		Assert.NotNull(rechazada);
		Assert.Equal(clave, rechazada.ClaveLocal);
		Assert.Equal(Objeto.Id, rechazada.TipoId);
		Assert.Equal(Advertencia.Id, rechazada.SeveridadId);
		Assert.Equal("130+200", rechazada.Kilometro);
		Assert.Equal("nota de prueba", rechazada.Nota);
		// El motivo no vive en la incidencia sino en la bitácora de intentos: hay que traerlo.
		Assert.Equal("appincidencias.nota.requerida", rechazada.UltimoErrorCodigo);
		Assert.Equal("Rechazo de prueba.", rechazada.UltimoErrorMensaje);
	}

	[Fact]
	public async Task Corregir_vuelveAPendienteConservandoClaveYUuidYSinArrastrarElRechazo()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await GuardarAsync(contexto, Advertencia);
		var jacob = new JacobControlado().RechazaFuncional();
		await contexto.CrearSincronizador(jacob).EjecutarAsync();
		var uuidOriginal = Assert.Single(jacob.Recibidos).Uuid;

		var corregida = await repositorio.CorregirRechazadaAsync(
			clave, Objeto, Kilometer.Crear("131+000"), KilometerSource.Manual, Critica, "nota corregida");

		Assert.True(corregida);
		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		// Misma clave: corregir es cambiar de estado, no crear otro registro.
		Assert.Equal(clave, registro.ClaveLocal);
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);
		Assert.Equal("131+000", registro.Kilometro);
		Assert.Equal(SyncPriority.Critica, registro.Prioridad);
		// Para el operador es un envío nuevo: no arrastra el código del rechazo ni su cuenta.
		Assert.Null(registro.UltimoErrorCodigo);
		Assert.Equal(0, registro.Intentos);
		Assert.False(registro.SePuedeCorregir);

		await contexto.CrearSincronizador(jacob).EjecutarAsync();
		Assert.Equal(2, jacob.Recibidos.Count);
		Assert.Equal(uuidOriginal, jacob.Recibidos[1].Uuid);
		Assert.Equal(131.000m, jacob.Recibidos[1].Km);
		Assert.Equal(EstadoSincronizacion.Sincronizado,
			Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync()).Estado);
	}

	[Fact]
	public async Task Corregir_noAlcanzaAUnRegistroQueNoEstaFallido()
	{
		await using var contexto = new ContextoSqlite();
		var repositorio = contexto.CrearRepositorio();
		var clave = await GuardarAsync(contexto, Advertencia);

		// Está Pendiente: reabrirlo para edición podría tocar algo que ya viajó a Jacob.
		Assert.Null(await repositorio.ObtenerRechazadaAsync(clave));
		Assert.False(await repositorio.CorregirRechazadaAsync(
			clave, Objeto, Kilometer.Crear("131+000"), KilometerSource.Manual, Critica, "otra"));
	}

	[Fact]
	public async Task Rechazada_deOtroOperadorNoSePuedeAbrirNiCorregir()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Advertencia);
		await contexto.CrearSincronizador(new JacobControlado().RechazaFuncional()).EjecutarAsync();

		var otro = contexto.CrearRepositorioDe(new SesionFija("otro"));

		Assert.Null(await otro.ObtenerRechazadaAsync(clave));
		Assert.False(await otro.CorregirRechazadaAsync(
			clave, Objeto, Kilometer.Crear("131+000"), KilometerSource.Manual, Critica, "ajena"));
	}

	[Fact]
	public async Task ElRegistroDeLaCola_traeLaHoraDeCaptura()
	{
		// La hora que se muestra es la de captura, no la del último cambio.
		await using var contexto = new ContextoSqlite();
		var capturada = contexto.Reloj.UtcAhora;
		await GuardarAsync(contexto, Advertencia);
		contexto.Reloj.Avanzar(TimeSpan.FromHours(3));
		await contexto.CrearSincronizador(new JacobControlado().RechazaTecnico()).EjecutarAsync();

		var registro = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());

		Assert.Equal(capturada, registro.CapturadaUtc);
		Assert.NotEqual(registro.UltimoIntentoUtc, registro.CapturadaUtc);
	}

	private static Task<string> GuardarAsync(ContextoSqlite contexto, SeveridadIncidencia severidad) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			severidad,
			"nota de prueba");
}
