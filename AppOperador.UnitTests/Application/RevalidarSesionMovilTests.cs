using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Revalidar la sesión al recuperar el enlace (JTT-1383 CA 9, 10 y 11).
/// </summary>
public class RevalidarSesionMovilTests
{
	private const string Modulo = "APP_OPERADOR_MOVIL";
	private const string Token = "jwt-de-la-sesion";

	private static readonly DateTime Validacion = new(2026, 8, 11, 8, 0, 0, DateTimeKind.Utc);
	private static readonly UnidadVehicular Unidad = new("u-1", "VEH-01", "Camioneta 01");

	private readonly IAccesoJacobClient _jacob = Substitute.For<IAccesoJacobClient>();
	private readonly IOfflineSessionStore _persistida = Substitute.For<IOfflineSessionStore>();
	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly ITokenClaims _claims = Substitute.For<ITokenClaims>();
	private readonly ISyncQueueService _cola = Substitute.For<ISyncQueueService>();
	private readonly IAuditLog _bitacora = Substitute.For<IAuditLog>();
	private readonly MonotonicoFalso _monotonico = new() { Transcurrido = TimeSpan.FromHours(9) };

	public RevalidarSesionMovilTests()
	{
		_tokens.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Token);
		_claims.ModulosDe(Arg.Any<string>()).Returns([Modulo]);
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Guardada());
	}

	private readonly AvisoDeSesionTerminada _aviso = new();

	private RevalidarSesionMovil Crear() =>
		new(_jacob, new CustodiaSesionLocal(_sesiones, _tokens, _persistida), _aviso,
			_claims, _monotonico, _cola, _bitacora);

	private static SesionOfflinePersistida Guardada() => new(
		SessionId: "s-1",
		Operador: "Juan Pérez",
		Rol: "Operador de campo",
		Unidad: Unidad,
		Permisos: PermisosOperador.DelServidor([Modulo]),
		Vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
		MonotonicoAlValidar: TimeSpan.FromHours(3),
		Instalacion: new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)));

	private void JacobConfirma(
		PermisosOperador? permisos = null,
		UnidadVehicular? unidad = null,
		DateTime? nuevaValidacion = null)
	{
		var validado = nuevaValidacion ?? Validacion.AddHours(6);

		_jacob.RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoRevalidacion.Confirmada(
				"Operador de campo",
				unidad ?? Unidad,
				permisos ?? PermisosOperador.DelServidor([Modulo]),
				VigenciaOffline.DelServidor(validado, validado.AddHours(8))));
	}

	// ---------- CA 10: al confirmar, se adopta lo que devuelve Jacob ----------

	[Fact]
	public async Task Al_confirmar_renueva_la_ventana_offline()
	{
		var nueva = Validacion.AddHours(6);
		JacobConfirma(nuevaValidacion: nueva);
		SesionOfflinePersistida? guardada = null;
		await _persistida.GuardarAsync(
			Arg.Do<SesionOfflinePersistida>(s => guardada = s), Arg.Any<CancellationToken>());

		await Crear().RevalidarAsync();

		Assert.Equal(nueva.AddHours(8), guardada!.Vigencia.OfflineUntilUtc);
	}

	[Fact]
	public async Task Al_confirmar_actualiza_la_referencia_monotonica()
	{
		// Sin esto, el transcurso se seguiría contando desde el acceso original y la
		// ventana nueva nacería medio consumida.
		JacobConfirma();
		SesionOfflinePersistida? guardada = null;
		await _persistida.GuardarAsync(
			Arg.Do<SesionOfflinePersistida>(s => guardada = s), Arg.Any<CancellationToken>());

		await Crear().RevalidarAsync();

		Assert.Equal(TimeSpan.FromHours(9), guardada!.MonotonicoAlValidar);
	}

	[Fact]
	public async Task Al_confirmar_adopta_la_unidad_vigente()
	{
		var otra = new UnidadVehicular("u-2", "VEH-09", "Camioneta 09");
		JacobConfirma(unidad: otra);
		SesionOperador? publicada = null;
		_sesiones.When(s => s.Guardar(Arg.Any<SesionOperador>()))
			.Do(c => publicada = c.Arg<SesionOperador>());

		await Crear().RevalidarAsync();

		Assert.Equal("VEH-09", publicada!.UnidadVehicular);
	}

	[Fact]
	public async Task Al_confirmar_inicia_la_sincronizacion()
	{
		JacobConfirma();

		await Crear().RevalidarAsync();

		await _cola.Received(1).SincronizarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Un_fallo_al_sincronizar_no_deshace_la_revalidacion()
	{
		JacobConfirma();
		_cola.SincronizarAsync(Arg.Any<CancellationToken>())
			.Returns<Task<int>>(_ => throw new InvalidOperationException("base ocupada"));

		var resultado = await Crear().RevalidarAsync();

		Assert.True(resultado.Exitoso);
		_sesiones.Received(1).Guardar(Arg.Any<SesionOperador>());
	}

	// ---------- CA 11: si Jacob niega la sesión ----------

	[Fact]
	public async Task Si_Jacob_niega_la_sesion_bloquea_y_pide_autenticacion()
	{
		_jacob.RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoRevalidacion.Negada(
				MotivoRechazoAcceso.SesionRevocada, "appoperador.sesion.revocada"));

		var resultado = await Crear().RevalidarAsync();

		Assert.True(resultado.EsRechazoDefinitivo);
		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
		await _persistida.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Si_Jacob_niega_la_sesion_deja_el_motivo_para_la_pantalla_de_acceso()
	{
		// Sin esto el operador aparece en el formulario sin explicacion (CA 11).
		_jacob.RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoRevalidacion.Negada(MotivoRechazoAcceso.SesionRevocada));

		await Crear().RevalidarAsync();

		Assert.Equal(MotivoRechazoAcceso.SesionRevocada, _aviso.Consumir());
	}

	[Fact]
	public async Task Si_Jacob_niega_la_sesion_no_toca_la_cola()
	{
		// Lo capturado en campo es del operador y se envía cuando alguien vuelva a entrar.
		_jacob.RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoRevalidacion.Negada(MotivoRechazoAcceso.SinPermiso));

		await Crear().RevalidarAsync();

		await _cola.DidNotReceive().SincronizarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Si_los_permisos_devueltos_exceden_al_token_se_trata_como_negativa()
	{
		JacobConfirma(PermisosOperador.DelServidor([Modulo, "ADMINISTRAR"]));

		var resultado = await Crear().RevalidarAsync();

		Assert.True(resultado.EsRechazoDefinitivo);
		_sesiones.Received(1).Limpiar();
	}

	// ---------- Sin respuesta: la sesión offline sigue ----------

	[Fact]
	public async Task Si_no_hay_comunicacion_la_sesion_offline_sigue_intacta()
	{
		// La diferencia que importa: no poder preguntar no es que le hayan dicho que no.
		_jacob.RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(ResultadoRevalidacion.SinRespuesta("conexion.fallida"));

		var resultado = await Crear().RevalidarAsync();

		Assert.False(resultado.Exitoso);
		Assert.False(resultado.EsRechazoDefinitivo);
		_sesiones.DidNotReceive().Limpiar();
		await _persistida.DidNotReceive().LimpiarAsync(Arg.Any<CancellationToken>());
		await _tokens.DidNotReceive().LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Sin_sesion_guardada_no_se_pregunta_nada()
	{
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>()).Returns((SesionOfflinePersistida?)null);

		var resultado = await Crear().RevalidarAsync();

		Assert.False(resultado.Exitoso);
		await _jacob.DidNotReceive().RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Sin_token_no_se_pregunta_nada()
	{
		_tokens.ObtenerAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

		await Crear().RevalidarAsync();

		await _jacob.DidNotReceive().RevalidarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	private sealed class MonotonicoFalso : IMonotonicClock
	{
		public TimeSpan Transcurrido { get; set; }
	}
}
