using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using AppOperador.Infrastructure.Sqlite;

namespace AppOperador.IntegrationTests.Sqlite;

public sealed class SeveridadEnLaColaTests
{
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

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
