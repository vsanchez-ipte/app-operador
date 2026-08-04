using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;

namespace AppOperador.UnitTests.Domain;

public class ReglaTransicionSincronizacionTests
{
	// Las cinco transiciones que el negocio declara válidas.
	public static TheoryData<EstadoSincronizacion, EstadoSincronizacion> TransicionesValidas => new()
	{
		{ EstadoSincronizacion.Borrador, EstadoSincronizacion.Pendiente },
		{ EstadoSincronizacion.Pendiente, EstadoSincronizacion.Enviando },
		{ EstadoSincronizacion.Enviando, EstadoSincronizacion.Sincronizado },
		{ EstadoSincronizacion.Enviando, EstadoSincronizacion.Fallido },
		{ EstadoSincronizacion.Fallido, EstadoSincronizacion.Pendiente },
	};

	[Theory]
	[MemberData(nameof(TransicionesValidas))]
	public void TransicionDeclarada_EsValida(EstadoSincronizacion origen, EstadoSincronizacion destino)
	{
		Assert.True(
			ReglaTransicionSincronizacion.EsTransicionValida(origen, destino),
			$"{origen} → {destino} debería ser válida.");
	}

	[Fact]
	public void CualquierTransicionNoDeclarada_EsInvalida()
	{
		var declaradas = TransicionesValidas
			.Select(fila => ((EstadoSincronizacion)fila[0], (EstadoSincronizacion)fila[1]))
			.ToHashSet();

		var invalidasQuePasaron = new List<string>();

		foreach (var origen in Enum.GetValues<EstadoSincronizacion>())
		{
			foreach (var destino in Enum.GetValues<EstadoSincronizacion>())
			{
				if (declaradas.Contains((origen, destino)))
				{
					continue;
				}

				if (ReglaTransicionSincronizacion.EsTransicionValida(origen, destino))
				{
					invalidasQuePasaron.Add($"{origen} → {destino}");
				}
			}
		}

		Assert.True(
			invalidasQuePasaron.Count == 0,
			"Estas transiciones no están declaradas y deberían haberse rechazado: " +
			string.Join(", ", invalidasQuePasaron));
	}

	[Fact]
	public void Sincronizado_EsTerminal()
	{
		Assert.True(ReglaTransicionSincronizacion.EsTerminal(EstadoSincronizacion.Sincronizado));
		Assert.Empty(ReglaTransicionSincronizacion.DestinosDesde(EstadoSincronizacion.Sincronizado));

		foreach (var destino in Enum.GetValues<EstadoSincronizacion>())
		{
			Assert.False(
				ReglaTransicionSincronizacion.EsTransicionValida(EstadoSincronizacion.Sincronizado, destino),
				$"Sincronizado es terminal, pero admitió salir hacia {destino}.");
		}
	}

	[Fact]
	public void NingunEstadoDistintoDeSincronizado_EsTerminal()
	{
		foreach (var estado in Enum.GetValues<EstadoSincronizacion>().Where(e => e != EstadoSincronizacion.Sincronizado))
		{
			Assert.False(
				ReglaTransicionSincronizacion.EsTerminal(estado),
				$"{estado} no debería ser terminal.");
		}
	}

	[Fact]
	public void UnBorrador_NuncaSeEnvia()
	{
		Assert.False(ReglaTransicionSincronizacion.EsTransicionValida(
			EstadoSincronizacion.Borrador, EstadoSincronizacion.Enviando));
		Assert.False(ReglaTransicionSincronizacion.EsTransicionValida(
			EstadoSincronizacion.Borrador, EstadoSincronizacion.Sincronizado));
		Assert.False(ReglaTransicionSincronizacion.EsTransicionValida(
			EstadoSincronizacion.Borrador, EstadoSincronizacion.Fallido));
	}

	[Fact]
	public void NingunEstado_TransitaHaciaBorrador()
	{
		foreach (var origen in Enum.GetValues<EstadoSincronizacion>())
		{
			Assert.False(
				ReglaTransicionSincronizacion.EsTransicionValida(origen, EstadoSincronizacion.Borrador),
				$"{origen} no debería poder volver a Borrador.");
		}
	}

	[Fact]
	public void NingunEstado_TransitaHaciaSiMismo()
	{
		foreach (var estado in Enum.GetValues<EstadoSincronizacion>())
		{
			Assert.False(
				ReglaTransicionSincronizacion.EsTransicionValida(estado, estado),
				$"{estado} → {estado} no está declarada y debería rechazarse.");
		}
	}

	[Fact]
	public void UnFallido_SoloPuedeReintentarse()
	{
		Assert.Equal(
			[EstadoSincronizacion.Pendiente],
			ReglaTransicionSincronizacion.DestinosDesde(EstadoSincronizacion.Fallido));
	}

	[Fact]
	public void Enviando_TieneExactamenteDosSalidas()
	{
		var destinos = ReglaTransicionSincronizacion.DestinosDesde(EstadoSincronizacion.Enviando);

		Assert.Equal(2, destinos.Count);
		Assert.Contains(EstadoSincronizacion.Sincronizado, destinos);
		Assert.Contains(EstadoSincronizacion.Fallido, destinos);
	}

	[Fact]
	public void CicloCompletoDeReintento_EsRecorrible()
	{
		// Borrador → Pendiente → Enviando → Fallido → Pendiente → Enviando → Sincronizado
		EstadoSincronizacion[] recorrido =
		[
			EstadoSincronizacion.Borrador,
			EstadoSincronizacion.Pendiente,
			EstadoSincronizacion.Enviando,
			EstadoSincronizacion.Fallido,
			EstadoSincronizacion.Pendiente,
			EstadoSincronizacion.Enviando,
			EstadoSincronizacion.Sincronizado,
		];

		for (var i = 0; i < recorrido.Length - 1; i++)
		{
			Assert.True(
				ReglaTransicionSincronizacion.EsTransicionValida(recorrido[i], recorrido[i + 1]),
				$"El paso {recorrido[i]} → {recorrido[i + 1]} del ciclo de reintento debería ser válido.");
		}
	}

	[Fact]
	public void EstadoNoDefinido_NoAdmiteNingunaTransicion()
	{
		var invalido = (EstadoSincronizacion)999;

		Assert.False(ReglaTransicionSincronizacion.EsTransicionValida(invalido, EstadoSincronizacion.Pendiente));
		Assert.False(ReglaTransicionSincronizacion.EsTerminal(invalido));
		Assert.Empty(ReglaTransicionSincronizacion.DestinosDesde(invalido));
	}
}
