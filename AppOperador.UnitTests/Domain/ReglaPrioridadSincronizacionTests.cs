using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public class ReglaPrioridadSincronizacionTests
{
	private const int OrdenCritico = 1;

	[Fact]
	public void ElNivelMasGrave_ProducePrioridadCritica()
	{
		Assert.Equal(SyncPriority.Critica, ReglaPrioridadSincronizacion.Para(OrdenCritico));
	}

	[Theory]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(10)]
	public void LosDemasNiveles_ProducenPrioridadNormal(int orden)
	{
		Assert.Equal(SyncPriority.Normal, ReglaPrioridadSincronizacion.Para(orden));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void UnOrdenPorDebajoDelPrimero_SigueSiendoCritico(int orden)
	{
		Assert.Equal(SyncPriority.Critica, ReglaPrioridadSincronizacion.Para(orden));
	}

	[Fact]
	public void UnOrdenDesconocido_NoEscalaAPrioridadCritica()
	{
		Assert.Equal(SyncPriority.Normal, ReglaPrioridadSincronizacion.Para(int.MaxValue));
	}

	[Fact]
	public void LaRegla_SiempreDevuelveUnaPrioridadDefinida()
	{
		foreach (var orden in new[] { int.MinValue, -1, 0, 1, 2, 99, int.MaxValue })
		{
			var prioridad = ReglaPrioridadSincronizacion.Para(orden);

			Assert.True(
				Enum.IsDefined(prioridad),
				$"El orden {orden} produjo la prioridad no definida {(int)prioridad}.");
		}
	}

	[Fact]
	public void LaRegla_EsPura_MismaEntradaMismaSalida()
	{
		Assert.Equal(
			ReglaPrioridadSincronizacion.Para(OrdenCritico),
			ReglaPrioridadSincronizacion.Para(OrdenCritico));
	}
}
