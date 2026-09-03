using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Entidades;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

public sealed class ObtenerKilometroPorUbicacionTests
{
	private static readonly DateTime Instante =
		new(2026, 8, 24, 20, 0, 0, DateTimeKind.Utc);

	private static GeometriaCorredor Corredor() => GeometriaCorredor.Crear(
		[
			new VerticeCorredor(-116.70, 32.50, 130_000),
			new VerticeCorredor(-116.69, 32.50, 131_000),
		],
		"Prueba");

	private static ObtenerKilometroPorUbicacion Crear(
		LecturaUbicacion lectura,
		out IRepositorioGeometriaCorredor repositorio)
	{
		var ubicacion = Substitute.For<ILocationService>();
		ubicacion.ObtenerPosicionAsync(Arg.Any<CancellationToken>()).Returns(lectura);

		repositorio = Substitute.For<IRepositorioGeometriaCorredor>();
		repositorio.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Corredor());

		return new ObtenerKilometroPorUbicacion(ubicacion, repositorio);
	}

	[Fact]
	public async Task PosicionSobreElCorredor_calculaYFormateaElKilometro()
	{
		var posicion = PosicionDispositivo.Crear(32.50, -116.695, 8, Instante);
		var casoDeUso = Crear(LecturaUbicacion.Con(posicion), out _);

		var resultado = await casoDeUso.EjecutarAsync();

		Assert.True(resultado.HayKilometro);
		Assert.Equal("130+500", resultado.Kilometro!.Valor);
		Assert.Equal(130_500, resultado.Metros);
		Assert.Same(posicion, resultado.Posicion);
		Assert.Null(resultado.Motivo);
	}

	[Fact]
	public async Task PrecisionInsuficiente_activaCapturaManualSinConsultarGeometria()
	{
		var posicion = PosicionDispositivo.Crear(32.50, -116.695, 51, Instante);
		var casoDeUso = Crear(LecturaUbicacion.Con(posicion), out var repositorio);

		var resultado = await casoDeUso.EjecutarAsync();

		Assert.False(resultado.HayKilometro);
		Assert.Equal(MotivoSinKilometro.PrecisionInsuficiente, resultado.Motivo);
		Assert.Same(posicion, resultado.Posicion);
		await repositorio.DidNotReceive().ObtenerAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task FalloDelDispositivo_conservaElMotivo()
	{
		var casoDeUso = Crear(
			LecturaUbicacion.Sin(MotivoSinKilometro.PermisoDenegado),
			out var repositorio);

		var resultado = await casoDeUso.EjecutarAsync();

		Assert.Equal(MotivoSinKilometro.PermisoDenegado, resultado.Motivo);
		Assert.Null(resultado.Posicion);
		await repositorio.DidNotReceive().ObtenerAsync(Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task PosicionLejanaSeConsideraFueraDelCorredor()
	{
		var posicion = PosicionDispositivo.Crear(32.51, -116.695, 8, Instante);
		var casoDeUso = Crear(LecturaUbicacion.Con(posicion), out _);

		var resultado = await casoDeUso.EjecutarAsync();

		Assert.Equal(MotivoSinKilometro.FueraDelCorredor, resultado.Motivo);
		Assert.Null(resultado.Kilometro);
	}

	[Fact]
	public async Task MasAllaDeUnExtremoSeDistingueDeFueraDelCorredor()
	{
		var posicion = PosicionDispositivo.Crear(32.50, -116.68, 8, Instante);
		var casoDeUso = Crear(LecturaUbicacion.Con(posicion), out _);

		var resultado = await casoDeUso.EjecutarAsync();

		Assert.Equal(MotivoSinKilometro.TramoSinGeometria, resultado.Motivo);
	}

	[Fact]
	public async Task FalloAlCargarLaGeometriaActivaCapturaManual()
	{
		var posicion = PosicionDispositivo.Crear(32.50, -116.695, 8, Instante);
		var ubicacion = Substitute.For<ILocationService>();
		ubicacion.ObtenerPosicionAsync(Arg.Any<CancellationToken>())
			.Returns(LecturaUbicacion.Con(posicion));
		var repositorio = Substitute.For<IRepositorioGeometriaCorredor>();
		repositorio.ObtenerAsync(Arg.Any<CancellationToken>())
			.Returns<Task<GeometriaCorredor>>(_ => throw new InvalidOperationException("Recurso dañado"));
		var casoDeUso = new ObtenerKilometroPorUbicacion(ubicacion, repositorio);

		var resultado = await casoDeUso.EjecutarAsync();

		Assert.Equal(MotivoSinKilometro.ErrorAlObtener, resultado.Motivo);
		Assert.Same(posicion, resultado.Posicion);
	}
}
