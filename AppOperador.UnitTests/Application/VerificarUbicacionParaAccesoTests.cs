using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace AppOperador.UnitTests.Application;

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
		_ubicacion.ConsultarEstadoAsync(Arg.Any<CancellationToken>()).Returns(estado);

		await CrearCasoDeUso().ExigirAsync();

		await _ubicacion.DidNotReceive().SolicitarPermisoAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Revisar_NuncaPideElPermiso()
	{
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
		_ubicacion.AbrirAjustesDeUbicacionAsync(Arg.Any<CancellationToken>())
			.Throws(new InvalidOperationException("no hay actividad que atienda el intent"));

		var abierto = await CrearCasoDeUso().AbrirAjustesAsync(AccionUbicacion.AbrirAjustesDeUbicacion);

		Assert.False(abierto);
	}

	[Fact]
	public async Task AbrirAjustes_NoReevaluaElEstado()
	{
		_ubicacion.AbrirAjustesDeLaAppAsync(Arg.Any<CancellationToken>()).Returns(true);

		await CrearCasoDeUso().AbrirAjustesAsync(AccionUbicacion.AbrirAjustesDeLaApp);

		await _ubicacion.DidNotReceive().ConsultarEstadoAsync(Arg.Any<CancellationToken>());
	}
}
