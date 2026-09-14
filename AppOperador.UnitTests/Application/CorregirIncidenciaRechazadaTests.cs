using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Corregir un registro que el CCO rechazó y devolverlo a la cola (JTT-291 CA 8).
/// </summary>
public sealed class CorregirIncidenciaRechazadaTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	private static readonly TipoIncidencia Otro = new(107, "Otro", ExigeDescripcion: true);

	private static readonly SeveridadIncidencia Advertencia =
		new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Advertencia", 2, "#EDD611");

	private readonly IIncidentRepository _incidencias = Substitute.For<IIncidentRepository>();

	private CorregirIncidenciaRechazada CrearCaso() => new(_incidencias);

	// ---------- Las mismas comprobaciones que al convertir un borrador ----------

	[Fact]
	public async Task SinTipo_noCorrigeYLoDice()
	{
		var resultado = await EjecutarAsync(tipo: null, kilometro: "130+200", nota: "nota");

		Assert.Equal(ResultadoCorreccionRechazada.FaltaTipo, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task SinSeveridad_noCorrigeYLoDice()
	{
		var resultado = await CrearCaso().EjecutarAsync(
			"LOC-000001", Objeto, "130+200", KilometerSource.Manual, severidad: null, "nota");

		Assert.Equal(ResultadoCorreccionRechazada.FaltaSeveridad, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task ConKilometroInvalido_noCorrige()
	{
		// Es el rechazo más probable —km fuera de corredor— y el que más se va a corregir: el
		// formulario tiene que atajar un formato malo antes de mandar un segundo rechazo seguro.
		var resultado = await EjecutarAsync(Objeto, kilometro: "130+", nota: "nota");

		Assert.Equal(ResultadoCorreccionRechazada.KilometroInvalido, resultado);
		await NoTocoElRepositorioAsync();
	}

	[Fact]
	public async Task SiElTipoExigeDescripcion_unaNotaCortaNoCorrige()
	{
		var resultado = await EjecutarAsync(Otro, kilometro: "130+200", nota: "corta");

		Assert.Equal(ResultadoCorreccionRechazada.NotaInsuficiente, resultado);
		await NoTocoElRepositorioAsync();
	}

	// ---------- Cuando está completa, vuelve a la cola ----------

	[Fact]
	public async Task CuandoTodoEstaCompleto_corrigeYRecortaLaNota()
	{
		RepositorioContesta(true);

		var resultado = await EjecutarAsync(Objeto, "130+200", nota: "  con espacios  ");

		Assert.Equal(ResultadoCorreccionRechazada.Corregida, resultado);
		await _incidencias.Received(1).CorregirRechazadaAsync(
			"LOC-000001",
			Objeto,
			Arg.Is<Kilometer>(k => k != null && k.Valor == "130+200"),
			KilometerSource.Manual,
			Advertencia,
			"con espacios",
			Arg.Any<PosicionDispositivo?>(),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task NoTocaElBorrador_niCreaOtraIncidencia()
	{
		// Corregir actúa sobre el registro rechazado: ni lo convierte como borrador ni guarda
		// uno nuevo. Si no, quedarían dos registros del mismo hecho.
		RepositorioContesta(true);

		await EjecutarAsync(Objeto, "130+200", "nota");

		await _incidencias.DidNotReceiveWithAnyArgs().ConvertirBorradorAsync(
			default!, default!, default!, default, default!, default!, default);
		await _incidencias.DidNotReceiveWithAnyArgs().GuardarAsync(
			default!, default!, default, default!, default!, default);
	}

	[Fact]
	public async Task SiElRegistroYaNoEstaFallido_loDiceEnVezDeFingirQueCorrigio()
	{
		// Pudo salir en una tanda entre que se abrió y que se reenvió, o ser de otra sesión.
		RepositorioContesta(false);

		var resultado = await EjecutarAsync(Objeto, "130+200", "nota");

		Assert.Equal(ResultadoCorreccionRechazada.NoEncontrada, resultado);
	}

	private void RepositorioContesta(bool corregida) =>
		_incidencias.CorregirRechazadaAsync(
			Arg.Any<string>(), Arg.Any<TipoIncidencia>(), Arg.Any<Kilometer>(),
			Arg.Any<KilometerSource>(), Arg.Any<SeveridadIncidencia>(), Arg.Any<string>(),
			Arg.Any<PosicionDispositivo?>(), Arg.Any<CancellationToken>()).Returns(corregida);

	private Task<ResultadoCorreccionRechazada> EjecutarAsync(
		TipoIncidencia? tipo,
		string? kilometro,
		string nota) =>
		CrearCaso().EjecutarAsync(
			"LOC-000001", tipo, kilometro, KilometerSource.Manual, Advertencia, nota);

	private Task NoTocoElRepositorioAsync() =>
		_incidencias.DidNotReceiveWithAnyArgs().CorregirRechazadaAsync(
			default!, default!, default!, default, default!, default!, default);
}
