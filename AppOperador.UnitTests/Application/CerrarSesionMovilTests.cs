using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Cierre de la sesión móvil sin perder lo pendiente (JTT-1390).
/// </summary>
public class CerrarSesionMovilTests
{
	private const string Token = "jwt-de-la-sesion";

	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly ITokenProvider _tokens = Substitute.For<ITokenProvider>();
	private readonly IAuditLog _bitacora = Substitute.For<IAuditLog>();
	private readonly ISyncQueueService _cola = Substitute.For<ISyncQueueService>();
	private readonly IAccesoJacobClient _jacob = Substitute.For<IAccesoJacobClient>();
	private readonly IOfflineSessionStore _persistida = Substitute.For<IOfflineSessionStore>();

	public CerrarSesionMovilTests()
	{
		_tokens.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Token);
		_cola.ContarPendientesAsync(Arg.Any<CancellationToken>()).Returns(0);
	}

	private CustodiaSesionLocal Custodia() => new(_sesiones, _tokens, _persistida);

	/// <summary>Caso de uso con el canal real de Jacob conectado.</summary>
	private CerrarSesionMovil ConJacob() => new(Custodia(), _bitacora, _cola, _jacob);

	/// <summary>Caso de uso contra los simuladores: no hay a quién avisar.</summary>
	private CerrarSesionMovil SinJacob() => new(Custodia(), _bitacora, _cola);

	private void JacobResponde(bool exito) =>
		_jacob.CerrarSesionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(exito);

	// ---------- CA 2: se invalida el contexto y se eliminan los tokens ----------

	[Fact]
	public async Task Borra_la_sesion_y_el_token()
	{
		JacobResponde(true);

		await ConJacob().CerrarAsync();

		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	// ---------- CA 3: se avisa al backend ----------

	[Fact]
	public async Task Avisa_a_Jacob_con_el_token_de_la_sesion()
	{
		JacobResponde(true);

		var resultado = await ConJacob().CerrarAsync();

		await _jacob.Received(1).CerrarSesionAsync(Token, Arg.Any<CancellationToken>());
		Assert.True(resultado.AvisoAlServidor);
	}

	[Fact]
	public async Task Avisa_antes_de_borrar_el_token()
	{
		// Al revés, el aviso se quedaria sin credencial con que autenticarse.
		JacobResponde(true);
		var ordenCorrecto = false;
		_jacob.CerrarSesionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(_ => { ordenCorrecto = true; return true; });
		_tokens.When(t => t.LimpiarAsync(Arg.Any<CancellationToken>()))
			.Do(_ => Assert.True(ordenCorrecto, "el token se borro antes de avisar a Jacob"));

		await ConJacob().CerrarAsync();

		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	// ---------- CA 4: sin conectividad se cierra igual ----------

	[Fact]
	public async Task Si_Jacob_no_contesta_la_sesion_se_cierra_de_todas_formas()
	{
		// En campo, quedarse sin señal es lo normal. Dejar la sesion abierta porque el
		// servidor no contesta seria lo contrario de lo que pide la historia.
		JacobResponde(false);

		var resultado = await ConJacob().CerrarAsync();

		Assert.False(resultado.AvisoAlServidor);
		_sesiones.Received(1).Limpiar();
		await _tokens.Received(1).LimpiarAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Sin_canal_real_el_cierre_es_puramente_local()
	{
		var resultado = await SinJacob().CerrarAsync();

		Assert.False(resultado.AvisoAlServidor);
		_sesiones.Received(1).Limpiar();
	}

	[Fact]
	public async Task Sin_token_guardado_no_se_llama_a_Jacob()
	{
		// Una sesion que nunca llego a existir en el servidor no tiene nada que revocar.
		_tokens.ObtenerAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

		var resultado = await ConJacob().CerrarAsync();

		await _jacob.DidNotReceive().CerrarSesionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
		Assert.False(resultado.AvisoAlServidor);
		_sesiones.Received(1).Limpiar();
	}

	// ---------- CA 5 y CA 10: no se borra lo pendiente ----------

	[Fact]
	public async Task No_sincroniza_ni_vacia_la_cola()
	{
		// Cerrar sesion no es desinstalar la app: la cola solo se consulta.
		JacobResponde(true);

		await ConJacob().CerrarAsync();

		// Cerrar sesión no sincroniza: la sincronización es una acción del operador o de la
		// revalidación, no un efecto del cierre. Y sobre todo, no vacía la cola.
		await _cola.DidNotReceiveWithAnyArgs().ActualizarEnvioAsync(default!, default);
		await _cola.Received(1).ContarPendientesAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Informa_cuantos_pendientes_quedaron_conservados()
	{
		JacobResponde(true);
		_cola.ContarPendientesAsync(Arg.Any<CancellationToken>()).Returns(3);

		var resultado = await ConJacob().CerrarAsync();

		Assert.Equal(3, resultado.PendientesConservados);
	}

	[Fact]
	public async Task Cuenta_los_pendientes_antes_de_limpiar_la_sesion()
	{
		// La cola esta filtrada por el operador de la sesion: contarlos despues daria cero
		// siempre, y la bitacora mentiria sobre lo que quedo guardado.
		JacobResponde(true);
		var sesionSeguiaAbierta = false;
		_cola.ContarPendientesAsync(Arg.Any<CancellationToken>())
			.Returns(_ => { sesionSeguiaAbierta = true; return 2; });

		await ConJacob().CerrarAsync();

		Assert.True(sesionSeguiaAbierta);
		Received.InOrder(() =>
		{
			_cola.ContarPendientesAsync(Arg.Any<CancellationToken>());
			_sesiones.Limpiar();
		});
	}

	[Fact]
	public async Task Un_fallo_al_contar_no_impide_el_cierre()
	{
		JacobResponde(true);
		_cola.ContarPendientesAsync(Arg.Any<CancellationToken>())
			.Returns<Task<int>>(_ => throw new InvalidOperationException("base ocupada"));

		var resultado = await ConJacob().CerrarAsync();

		Assert.Equal(0, resultado.PendientesConservados);
		_sesiones.Received(1).Limpiar();
	}

	// ---------- CA 9: queda auditado localmente ----------

	[Fact]
	public async Task Deja_constancia_en_la_bitacora_local()
	{
		JacobResponde(true);

		await ConJacob().CerrarAsync();

		await _bitacora.Received(1).RegistrarAsync(
			NivelAuditoria.Info, Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task La_bitacora_distingue_el_cierre_sin_conexion()
	{
		JacobResponde(false);
		string? registrado = null;
		_bitacora.When(b => b.RegistrarAsync(
				Arg.Any<NivelAuditoria>(), Arg.Any<string>(), Arg.Any<CancellationToken>()))
			.Do(c => registrado = c.ArgAt<string>(1));

		await ConJacob().CerrarAsync();

		Assert.Contains("sin conexión", registrado!, StringComparison.OrdinalIgnoreCase);
	}
}
