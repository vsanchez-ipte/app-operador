using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

public sealed class CatalogoSimulado : ICatalogosJacobClient
{
	public Task<CatalogosOperacion?> ObtenerVigentesAsync(
		string accessToken,
		CancellationToken cancelacion = default)
	{
		CatalogosOperacion catalogos = new(
			DateOnly.FromDateTime(DateTime.UtcNow),
			[
				new TipoIncidencia(11, "Choque por alcance"),
				new TipoIncidencia(3, "Caída de material/carga"),
				new TipoIncidencia(28, "Vehículo descompuesto"),
				new TipoIncidencia(52, "Atropellado"),
				new TipoIncidencia(74, "Bacheo"),

				new TipoIncidencia(107, "Otro", ExigeDescripcion: true),
			],
			[
				new SeveridadIncidencia(
					Guid.Parse("a1000000-0000-0000-0000-000000000001"), "Crítico", 1, "#EB1409"),
				new SeveridadIncidencia(
					Guid.Parse("a1000000-0000-0000-0000-000000000002"), "Advertencia", 2, "#EDD611"),
				new SeveridadIncidencia(
					Guid.Parse("a1000000-0000-0000-0000-000000000003"), "Información", 3, "#120AF2"),
			],
			[
				// Es el grado de cierre de la vía, no el carril afectado.
				new AfectacionIncidencia(1, "Total"),
				new AfectacionIncidencia(2, "Parcial"),
				new AfectacionIncidencia(3, "Sin afectación"),
			],
			[
				new CuerpoVia("A", "Cuerpo A"),
				new CuerpoVia("B", "Cuerpo B"),
				new CuerpoVia("C", "Ambos cuerpos"),
				new CuerpoVia("D", "Camellón/cuneta central"),
			],

			new LimitesEvidencia(["image/jpeg", "image/png", "image/bmp", "application/pdf"], 5, 3));

		return Task.FromResult<CatalogosOperacion?>(catalogos);
	}
}
