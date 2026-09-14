using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using NSubstitute;

namespace AppOperador.UnitTests.Application;

/// <summary>Espacio libre y evidencias pendientes para el perfil (JTT-292 CA 4 y 6).</summary>
public sealed class ConsultarAlmacenamientoLocalTests
{
	private readonly ISessionStore _sesiones = Substitute.For<ISessionStore>();
	private readonly IRepositorioEvidencias _evidencias = Substitute.For<IRepositorioEvidencias>();
	private readonly IEspacioDispositivo _espacio = Substitute.For<IEspacioDispositivo>();

	public ConsultarAlmacenamientoLocalTests()
	{
		_espacio.Medir().Returns(new EspacioDispositivo(25L * 1024 * 1024 * 1024, 100L * 1024 * 1024 * 1024));
	}

	private ConsultarAlmacenamientoLocal Crear() => new(_sesiones, _evidencias, _espacio);

	private static EvidenciaPendiente Pendiente(string uuid, long bytes, EstadoSincronizacion estado = EstadoSincronizacion.Pendiente) =>
		new(uuid, $"{uuid}.jpg", "image/jpeg", bytes, estado, "LOC-000001");

	[Fact]
	public async Task ConSesion_trae_lasPendientesDelOperadorYElEspacio()
	{
		ConSesionDe("Juan Pérez");
		_evidencias.ObtenerPendientesDelOperadorAsync("Juan Pérez", Arg.Any<CancellationToken>())
			.Returns([Pendiente("a", 1000), Pendiente("b", 2500, EstadoSincronizacion.Fallido)]);

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(2, resultado.CuantasPendientes);
		Assert.Equal(3500, resultado.BytesPendientes);
		Assert.Equal(25, resultado.Espacio.PorcentajeLibre);
	}

	[Fact]
	public async Task SinSesion_noConsultaEvidenciasPeroSiMideElEspacio()
	{
		_sesiones.Actual.Returns((SesionOperador?)null);

		var resultado = await Crear().EjecutarAsync();

		Assert.Equal(0, resultado.CuantasPendientes);
		Assert.Equal(25, resultado.Espacio.PorcentajeLibre);
		await _evidencias.DidNotReceiveWithAnyArgs().ObtenerPendientesDelOperadorAsync(default!, default);
	}

	[Fact]
	public async Task ConEspacioDesconocido_elPorcentajeEsNulo()
	{
		ConSesionDe("Juan Pérez");
		_espacio.Medir().Returns(EspacioDispositivo.Desconocido);

		var resultado = await Crear().EjecutarAsync();

		Assert.Null(resultado.Espacio.PorcentajeLibre);
	}

	private void ConSesionDe(string operador)
	{
		var vigencia = VigenciaOffline.Validada(new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc));
		_sesiones.Actual.Returns(new SesionOperador(
			operador, "Operador de campo", "VEH-01", vigencia, PermisosOperador.Ninguno,
			"1.0", new DateOnly(2026, 9, 1), sessionId: "s-1"));
	}
}
