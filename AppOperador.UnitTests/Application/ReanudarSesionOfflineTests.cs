using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Reanudar una sesión guardada sin conexión (JTT-1383).
/// </summary>
public class ReanudarSesionOfflineTests
{
	private const string Modulo = "APP_OPERADOR_MOVIL";
	private const string Token = "jwt-de-la-sesion";

	private static readonly DateTime Validacion = new(2026, 8, 11, 8, 0, 0, DateTimeKind.Utc);
	private static readonly TimeSpan MonotonicoAlValidar = TimeSpan.FromHours(3);

	private readonly IOfflineSessionStore _persistida = Substitute.For<IOfflineSessionStore>();
	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly ITokenClaims _claims = Substitute.For<ITokenClaims>();
	private readonly IAuditLog _bitacora = Substitute.For<IAuditLog>();
	private readonly RelojFalso _reloj = new();
	private readonly MonotonicoFalso _monotonico = new();

	public ReanudarSesionOfflineTests()
	{
		_tokens.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Token);
		_claims.ModulosDe(Arg.Any<string>()).Returns([Modulo]);
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Guardada());

		// Dos horas después de validar, con las dos señales de acuerdo.
		_reloj.UtcAhora = Validacion.AddHours(2);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(2);
	}

	private ReanudarSesionOffline Crear() =>
		new(new CustodiaSesionLocal(_sesiones, _tokens, _persistida),
			_sesiones, _claims, _reloj, _monotonico, _bitacora);

	private static SesionOfflinePersistida Guardada(PermisosOperador? permisos = null) => new(
		SessionId: "s-1",
		Operador: "Juan Pérez",
		Rol: "Operador de campo",
		Unidad: new UnidadVehicular("u-1", "VEH-01", "Camioneta 01"),
		Permisos: permisos ?? PermisosOperador.DelServidor([Modulo]),
		Vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
		MonotonicoAlValidar: MonotonicoAlValidar,
		Instalacion: new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)));

	// ---------- CA 6: dentro de la ventana se reanuda ----------

	[Fact]
	public async Task Dentro_de_la_ventana_autoriza_y_publica_la_sesion()
	{
		var resultado = await Crear().ReanudarAsync();

		Assert.True(resultado.Autorizado);
		Assert.Equal("Juan Pérez", resultado.Sesion!.Operador);
		Assert.Equal("VEH-01", resultado.Sesion.UnidadVehicular);
		_sesiones.Received(1).Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task No_renueva_la_vigencia_al_reanudar()
	{
		// CA 15: solo el servidor puede correr la ventana.
		SesionOperador? publicada = null;
		_sesiones.When(s => s.Guardar(Arg.Any<SesionOperador>()))
			.Do(c => publicada = c.Arg<SesionOperador>());

		await Crear().ReanudarAsync();

		Assert.Equal(Validacion.AddHours(8), publicada!.Vigencia.OfflineUntilUtc);
		Assert.Equal(Validacion, publicada.Vigencia.LastValidatedAtUtc);
	}

	// ---------- CA 1: sin validación previa no hay modo offline ----------

	[Fact]
	public async Task Sin_sesion_guardada_rechaza()
	{
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>()).Returns((SesionOfflinePersistida?)null);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
		Assert.Equal(MotivoRechazoAcceso.SesionOfflineExpirada, resultado.Motivo);
		_sesiones.DidNotReceive().Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task Sin_token_guardado_rechaza()
	{
		_tokens.ObtenerAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
	}

	// ---------- CA 2: fuera de la ventana no se reanuda ----------

	[Fact]
	public async Task Pasadas_las_ocho_horas_rechaza()
	{
		_reloj.UtcAhora = Validacion.AddHours(9);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
		Assert.Equal(MotivoRechazoAcceso.SesionOfflineExpirada, resultado.Motivo);
	}

	// ---------- CA 5, 13 y 14: el reloj no manda solo ----------

	[Fact]
	public async Task Atrasar_el_reloj_no_reabre_una_ventana_vencida()
	{
		// El operador atrasa el telefono para seguir trabajando; el contador monotonico
		// dice que ya pasaron nueve horas.
		_reloj.UtcAhora = Validacion.AddHours(-5);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
	}

	[Fact]
	public async Task Atrasar_el_reloj_dentro_de_la_ventana_reanuda_y_queda_asentado()
	{
		_reloj.UtcAhora = Validacion.AddHours(-5);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(2);

		var resultado = await Crear().ReanudarAsync();

		Assert.True(resultado.Autorizado);
		await _bitacora.Received().RegistrarAsync(
			NivelAuditoria.Advertencia,
			Arg.Is<string>(m => m != null && m.Contains("reloj", StringComparison.OrdinalIgnoreCase)),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Reiniciar_el_dispositivo_no_reabre_una_ventana_vencida()
	{
		// El contador arranca de cero; el reloj sigue sirviendo y dice que pasaron diez horas.
		_reloj.UtcAhora = Validacion.AddHours(10);
		_monotonico.Transcurrido = TimeSpan.FromMinutes(1);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
	}

	[Fact]
	public async Task Si_no_se_puede_medir_el_tiempo_no_se_reanuda()
	{
		// Reloj atrasado y equipo reiniciado a la vez.
		_reloj.UtcAhora = Validacion.AddHours(-3);
		_monotonico.Transcurrido = TimeSpan.FromMinutes(1);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
	}

	// ---------- Permisos: se cotejan otra vez contra el token ----------

	[Fact]
	public async Task Si_los_permisos_guardados_exceden_al_token_rechaza_y_borra_la_sesion()
	{
		// El archivo local es editable; el token va firmado (JTT-1379 CA 8).
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>())
			.Returns(Guardada(PermisosOperador.DelServidor([Modulo, "ADMINISTRAR"])));

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
		await _persistida.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
		_sesiones.DidNotReceive().Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task Un_token_ilegible_no_respalda_los_permisos()
	{
		_claims.ModulosDe(Arg.Any<string>()).Returns([]);

		var resultado = await Crear().ReanudarAsync();

		Assert.False(resultado.Autorizado);
	}

	// ---------- Bitácora ----------

	[Fact]
	public async Task Deja_constancia_de_haber_entrado_en_modo_offline()
	{
		await Crear().ReanudarAsync();

		await _bitacora.Received().RegistrarAsync(
			NivelAuditoria.Advertencia,
			Arg.Is<string>(m => m != null && m.Contains("offline", StringComparison.OrdinalIgnoreCase)),
			Arg.Any<CancellationToken>());
	}

	private sealed class RelojFalso : IClock
	{
		public DateTime UtcAhora { get; set; } = DateTime.UnixEpoch;
	}

	private sealed class MonotonicoFalso : IMonotonicClock
	{
		public TimeSpan Transcurrido { get; set; }
	}
}
