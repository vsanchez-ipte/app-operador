using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Application;

public sealed class EvidenciaRezagadaTests
{
	private static readonly DateTime Las10 = new(2026, 9, 25, 10, 0, 0, DateTimeKind.Utc);

	private static EvidenciaRezagada Rezagada(int intentos, DateTime? ultimo, string? error) =>
		new(new EvidenciaAdjunta(
				"ev-1", "inc-1", "foto.jpg", "image/jpeg", 1024, "/privado/ev-1.jpg",
				EstadoSincronizacion.Fallido, error),
			intentos,
			ultimo);

	[Fact]
	public void UnFalloTecnico_esperaLoMismoQueUnaIncidencia()
	{
		var rezagada = Rezagada(3, Las10, CodigosErrorJacob.ErrorTecnico);

		Assert.Equal(Las10 + ReglaEsperaReintento.Para(3), rezagada.ProximoIntentoUtc);
		Assert.False(rezagada.TocaIntentar(Las10.AddMinutes(1)));
		Assert.True(rezagada.TocaIntentar(Las10 + ReglaEsperaReintento.Para(3)));
	}

	[Fact]
	public void UnaQueNuncaSeIntento_tocaYa()
	{
		var rezagada = Rezagada(0, null, null);

		Assert.True(rezagada.TocaIntentar(Las10));
	}

	[Fact]
	public void UnRechazoDelCco_noTieneProximoIntento()
	{
		var rezagada = Rezagada(1, Las10, "appevidencias.formato.nopermitido");

		Assert.True(rezagada.RechazadaPorElCco);
		Assert.Null(rezagada.ProximoIntentoUtc);
		Assert.False(rezagada.TocaIntentar(Las10.AddDays(1)));
	}
}
