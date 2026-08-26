using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Qué pasa en la app cuando Jacob retira el permiso funcional (JTT-1379 CA 6).
/// </summary>
/// <remarks>
/// La revocación se decide en el servidor y llega en la siguiente validación en línea. Sin
/// borrar lo guardado, el operador revocado seguiría trabajando sin conexión con la sesión
/// de su último acceso correcto.
/// </remarks>
public class RevocacionDePermisoTests
{
	private static readonly DateTime Validacion = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
	private static readonly UnidadVehicular Unidad = new("u-1", "VEH-01", "Camioneta 01");

	private readonly IAccesoJacobClient _jacob = Substitute.For<IAccesoJacobClient>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly IOfflineSessionStore _persistida = Substitute.For<IOfflineSessionStore>();
	private readonly IMonotonicClock _monotonico = Substitute.For<IMonotonicClock>();

	private AbrirSesionMovil CrearCasoDeUso() =>
		new(_jacob,
			new CustodiaSesionLocal(_sesiones, _tokens, _persistida),
			new DatosDeInstalacion("1.2.0", new DateOnly(2026, 7, 23)),
			_monotonico);

	private void PreautenticacionDevuelve(ResultadoPreauth resultado) =>
		_jacob.PreautenticarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(resultado);

	private void LoginDevuelve(ResultadoLogin resultado) =>
		_jacob.CompletarAccesoAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(resultado);

	private async Task<AbrirSesionMovil> ConDesafioEmitidoAsync()
	{
		PreautenticacionDevuelve(ResultadoPreauth.Emitido("d-1", Validacion.AddMinutes(5), [Unidad]));
		var casoDeUso = CrearCasoDeUso();
		await casoDeUso.IdentificarAsync("op@ipte.com.mx", "secreta");

		return casoDeUso;
	}

	// ---------- Paso 1: preautenticación ----------

	[Fact]
	public async Task Preauth_sin_permiso_borra_la_sesion_y_el_token_locales()
	{
		PreautenticacionDevuelve(
			ResultadoPreauth.Rechazado(MotivoRechazoAcceso.SinPermiso, "appoperador.permiso.requerido"));

		await CrearCasoDeUso().IdentificarAsync("op@ipte.com.mx", "secreta");

		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Preauth_sin_permiso_borra_tambien_la_sesion_persistida()
	{
		// Esto se quedaba fuera: la revocacion limpiaba sesion y token pero dejaba en disco
		// la sesion guardada para reanudar sin conexion.
		PreautenticacionDevuelve(
			ResultadoPreauth.Rechazado(MotivoRechazoAcceso.SinPermiso, "appoperador.permiso.requerido"));

		await CrearCasoDeUso().IdentificarAsync("op@ipte.com.mx", "secreta");

		await _persistida.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData(MotivoRechazoAcceso.CredencialInvalida)]
	[InlineData(MotivoRechazoAcceso.SinComunicacion)]
	[InlineData(MotivoRechazoAcceso.ErrorDelServicio)]
	[InlineData(MotivoRechazoAcceso.CuentaBloqueada)]
	[InlineData(MotivoRechazoAcceso.SinUnidades)]
	public async Task Otro_rechazo_no_toca_lo_guardado(MotivoRechazoAcceso motivo)
	{
		// Una contraseña mal escrita o una caída de red no dicen nada sobre la autorización
		// del operador. Cerrarle la sesión por eso convertiría cualquier tropiezo en salida.
		PreautenticacionDevuelve(ResultadoPreauth.Rechazado(motivo));

		await CrearCasoDeUso().IdentificarAsync("op@ipte.com.mx", "secreta");

		_sesiones.DidNotReceive().Limpiar();
		await _tokens.DidNotReceive().LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Una_preautenticacion_correcta_no_borra_nada()
	{
		await ConDesafioEmitidoAsync();

		_sesiones.DidNotReceive().Limpiar();
		await _tokens.DidNotReceive().LimpiarAsync(Arg.Any<CancellationToken>());
	}

	// ---------- Paso 2: creación de la sesión ----------

	[Fact]
	public async Task Login_sin_permiso_borra_la_sesion_y_el_token_locales()
	{
		// El permiso se revalida al crear la sesión, no solo al preautenticar: puede
		// retirarse entre un paso y el otro.
		var casoDeUso = await ConDesafioEmitidoAsync();
		LoginDevuelve(ResultadoLogin.Rechazado(MotivoRechazoAcceso.SinPermiso, "appoperador.permiso.requerido"));

		await casoDeUso.AbrirAsync(Unidad);

		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Login_rechazado_por_la_unidad_no_borra_lo_guardado()
	{
		var casoDeUso = await ConDesafioEmitidoAsync();
		LoginDevuelve(ResultadoLogin.Rechazado(MotivoRechazoAcceso.UnidadNoAutorizada));

		await casoDeUso.AbrirAsync(Unidad);

		_sesiones.DidNotReceive().Limpiar();
		await _tokens.DidNotReceive().LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Un_acceso_correcto_reemplaza_los_permisos_por_los_que_manda_Jacob()
	{
		// La otra mitad del CA 6: los permisos de la sesión nueva son los del servidor, no
		// una mezcla con los que hubiera guardados.
		var casoDeUso = await ConDesafioEmitidoAsync();
		LoginDevuelve(ResultadoLogin.Creada(new SesionValidada(
			sessionId: "s-1",
			accessToken: "jwt",
			tokenExpiraUtc: Validacion.AddHours(8),
			operador: "Juan Pérez",
			rol: "Operador de campo",
			unidad: Unidad,
			permisos: PermisosOperador.DelServidor(["CAPTURA"]),
			vigencia: VigenciaOffline.DelServidor(Validacion, Validacion.AddHours(8)),
			horaServidorUtc: Validacion)));
		SesionOperador? guardada = null;
		_sesiones.When(s => s.Guardar(Arg.Any<SesionOperador>()))
			.Do(c => guardada = c.Arg<SesionOperador>());

		await casoDeUso.AbrirAsync(Unidad);

		Assert.Equal(["CAPTURA"], guardada!.Permisos);
		_sesiones.DidNotReceive().Limpiar();
	}
}
