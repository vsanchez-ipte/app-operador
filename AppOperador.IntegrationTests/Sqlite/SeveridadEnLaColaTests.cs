using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// La severidad que la Cola muestra es la que se capturó, contra una base real (JTT-1398).
/// </summary>
/// <remarks>
/// <b>Existe por un defecto que ninguna prueba podía atrapar.</b> La pantalla mostraba la
/// <c>Prioridad</c> rotulada como severidad, y la prioridad solo tiene dos valores: toda
/// severidad que no fuera crítica —Advertencia, Información, Normal— se leía «Normal». El
/// operador que capturaba una Advertencia veía otra cosa en la Cola.
/// <para>
/// <b>Y el dato nunca faltó:</b> <c>SeveridadNombre</c> llevaba guardado en la fila desde
/// JTT-1394. Lo que no había era quien lo llevara de la base al modelo, así que la prueba tiene
/// que ir contra la base: en memoria, el mapeo que fallaba no participa.
/// </para>
/// </remarks>
public sealed class SeveridadEnLaColaTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	/// <summary>Las tres que compartían prioridad Normal y se veían iguales.</summary>
	[Theory]
	[InlineData("Advertencia", 2)]
	[InlineData("Información", 3)]
	[InlineData("Normal", 4)]
	public async Task LaSeveridadCapturadaEsLaQueLaColaMuestra(string nombre, int orden)
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Severidad(nombre, orden));

		var registro = Assert.Single(
			await contexto.CrearCola().ObtenerRegistrosAsync(),
			r => r.ClaveLocal == clave);

		Assert.Equal(nombre, registro.SeveridadLegible);

		// La prioridad sigue siendo Normal en las tres: es justo lo que las hacía
		// indistinguibles cuando la pantalla mostraba prioridad en su lugar.
		Assert.Equal(SyncPriority.Normal, registro.Prioridad);
	}

	[Fact]
	public async Task UnaCriticaSeSigueDistinguiendo()
	{
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto, Severidad("Crítica", 1));

		var registro = Assert.Single(
			await contexto.CrearCola().ObtenerRegistrosAsync(),
			r => r.ClaveLocal == clave);

		Assert.Equal("Crítica", registro.SeveridadLegible);
		Assert.Equal(SyncPriority.Critica, registro.Prioridad);
	}

	[Fact]
	public async Task DosSeveridadesDistintasNoSeVenIgual()
	{
		// La comprobación que resume el defecto: antes las dos decían "Normal".
		await using var contexto = new ContextoSqlite();
		var advertencia = await GuardarAsync(contexto, Severidad("Advertencia", 2));
		var informacion = await GuardarAsync(contexto, Severidad("Información", 3));

		var registros = await contexto.CrearCola().ObtenerRegistrosAsync();

		Assert.NotEqual(
			Assert.Single(registros, r => r.ClaveLocal == advertencia).SeveridadLegible,
			Assert.Single(registros, r => r.ClaveLocal == informacion).SeveridadLegible);
	}

	private static SeveridadIncidencia Severidad(string nombre, int orden) =>
		new(Guid.NewGuid(), nombre, orden, "#888888");

	private static Task<string> GuardarAsync(ContextoSqlite contexto, SeveridadIncidencia severidad) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			severidad,
			"nota de prueba");
}
