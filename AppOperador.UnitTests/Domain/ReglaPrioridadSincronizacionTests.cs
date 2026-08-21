using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

/// <summary>
/// JTT-1394 CA 3: «la severidad Crítica tiene prioridad Crítica».
/// </summary>
/// <remarks>
/// La regla dejó de mirar un enum propio de la app y ahora decide por el <b>orden</b> que
/// publica el catálogo de Jacob, donde menor es más grave. Estas pruebas se reescribieron con
/// ese cambio: las anteriores comparaban contra <c>Gravedad.Critica</c>, un valor que la app se
/// inventaba y que ya no existe.
/// </remarks>
public class ReglaPrioridadSincronizacionTests
{
	/// <summary>El catálogo de Dev entrega tres niveles: Crítico 1, Advertencia 2, Información 3.</summary>
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

	/// <summary>
	/// Un catálogo que empezara a numerar en 0 no debe degradar su nivel más grave.
	/// </summary>
	/// <remarks>
	/// Es la razón por la que la regla compara con <c>&lt;=</c> y no con <c>==</c>: numerar
	/// desde cero es una convención tan razonable como numerar desde uno, y equivocarse ahí
	/// mandaría lo más urgente a la cola normal sin que nadie lo notara.
	/// </remarks>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void UnOrdenPorDebajoDelPrimero_SigueSiendoCritico(int orden)
	{
		Assert.Equal(SyncPriority.Critica, ReglaPrioridadSincronizacion.Para(orden));
	}

	/// <summary>
	/// Una severidad sin orden conocido no puede colarse como crítica.
	/// </summary>
	/// <remarks>
	/// Es lo que se guarda cuando el borrador todavía no tiene nivel elegido, o cuando el
	/// servidor manda una severidad sin <c>orden</c>: se le asigna <see cref="int.MaxValue"/>.
	/// </remarks>
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
