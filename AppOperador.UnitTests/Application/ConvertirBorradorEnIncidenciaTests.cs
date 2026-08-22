using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Conversión de un borrador en incidencia (JTT-1399 CA 8 y 9).
/// </summary>
/// <remarks>
/// Lo que se prueba aquí es la <b>compuerta</b>: que ningún borrador incompleto llegue al
/// repositorio. Que la fila cambie de estado se prueba contra SQLite, en
/// <c>RepositorioIncidenciasSqliteTests</c>.
/// </remarks>
public sealed class ConvertirBorradorEnIncidenciaTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	private static readonly TipoIncidencia Otro = new(107, "Otro", ExigeDescripcion: true);

	private static readonly SeveridadIncidencia Advertencia =
		new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Advertencia", 2, "#EDD611");

	private readonly IIncidentRepository _incidencias = Substitute.For<IIncidentRepository>();

	private ConvertirBorradorEnIncidencia CrearCaso() => new(_incidencias);

	[Fact]
	public async Task SinTipo_noConvierteYLoDice()
	{
		var resultado = await EjecutarAsync(tipo: null, kilometro: "130+200", nota: "nota");

		Assert.Equal(ResultadoConversionBorrador.FaltaTipo, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task SinSeveridad_noConvierteYLoDice()
	{
		var resultado = await CrearCaso().EjecutarAsync(
			"LOC-000001", Objeto, "130+200", KilometerSource.Manual, severidad: null, "nota");

		Assert.Equal(ResultadoConversionBorrador.FaltaSeveridad, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task ConKilometroAMedioEscribir_noConvierte()
	{
		// "130+" es un borrador válido y una incidencia inválida: es justo la diferencia que
		// el CA 9 obliga a comprobar al convertir.
		var resultado = await EjecutarAsync(Objeto, kilometro: "130+", nota: "nota");

		Assert.Equal(ResultadoConversionBorrador.KilometroInvalido, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task ConKilometroVacio_noConvierte()
	{
		var resultado = await EjecutarAsync(Objeto, kilometro: null, nota: "nota");

		Assert.Equal(ResultadoConversionBorrador.KilometroInvalido, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task SiElTipoExigeDescripcion_unaNotaCortaNoConvierte()
	{
		var resultado = await EjecutarAsync(Otro, kilometro: "130+200", nota: "corta");

		Assert.Equal(ResultadoConversionBorrador.NotaInsuficiente, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task SiElTipoNoExigeDescripcion_laNotaVaciaSiConvierte()
	{
		_incidencias.ConvertirBorradorAsync(
			Arg.Any<string>(), Arg.Any<TipoIncidencia>(), Arg.Any<Kilometer>(),
			Arg.Any<KilometerSource>(), Arg.Any<SeveridadIncidencia>(), Arg.Any<string>(),
			Arg.Any<CancellationToken>()).Returns(true);

		var resultado = await EjecutarAsync(Objeto, kilometro: "130+200", nota: "");

		Assert.Equal(ResultadoConversionBorrador.Convertido, resultado);
	}

	[Fact]
	public async Task CuandoTodoEstaCompleto_convierteYRecortaLaNota()
	{
		_incidencias.ConvertirBorradorAsync(
			Arg.Any<string>(), Arg.Any<TipoIncidencia>(), Arg.Any<Kilometer>(),
			Arg.Any<KilometerSource>(), Arg.Any<SeveridadIncidencia>(), Arg.Any<string>(),
			Arg.Any<CancellationToken>()).Returns(true);

		var resultado = await EjecutarAsync(Objeto, "130+200", nota: "  con espacios  ");

		Assert.Equal(ResultadoConversionBorrador.Convertido, resultado);
		await _incidencias.Received(1).ConvertirBorradorAsync(
			"LOC-000001",
			Objeto,
			Arg.Is<Kilometer>(k => k != null && k.Valor == "130+200"),
			KilometerSource.Manual,
			Advertencia,
			"con espacios",
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task SiElBorradorYaNoExiste_loDiceEnVezDeFingirQueConvirtio()
	{
		_incidencias.ConvertirBorradorAsync(
			Arg.Any<string>(), Arg.Any<TipoIncidencia>(), Arg.Any<Kilometer>(),
			Arg.Any<KilometerSource>(), Arg.Any<SeveridadIncidencia>(), Arg.Any<string>(),
			Arg.Any<CancellationToken>()).Returns(false);

		var resultado = await EjecutarAsync(Objeto, "130+200", "nota");

		Assert.Equal(ResultadoConversionBorrador.NoEncontrado, resultado);
	}

	private Task<ResultadoConversionBorrador> EjecutarAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		string nota) =>
		CrearCaso().EjecutarAsync(
			"LOC-000001", tipo, kilometro, KilometerSource.Manual, Advertencia, nota);

	private Task NoTocoElRepositorioAsync() =>
		_incidencias.DidNotReceiveWithAnyArgs().ConvertirBorradorAsync(
			default!, default!, default!, default, default!, default!, default);
}
