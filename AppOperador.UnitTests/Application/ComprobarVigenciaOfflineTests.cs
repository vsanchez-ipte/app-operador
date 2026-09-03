using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Vigilar la ventana offline mientras el operador trabaja (JTT-1384).
/// </summary>
/// <remarks>
/// El hueco que cubre: la reanudación comprueba la vigencia al entrar, pero una vez dentro
/// nadie la volvía a mirar. Quien entrara con siete horas y media consumidas seguía
/// capturando indefinidamente.
/// </remarks>
public class ComprobarVigenciaOfflineTests
{
	private static readonly DateTime Validacion = new(2026, 8, 12, 8, 0, 0, DateTimeKind.Utc);
	private static readonly TimeSpan MonotonicoAlValidar = TimeSpan.FromHours(3);

	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly IOfflineSessionStore _persistida = Substitute.For<IOfflineSessionStore>();
	private readonly IAuditLog _bitacora = Substitute.For<IAuditLog>();
	private readonly AvisoDeSesionTerminada _aviso = new();
	private readonly RelojFalso _reloj = new();
	private readonly MonotonicoFalso _monotonico = new();

	public ComprobarVigenciaOfflineTests()
	{
		_sesiones.Actual.Returns(Guardada().ComoSesionOperador());
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Guardada());

		// Dos horas dentro de la ventana, con las dos señales de acuerdo.
		_reloj.UtcAhora = Validacion.AddHours(2);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(2);
	}

	private ComprobarVigenciaOffline Crear() =>
		new(new CustodiaSesionLocal(_sesiones, _tokens, _persistida),
			_aviso, _reloj, _monotonico, _bitacora);

	private static SesionOfflinePersistida Guardada() => new(
		SessionId: "s-1",
		Operador: "Juan Pérez",
		Rol: "Operador de campo",
		Unidad: new UnidadVehicular("u-1", "VEH-01", "Camioneta 01"),
		Permisos: PermisosOperador.DelServidor(["APP_OPERADOR_MOVIL"]),
		Vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
		MonotonicoAlValidar: MonotonicoAlValidar,
		Instalacion: new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)));

	// ---------- Dentro de la ventana ----------

	[Fact]
	public async Task Dentro_de_la_ventana_la_sesion_sigue()
	{
		var estado = await Crear().ComprobarAsync();

		Assert.Equal(EstadoVigenciaSesion.Vigente, estado);
		_sesiones.DidNotReceive().Limpiar();
	}

	[Fact]
	public async Task Sin_sesion_abierta_no_hay_nada_que_vigilar()
	{
		_sesiones.Actual.Returns((SesionOperador?)null);

		Assert.Equal(EstadoVigenciaSesion.SinSesion, await Crear().ComprobarAsync());
	}

	[Fact]
	public async Task Con_sesion_viva_pero_sin_nada_guardado_no_se_bloquea()
	{
		// Es el recorrido contra simuladores: no persiste sesion, asi que no hay ventana
		// que medir y bloquear seria expulsar al operador sin motivo.
		_persistida.ObtenerAsync(Arg.Any<CancellationToken>()).Returns((SesionOfflinePersistida?)null);

		Assert.Equal(EstadoVigenciaSesion.Vigente, await Crear().ComprobarAsync());
		_sesiones.DidNotReceive().Limpiar();
	}

	// ---------- CA 2: pasadas las ocho horas ----------

	[Fact]
	public async Task Al_vencer_la_ventana_cierra_la_sesion()
	{
		_reloj.UtcAhora = Validacion.AddHours(9);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		var estado = await Crear().ComprobarAsync();

		Assert.Equal(EstadoVigenciaSesion.Expirada, estado);
		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
		await _persistida.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Al_vencer_deja_el_motivo_para_la_pantalla_de_acceso()
	{
		// CA 3: el operador tiene que ver "Sesion offline expirada", no un formulario mudo.
		_reloj.UtcAhora = Validacion.AddHours(9);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		await Crear().ComprobarAsync();

		Assert.Equal(MotivoRechazoAcceso.SesionOfflineExpirada, _aviso.Consumir());
	}

	[Fact]
	public async Task Al_vencer_queda_asentado_en_la_bitacora()
	{
		_reloj.UtcAhora = Validacion.AddHours(9);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		await Crear().ComprobarAsync();

		await _bitacora.Received(1).RegistrarAsync(
			NivelAuditoria.Advertencia, Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	// ---------- CA 4: no se puede estirar la ventana ----------

	[Fact]
	public async Task Atrasar_el_reloj_no_evita_el_bloqueo()
	{
		// El contador monotonico dice que pasaron nueve horas aunque el reloj diga otra cosa.
		_reloj.UtcAhora = Validacion.AddHours(-4);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		Assert.Equal(EstadoVigenciaSesion.Expirada, await Crear().ComprobarAsync());
	}

	[Fact]
	public async Task Reiniciar_el_dispositivo_no_evita_el_bloqueo()
	{
		// El contador arranca de cero; manda el reloj, que dice que pasaron diez horas.
		_reloj.UtcAhora = Validacion.AddHours(10);
		_monotonico.Transcurrido = TimeSpan.FromMinutes(1);

		Assert.Equal(EstadoVigenciaSesion.Expirada, await Crear().ComprobarAsync());
	}

	// ---------- CA 5 y 6: lo capturado no se toca ----------

	[Fact]
	public async Task Al_vencer_no_se_toca_nada_de_lo_capturado()
	{
		// La expiracion cierra la sesion, no borra el trabajo de campo. La custodia no
		// conoce la cola ni las evidencias, y esa es justamente la garantia.
		_reloj.UtcAhora = Validacion.AddHours(9);
		_monotonico.Transcurrido = MonotonicoAlValidar + TimeSpan.FromHours(9);

		await Crear().ComprobarAsync();

		await _persistida.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
		await _persistida.DidNotReceive().GuardarAsync(
			Arg.Any<SesionOfflinePersistida>(), Arg.Any<CancellationToken>());
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

/// <summary>
/// El aviso de por qué terminó la sesión se entrega una sola vez.
/// </summary>
public class AvisoDeSesionTerminadaTests
{
	[Fact]
	public void Sin_nada_registrado_no_hay_aviso()
	{
		Assert.Null(new AvisoDeSesionTerminada().Consumir());
	}

	[Fact]
	public void Devuelve_el_motivo_registrado()
	{
		var aviso = new AvisoDeSesionTerminada();
		aviso.Registrar(MotivoRechazoAcceso.SesionOfflineExpirada);

		Assert.Equal(MotivoRechazoAcceso.SesionOfflineExpirada, aviso.Consumir());
	}

	[Fact]
	public void El_aviso_no_reaparece_en_el_siguiente_acceso()
	{
		// Si quedara guardado, diria algo que ya no es cierto.
		var aviso = new AvisoDeSesionTerminada();
		aviso.Registrar(MotivoRechazoAcceso.SesionRevocada);

		aviso.Consumir();

		Assert.Null(aviso.Consumir());
	}

	[Fact]
	public void El_ultimo_motivo_reemplaza_al_anterior()
	{
		var aviso = new AvisoDeSesionTerminada();
		aviso.Registrar(MotivoRechazoAcceso.SesionOfflineExpirada);
		aviso.Registrar(MotivoRechazoAcceso.SinPermiso);

		Assert.Equal(MotivoRechazoAcceso.SinPermiso, aviso.Consumir());
	}
}
