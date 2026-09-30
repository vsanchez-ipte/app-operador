using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

public class ActualizarCatalogoLocalTests
{
	private static CatalogosOperacion Catalogo() =>
		new(
			new DateOnly(2026, 8, 20),
			[new TipoIncidencia(11, "Choque por alcance")],
			[new SeveridadIncidencia(Guid.NewGuid(), "Crítico", 1, "#EB1409")],
			[],
			[],
			LimitesEvidencia.Desconocidos);

	private sealed class ClienteFalso(CatalogosOperacion? respuesta) : ICatalogosJacobClient
	{
		public int Llamadas { get; private set; }

		public Task<CatalogosOperacion?> ObtenerVigentesAsync(
			string accessToken, CancellationToken cancelacion = default)
		{
			Llamadas++;
			return Task.FromResult(respuesta);
		}
	}

	private sealed class RepositorioFalso : ICatalogoRepository
	{
		public CatalogosOperacion? Guardado { get; private set; }

		public Task<CatalogosOperacion> ObtenerAsync(CancellationToken cancelacion = default) =>
			Task.FromResult(Guardado ?? CatalogosOperacion.Vacio);

		public Task ReemplazarAsync(
			CatalogosOperacion catalogos, CancellationToken cancelacion = default)
		{
			Guardado = catalogos;
			return Task.CompletedTask;
		}
	}

	[Fact]
	public async Task ConCatalogoNuevo_loGuarda()
	{
		var repositorio = new RepositorioFalso();
		var caso = new ActualizarCatalogoLocal(new ClienteFalso(Catalogo()), repositorio);

		var actualizado = await caso.EjecutarAsync("token");

		Assert.True(actualizado);
		Assert.NotNull(repositorio.Guardado);
		Assert.Equal(new DateOnly(2026, 8, 20), repositorio.Guardado!.Version);
	}

	[Fact]
	public async Task SinRespuestaDelServidor_noPisaLaCopiaLocal()
	{
		var repositorio = new RepositorioFalso();
		await repositorio.ReemplazarAsync(Catalogo());

		var caso = new ActualizarCatalogoLocal(new ClienteFalso(null), repositorio);

		var actualizado = await caso.EjecutarAsync("token");

		Assert.False(actualizado);
		Assert.NotNull(repositorio.Guardado);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public async Task SinToken_niSiquieraLlamaAlServidor(string? token)
	{
		var cliente = new ClienteFalso(Catalogo());
		var caso = new ActualizarCatalogoLocal(cliente, new RepositorioFalso());

		var actualizado = await caso.EjecutarAsync(token);

		Assert.False(actualizado);
		Assert.Equal(0, cliente.Llamadas);
	}
}
