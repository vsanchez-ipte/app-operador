using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public class ReglaPrioridadSincronizacionTests
{
	[Fact]
	public void GravedadCritica_ProducePrioridadCritica()
	{
		// Caso cerrado por la regla de negocio.
		Assert.Equal(SyncPriority.Critica, ReglaPrioridadSincronizacion.Para(Gravedad.Critica));
	}

	[Theory]
	[InlineData(Gravedad.Baja)]
	[InlineData(Gravedad.Media)]
	[InlineData(Gravedad.Alta)]
	public void GravedadNoCritica_ProducePrioridadNormal(Gravedad gravedad)
	{
		Assert.Equal(SyncPriority.Normal, ReglaPrioridadSincronizacion.Para(gravedad));
	}

	[Fact]
	public void Critica_EsLaUnicaGravedadQueEscalaLaPrioridad()
	{
		var escalan = Enum.GetValues<Gravedad>()
			.Where(g => ReglaPrioridadSincronizacion.Para(g) == SyncPriority.Critica)
			.ToArray();

		Assert.Equal([Gravedad.Critica], escalan);
	}

	[Fact]
	public void LaRegla_EsTotalSobreTodasLasGravedadesDefinidas()
	{
		// Ninguna gravedad puede quedarse sin prioridad asignada.
		foreach (var gravedad in Enum.GetValues<Gravedad>())
		{
			var prioridad = ReglaPrioridadSincronizacion.Para(gravedad);

			Assert.True(
				Enum.IsDefined(prioridad),
				$"La gravedad {gravedad} produjo la prioridad no definida {(int)prioridad}.");
		}
	}

	[Fact]
	public void LaRegla_EsPura_MismaEntradaMismaSalida()
	{
		Assert.Equal(
			ReglaPrioridadSincronizacion.Para(Gravedad.Critica),
			ReglaPrioridadSincronizacion.Para(Gravedad.Critica));
	}

	[Fact]
	public void ValorNoDefinido_NoEscalaAPrioridadCritica()
	{
		// Un entero fuera de la enumeración no debe colarse como crítico.
		var gravedadInvalida = (Gravedad)999;

		Assert.Equal(SyncPriority.Normal, ReglaPrioridadSincronizacion.Para(gravedadInvalida));
	}
}
