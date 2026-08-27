using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Catálogo del recorrido simulado (JTT-1394).
/// </summary>
/// <remarks>
/// <para>
/// El simulador hace de Jacob, igual que <see cref="ServicioAutenticacionSimulado"/> hace de
/// Jacob para los permisos. <b>Hace falta desde JTT-1394</b>: al retirar la siembra de la base,
/// el recorrido simulado se quedaba sin tipos ni severidades y no se podía capturar nada.
/// </para>
/// <para>
/// <b>Los valores imitan a los reales, no los inventan.</b> Los tipos y sus identificadores
/// salen del catálogo de Dev, y las tres severidades son las que Jacob publica de verdad, con
/// su orden y su color. Sembrar valores de fantasía es lo que llevó a creer durante semanas que
/// el catálogo estaba resuelto.
/// </para>
/// <para>
/// La lista es corta a propósito: en Dev hay cien tipos, y arrastrarlos aquí no haría más
/// realista el recorrido, solo más incómodo de leer.
/// </para>
/// </remarks>
public sealed class CatalogoSimulado : ICatalogosJacobClient
{
	/// <inheritdoc />
	/// <remarks>
	/// No consulta nada ni puede fallar: el recorrido simulado no tiene servidor al que llamar.
	/// </remarks>
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

				// El que obliga a capturar nota. Se reconoce por la bandera, nunca por el
				// nombre ni por el id: en cada ambiente tiene uno distinto (JTT-1397).
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

			// Los mismos que publica el servidor hoy. Aquí sí van escritos, porque este
			// catálogo ES el simulador: no hay servidor del que leerlos. En el camino real
			// nunca se codifican (JTT-1398).
			new LimitesEvidencia(["image/jpeg", "image/png", "image/bmp", "application/pdf"], 5, 3));

		return Task.FromResult<CatalogosOperacion?>(catalogos);
	}
}
