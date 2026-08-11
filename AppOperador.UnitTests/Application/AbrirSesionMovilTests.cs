using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Los dos pasos del acceso y la apertura de la sesión móvil (JTT-1382).
/// </summary>
public class AbrirSesionMovilTests
{
	private static readonly DateTime Validacion = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);
	private static readonly UnidadVehicular Unidad = new("u-1", "VEH-01", "Camioneta 01");

	private readonly IAccesoJacobClient _jacob = Substitute.For<IAccesoJacobClient>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();

	private AbrirSesionMovil CrearCasoDeUso() =>
		new(_jacob, _tokens, _sesiones, new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)));

	private static SesionValidada SesionDePrueba(string token = "jwt-de-prueba") =>
		new(
			sessionId: "s-1",
			accessToken: token,
			tokenExpiraUtc: Validacion.AddHours(8),
			operador: "Juan Pérez",
			rol: "Operador de campo",
			unidad: Unidad,
			permisos: PermisosOperador.DelServidor(["APP_OPERADOR_MOVIL"]),
			vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
			horaServidorUtc: Validacion);

	private void ConDesafioEmitido(string desafio = "d-1") =>
		_jacob.PreautenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoPreauth.Emitido(desafio, Validacion.AddMinutes(5), [Unidad]));

	// ---------- Paso 1 ----------

	[Fact]
	public async Task Identificar_DevuelveElResultadoDelCliente()
	{
		ConDesafioEmitido();

		var resultado = await CrearCasoDeUso().IdentificarAsync("op@ipte.com.mx", "secreta");

		Assert.True(resultado.Exitoso);
		Assert.Single(resultado.Unidades);
	}

	[Fact]
	public async Task Identificar_NoGuardaNadaNiAbreSesion()
	{
		// El paso 1 no crea sesión: solo obtiene el desafío.
		ConDesafioEmitido();

		await CrearCasoDeUso().IdentificarAsync("op@ipte.com.mx", "secreta");

		await _tokens.DidNotReceive().GuardarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		_sesiones.DidNotReceive().Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task Identificar_ConRechazo_DejaElAccesoSinDesafioUtilizable()
	{
		// Si el operador se equivocó de cuenta, el desafío de la anterior no puede seguir
		// sirviendo para abrir sesión.
		var casoDeUso = CrearCasoDeUso();
		ConDesafioEmitido();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		_jacob.PreautenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoPreauth.Rechazado(MotivoRechazoAcceso.CredencialInvalida));
		await casoDeUso.IdentificarAsync("otro@ipte.com.mx", "mala");

		var resultado = await casoDeUso.AbrirAsync(Unidad);

		Assert.Equal(MotivoRechazoAcceso.DesafioNoValido, resultado.Motivo);
		await _jacob.DidNotReceive().CompletarAccesoAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	// ---------- Paso 2 ----------

	[Fact]
	public async Task Abrir_EnviaElDesafioYElIdentificadorTecnicoDeLaUnidad()
	{
		// JTT-1381 CA 6: viaja el id, no la clave visible.
		ConDesafioEmitido("d-42");
		_jacob.CompletarAccesoAsync("d-42", "u-1", Arg.Any<CancellationToken>())
			.Returns(ResultadoLogin.Creada(SesionDePrueba()));
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		var resultado = await casoDeUso.AbrirAsync(Unidad);

		Assert.True(resultado.Exitoso);
		await _jacob.Received(1).CompletarAccesoAsync("d-42", "u-1", Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Abrir_SinHaberseIdentificado_NoLlamaAlApi()
	{
		var resultado = await CrearCasoDeUso().AbrirAsync(Unidad);

		Assert.Equal(MotivoRechazoAcceso.DesafioNoValido, resultado.Motivo);
		await _jacob.DidNotReceive().CompletarAccesoAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Abrir_ConExito_GuardaElTokenYPublicaLaSesion()
	{
		ConDesafioEmitido();
		_jacob.CompletarAccesoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoLogin.Creada(SesionDePrueba("jwt-real")));
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		await casoDeUso.AbrirAsync(Unidad);

		await _tokens.Received(1).GuardarAsync("jwt-real", Arg.Any<CancellationToken>());
		_sesiones.Received(1).Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task Abrir_ConExito_LaSesionLlevaLoQueMuestranInicioYPerfil()
	{
		ConDesafioEmitido();
		_jacob.CompletarAccesoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoLogin.Creada(SesionDePrueba()));
		SesionOperador? guardada = null;
		_sesiones.When(s => s.Guardar(Arg.Any<SesionOperador>())).Do(c => guardada = c.Arg<SesionOperador>());
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		await casoDeUso.AbrirAsync(Unidad);

		Assert.NotNull(guardada);
		Assert.Equal("Juan Pérez", guardada.Operador);
		Assert.Equal("Operador de campo", guardada.Rol);
		Assert.Equal("VEH-01", guardada.UnidadVehicular);
		Assert.Equal(["APP_OPERADOR_MOVIL"], guardada.Permisos);
		Assert.Equal("1.2.0", guardada.VersionAplicacion);
	}

	[Fact]
	public async Task Abrir_ConExito_ConservaLaVentanaOfflineDelServidorSinRecalcularla()
	{
		// CA 3 y CA 4: la ventana la calcula Jacob. Si el caso de uso la rehiciera contra el
		// reloj del dispositivo, un telefono desfasado daria una vigencia distinta.
		var delServidor = VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(6));
		ConDesafioEmitido();
		_jacob.CompletarAccesoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoLogin.Creada(new SesionValidada(
				"s-1", "jwt", Validacion.AddHours(6), "Juan Pérez", "Operador de campo",
				Unidad, PermisosOperador.DelServidor(["APP_OPERADOR_MOVIL"]), delServidor, Validacion)));
		SesionOperador? guardada = null;
		_sesiones.When(s => s.Guardar(Arg.Any<SesionOperador>())).Do(c => guardada = c.Arg<SesionOperador>());
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		await casoDeUso.AbrirAsync(Unidad);

		Assert.Equal(Validacion.AddHours(6), guardada!.Vigencia.OfflineUntilUtc);
	}

	[Fact]
	public async Task Abrir_ConRechazo_NoGuardaTokenNiSesion()
	{
		// Sin sesión no puede quedar rastro de una sesión: seria mentirle al resto de la app.
		ConDesafioEmitido();
		_jacob.CompletarAccesoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoLogin.Rechazado(MotivoRechazoAcceso.UnidadNoAutorizada, "appoperador.vehiculo.noautorizado"));
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		var resultado = await casoDeUso.AbrirAsync(Unidad);

		Assert.False(resultado.Exitoso);
		await _tokens.DidNotReceive().GuardarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		_sesiones.DidNotReceive().Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task Abrir_ElDesafioEsDeUnSoloUso_AunqueElIntentoFalle()
	{
		// El servidor ya lo consumio: reintentar con el mismo solo daria desafio.consumido.
		ConDesafioEmitido("d-7");
		_jacob.CompletarAccesoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoLogin.Rechazado(MotivoRechazoAcceso.UnidadNoAutorizada));
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");
		await casoDeUso.AbrirAsync(Unidad);

		var segundo = await casoDeUso.AbrirAsync(Unidad);

		Assert.Equal(MotivoRechazoAcceso.DesafioNoValido, segundo.Motivo);
		await _jacob.Received(1).CompletarAccesoAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Descartar_OlvidaElDesafio()
	{
		ConDesafioEmitido();
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		casoDeUso.Descartar();
		var resultado = await casoDeUso.AbrirAsync(Unidad);

		Assert.Equal(MotivoRechazoAcceso.DesafioNoValido, resultado.Motivo);
	}

	[Fact]
	public async Task DosPantallasDeAcceso_NoCompartenElDesafio()
	{
		// Por eso el caso de uso se registra como transitorio.
		ConDesafioEmitido();
		var primera = CrearCasoDeUso();
		await primera.IdentificarAsync("op@ipte.com.mx", "secreta");

		var segunda = CrearCasoDeUso();
		var resultado = await segunda.AbrirAsync(Unidad);

		Assert.Equal(MotivoRechazoAcceso.DesafioNoValido, resultado.Motivo);
	}
}
