using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Comprobación del prerrequisito de ubicación del acceso (JTT-1380).
/// </summary>
/// <remarks>
/// Todo lo que toca el dispositivo está detrás de <see cref="ILocationPermissionService"/>,
/// así que estas pruebas cubren la regla completa sin emulador: qué estado deja entrar, qué
/// acción se le ofrece al operador en cada caso y qué pasa cuando el dispositivo falla.
/// </remarks>
public class VerificarUbicacionParaAccesoTests
{
	private readonly ILocationPermissionService _ubicacion = Substitute.For<ILocationPermissionService>();

	private VerificarUbicacionParaAcceso CrearCasoDeUso() => new(_ubicacion);

	[Fact]
	public async Task Concedido_EsElUnicoEstadoQueDejaAcceder()
	{
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.Concedido);

		var resultado = await CrearCasoDeUso().RevisarAsync();

		Assert.True(resultado.PermiteAcceder);
		Assert.Equal(EstadoUbicacion.Concedido, resultado.Estado);
		Assert.Equal(AccionUbicacion.Ninguna, resultado.Accion);
	}

	[Theory]
	[InlineData(EstadoUbicacion.NoSolicitado)]
	[InlineData(EstadoUbicacion.Rechazado)]
	[InlineData(EstadoUbicacion.BloqueadoPermanentemente)]
	[InlineData(EstadoUbicacion.ServicioDesactivado)]
	[InlineData(EstadoUbicacion.NoDisponibleEnElDispositivo)]
	[InlineData(EstadoUbicacion.ErrorAlConsultar)]
	public async Task NingunEstadoQueNoSeaConcedido_DejaAcceder(EstadoUbicacion estado)
	{
		// Es el corazón de la historia: el prerrequisito es obligatorio y ninguna situación
		// distinta de "concedido" puede dejar continuar el acceso.
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(estado);

		var resultado = await CrearCasoDeUso().RevisarAsync();

		Assert.False(resultado.PermiteAcceder);
		Assert.Equal(estado, resultado.Estado);
	}

	[Theory]
	[InlineData(EstadoUbicacion.Concedido, AccionUbicacion.Ninguna)]
	[InlineData(EstadoUbicacion.NoSolicitado, AccionUbicacion.SolicitarPermiso)]
	[InlineData(EstadoUbicacion.Rechazado, AccionUbicacion.SolicitarPermiso)]
	[InlineData(EstadoUbicacion.BloqueadoPermanentemente, AccionUbicacion.AbrirAjustesDeLaApp)]
	[InlineData(EstadoUbicacion.ServicioDesactivado, AccionUbicacion.AbrirAjustesDeUbicacion)]
	[InlineData(EstadoUbicacion.NoDisponibleEnElDispositivo, AccionUbicacion.Ninguna)]
	[InlineData(EstadoUbicacion.ErrorAlConsultar, AccionUbicacion.Ninguna)]
	public void CadaEstado_OfreceLaAccionQueLeCorresponde(EstadoUbicacion estado, AccionUbicacion esperada)
	{
		// Un permiso bloqueado no se arregla volviendo a pedirlo, y un GPS apagado no se
		// arregla desde la ficha de la app: ofrecer la acción equivocada deja al operador
		// pulsando un botón que no cambia nada.
		var resultado = ResultadoUbicacion.Para(estado);

		Assert.Equal(esperada, resultado.Accion);
		Assert.Equal(esperada != AccionUbicacion.Ninguna, resultado.HayAccion);
	}

	[Fact]
	public void UnEstadoDesconocido_SeTrataComoFalloTecnicoYNoConcedeElPaso()
	{
		// Si alguien agrega un estado y olvida esta tabla, el acceso no se abre solo.
		var resultado = ResultadoUbicacion.Para((EstadoUbicacion)99);

		Assert.False(resultado.PermiteAcceder);
		Assert.Equal(EstadoUbicacion.ErrorAlConsultar, resultado.Estado);
		Assert.Equal(AccionUbicacion.Ninguna, resultado.Accion);
	}

	[Fact]
	public async Task Exigir_ConPermisoNuncaPedido_LoPideUnaVezYDevuelveElEstadoPosterior()
	{
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.NoSolicitado);
		_ubicacion.SolicitarPermisoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.Concedido);

		var resultado = await CrearCasoDeUso().ExigirAsync();

		await _ubicacion.Received(1).SolicitarPermisoAsync(Arg.Any<CancellationToken>());
		Assert.True(resultado.PermiteAcceder);
		Assert.Equal(EstadoUbicacion.Concedido, resultado.Estado);
	}

	[Fact]
	public async Task Exigir_CuandoElOperadorNiegaElPermisoReciénPedido_QuedaRechazado()
	{
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.NoSolicitado);
		_ubicacion.SolicitarPermisoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.Rechazado);

		var resultado = await CrearCasoDeUso().ExigirAsync();

		Assert.False(resultado.PermiteAcceder);
		Assert.Equal(EstadoUbicacion.Rechazado, resultado.Estado);
		Assert.Equal(AccionUbicacion.SolicitarPermiso, resultado.Accion);
	}

	[Theory]
	[InlineData(EstadoUbicacion.Concedido)]
	[InlineData(EstadoUbicacion.Rechazado)]
	[InlineData(EstadoUbicacion.BloqueadoPermanentemente)]
	[InlineData(EstadoUbicacion.ServicioDesactivado)]
	[InlineData(EstadoUbicacion.NoDisponibleEnElDispositivo)]
	public async Task Exigir_NoVuelveAPedirElPermisoCuandoYaHuboRespuesta(EstadoUbicacion estado)
	{
		// Insistir sobre una decisión ya tomada es acoso, y en Android un segundo diálogo
		// automático puede resolverse en negativa sin llegar a mostrarse. El operador vuelve
		// a pedirlo con el botón de la pantalla, no la app por su cuenta.
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(estado);

		await CrearCasoDeUso().ExigirAsync();

		await _ubicacion.DidNotReceive().SolicitarPermisoAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Revisar_NuncaPideElPermiso()
	{
		// Es la comprobación de reingreso: mira y calla. Si pidiera el permiso, volver a la
		// pantalla dispararía el diálogo del sistema sin que el operador hiciera nada.
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.NoSolicitado);

		var resultado = await CrearCasoDeUso().RevisarAsync();

		await _ubicacion.DidNotReceive().SolicitarPermisoAsync(Arg.Any<CancellationToken>());
		Assert.Equal(EstadoUbicacion.NoSolicitado, resultado.Estado);
	}

	[Fact]
	public async Task Revisar_TrasCambiarElPermisoEnConfiguracion_DevuelveElEstadoNuevo()
	{
		// El caso de "salí a la configuración, lo concedí y volví".
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>())
			.Returns(EstadoUbicacion.BloqueadoPermanentemente, EstadoUbicacion.Concedido);

		var casoDeUso = CrearCasoDeUso();

		var antes = await casoDeUso.RevisarAsync();
		var despues = await casoDeUso.RevisarAsync();

		Assert.False(antes.PermiteAcceder);
		Assert.True(despues.PermiteAcceder);
	}

	[Fact]
	public async Task UnFalloDelDispositivoAlConsultar_NoSePresentaComoNegativaDelOperador()
	{
		// Mismo criterio que JTT-1378 CA 11: un error técnico no puede disfrazarse de otra
		// cosa. Bloquea igual, pero se le dice al operador que reintente, no que conceda un
		// permiso que quizá ya concedió.
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>())
			.Throws(new InvalidOperationException("el servicio de ubicación no respondió"));

		var resultado = await CrearCasoDeUso().RevisarAsync();

		Assert.False(resultado.PermiteAcceder);
		Assert.Equal(EstadoUbicacion.ErrorAlConsultar, resultado.Estado);
		Assert.Equal(AccionUbicacion.Ninguna, resultado.Accion);
	}

	[Fact]
	public async Task UnFalloDelDispositivoAlSolicitar_TampocoEscapaComoExcepcion()
	{
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(EstadoUbicacion.NoSolicitado);
		_ubicacion.SolicitarPermisoAsync(Arg.Any<CancellationToken>())
			.Throws(new InvalidOperationException("el diálogo de permisos falló"));

		var resultado = await CrearCasoDeUso().ExigirAsync();

		Assert.Equal(EstadoUbicacion.ErrorAlConsultar, resultado.Estado);
	}

	[Fact]
	public async Task LaCancelacionSePropaga_NoSeConfundeConUnFalloDeUbicacion()
	{
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>())
			.Throws(new OperationCanceledException());

		await Assert.ThrowsAsync<OperationCanceledException>(() => CrearCasoDeUso().RevisarAsync());
	}

	[Fact]
	public async Task AbrirAjustes_LlevaACadaPantallaSegunLaAccion()
	{
		_ubicacion.AbrirAjustesDeLaAppAsync(Arg.Any<CancellationToken>()).Returns(true);
		_ubicacion.AbrirAjustesDeUbicacionAsync(Arg.Any<CancellationToken>()).Returns(true);
		var casoDeUso = CrearCasoDeUso();

		Assert.True(await casoDeUso.AbrirAjustesAsync(AccionUbicacion.AbrirAjustesDeLaApp));
		Assert.True(await casoDeUso.AbrirAjustesAsync(AccionUbicacion.AbrirAjustesDeUbicacion));

		await _ubicacion.Received(1).AbrirAjustesDeLaAppAsync(Arg.Any<CancellationToken>());
		await _ubicacion.Received(1).AbrirAjustesDeUbicacionAsync(Arg.Any<CancellationToken>());
	}

	[Theory]
	[InlineData(AccionUbicacion.Ninguna)]
	[InlineData(AccionUbicacion.SolicitarPermiso)]
	public async Task AbrirAjustes_ConUnaAccionQueNoAbreNada_NoTocaElDispositivo(AccionUbicacion accion)
	{
		var abierto = await CrearCasoDeUso().AbrirAjustesAsync(accion);

		Assert.False(abierto);
		await _ubicacion.DidNotReceive().AbrirAjustesDeLaAppAsync(Arg.Any<CancellationToken>());
		await _ubicacion.DidNotReceive().AbrirAjustesDeUbicacionAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task AbrirAjustes_CuandoElDispositivoNoLosAbre_LoDiceEnVezDeLanzar()
	{
		// La pantalla necesita saberlo para avisar: un botón que no hace nada visible es peor
		// que no tener botón.
		_ubicacion.AbrirAjustesDeUbicacionAsync(Arg.Any<CancellationToken>())
			.Throws(new InvalidOperationException("no hay actividad que atienda el intent"));

		var abierto = await CrearCasoDeUso().AbrirAjustesAsync(AccionUbicacion.AbrirAjustesDeUbicacion);

		Assert.False(abierto);
	}

	[Fact]
	public async Task AbrirAjustes_NoReevaluaElEstado()
	{
		// No podría: la app queda en segundo plano mientras el operador está en la
		// configuración. La reevaluación es cosa de RevisarAsync al volver.
		_ubicacion.AbrirAjustesDeLaAppAsync(Arg.Any<CancellationToken>()).Returns(true);

		await CrearCasoDeUso().AbrirAjustesAsync(AccionUbicacion.AbrirAjustesDeLaApp);

		await _ubicacion.DidNotReceive().ConsultarEstadoAsync(Arg.Any<CancellationToken>());
	}
}
