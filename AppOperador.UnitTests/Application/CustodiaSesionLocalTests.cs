using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// El rastro local de una sesión se abre y se cierra entero.
/// </summary>
/// <remarks>
/// Una sesión deja huella en tres sitios y antes cada caso de uso los recorría por su
/// cuenta. Uno se dejaba la sesión persistida sin borrar. Estas pruebas fijan que los tres
/// se tocan siempre juntos.
/// </remarks>
public class CustodiaSesionLocalTests
{
	private static readonly DateTime Validacion = new(2026, 8, 11, 8, 0, 0, DateTimeKind.Utc);

	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly IOfflineSessionStore _persistida = Substitute.For<IOfflineSessionStore>();

	private CustodiaSesionLocal Crear() => new(_sesiones, _tokens, _persistida);

	private static SesionOfflinePersistida Sesion() => new(
		SessionId: "s-1",
		Operador: "Juan Pérez",
		Rol: "Operador de campo",
		Unidad: new UnidadVehicular("u-1", "VEH-01", "Camioneta 01"),
		Permisos: PermisosOperador.DelServidor(["APP_OPERADOR_MOVIL"]),
		Vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
		MonotonicoAlValidar: TimeSpan.FromHours(3),
		Instalacion: new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)));

	// ---------- Abrir ----------

	[Fact]
	public async Task Abrir_deja_la_sesion_en_los_tres_sitios()
	{
		await Crear().AbrirAsync(Sesion(), "jwt-nuevo");

		await _tokens.Received(1).GuardarAsync("jwt-nuevo", Arg.Any<CancellationToken>());
		await _persistida.Received(1).GuardarAsync(Arg.Any<SesionOfflinePersistida>(), Arg.Any<CancellationToken>());
		_sesiones.Received(1).Guardar(Arg.Any<SesionOperador>());
	}

	[Fact]
	public async Task Abrir_guarda_el_token_antes_de_anunciar_la_sesion()
	{
		// Si el token fallara al guardarse, es preferible no haber anunciado una sesion que
		// despues no podria autenticar ninguna peticion.
		await Crear().AbrirAsync(Sesion(), "jwt-nuevo");

		Received.InOrder(() =>
		{
			_tokens.GuardarAsync("jwt-nuevo", Arg.Any<CancellationToken>());
			_sesiones.Guardar(Arg.Any<SesionOperador>());
		});
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task Abrir_sin_token_nuevo_conserva_el_que_ya_estaba(string? token)
	{
		// Es el caso de la revalidacion: no emite token nuevo, y sobrescribirlo con vacio
		// dejaria la sesion sin credencial.
		await Crear().AbrirAsync(Sesion(), token);

		await _tokens.DidNotReceive().GuardarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		_sesiones.Received(1).Guardar(Arg.Any<SesionOperador>());
	}

	// ---------- Revocar ----------

	[Fact]
	public async Task Revocar_borra_los_tres_sitios()
	{
		await Crear().RevocarAsync();

		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
		await _persistida.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}
}
