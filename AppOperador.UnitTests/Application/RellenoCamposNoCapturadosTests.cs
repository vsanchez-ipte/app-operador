using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Relleno de los campos que Jacob exige y el formulario no captura (JTT-1401).
/// </summary>
/// <remarks>
/// <b>Estas pruebas nacen de un defecto visto en el CCO el 21-ago:</b> las incidencias de la app
/// llegaban declarando la vía «Total» —completamente cerrada— sin que nadie lo hubiera observado,
/// porque el relleno tomaba la primera afectación del catálogo y la primera es la más grave.
/// </remarks>
public sealed class RellenoCamposNoCapturadosTests
{
	private static IncidenciaEnviable Incidencia() => new(
		"uuid-1", "LOC-000001", 11, Guid.NewGuid(), "130+200", KilometerSource.Manual,
		"nota", new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc), "sesion-1",
		EstadoSincronizacion.Pendiente, 0, DateTime.UtcNow, null);

	private static CatalogosOperacion Catalogo(
		IReadOnlyList<AfectacionIncidencia> afectaciones,
		IReadOnlyList<CuerpoVia> cuerpos) =>
		new(new DateOnly(2026, 8, 20), [new TipoIncidencia(11, "Objeto")], [], afectaciones, cuerpos);

	[Fact]
	public void NuncaRellenaConElCierreTotal()
	{
		// El catálogo real ordena de mayor a menor gravedad: Total, Parcial, Sin afectación.
		var catalogo = Catalogo(
			[new(1, "Total"), new(2, "Parcial"), new(3, "Sin afectación")],
			[new("A", "Cuerpo A")]);

		var envio = RellenoCamposNoCapturados.Completar(Incidencia(), catalogo);

		Assert.Equal(3, envio.IdAfectacion);
	}

	[Fact]
	public void EncuentraSinAfectacionAunqueVengaSinTilde()
	{
		// Comparar el literal acentuado haría que el relleno cayera al peor valor por un
		// detalle ortográfico del catálogo.
		var catalogo = Catalogo(
			[new(1, "Total"), new(7, "SIN AFECTACION")],
			[new("A", "Cuerpo A")]);

		var envio = RellenoCamposNoCapturados.Completar(Incidencia(), catalogo);

		Assert.Equal(7, envio.IdAfectacion);
	}

	[Fact]
	public void SinLaOpcionNeutraTomaLaUltimaYNoLaPrimera()
	{
		// La última es la apuesta menos dañina si el catálogo conserva su orden por gravedad.
		var catalogo = Catalogo(
			[new(1, "Total"), new(2, "Parcial")],
			[new("A", "Cuerpo A")]);

		var envio = RellenoCamposNoCapturados.Completar(Incidencia(), catalogo);

		Assert.Equal(2, envio.IdAfectacion);
	}

    [Fact]
	public void PrefiereAmbosCuerposParaNoDirigirAUnLadoEquivocado()
	{
		// Declarar "Cuerpo A" manda a quien atienda a un lado concreto de una vía de dos
		// cuerpos, y equivocarse cuesta un recorrido completo.
		var catalogo = Catalogo(
			[new(3, "Sin afectación")],
			[new("A", "Cuerpo A"), new("B", "Cuerpo B"), new("C", "Ambos cuerpos")]);

		var envio = RellenoCamposNoCapturados.Completar(Incidencia(), catalogo);

		Assert.Equal("C", envio.Cuerpo);
	}

	[Fact]
	public void ElKilometroViajaComoDecimalInvariante()
	{
		var catalogo = Catalogo([new(3, "Sin afectación")], [new("C", "Ambos")]);

		var envio = RellenoCamposNoCapturados.Completar(Incidencia(), catalogo);

		Assert.Equal(130.200m, envio.Km);
	}

	[Fact]
	public void UnCatalogoVacioNoRompeElEnvio()
	{
		var envio = RellenoCamposNoCapturados.Completar(Incidencia(), Catalogo([], []));

		Assert.Equal(0, envio.IdAfectacion);
		Assert.Equal("C", envio.Cuerpo);
	}
}
