using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// Los borradores son de quien los escribió (JTT-1388 CA 9).
/// </summary>
/// <remarks>
/// Lo mismo que <see cref="ColaPorOperadorTests"/> protege para la cola, pero en la otra
/// superficie donde quedan datos pendientes: la lista de borradores de la pantalla de captura.
/// Un borrador no se sincroniza, así que la cola no lo cubre; y sigue siendo trabajo a medio
/// hacer de un operador concreto, que no debe pasar al siguiente por haber entrado después.
/// </remarks>
public sealed class BorradoresPorOperadorTests
{
	private static readonly TipoIncidencia Objeto = new("OBJETO", "Objeto en camino");

	[Fact]
	public async Task El_operador_siguiente_no_ve_los_borradores_del_anterior()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarBorradorAsync(contexto);

		var otro = contexto.CrearRepositorioDe(new SesionFija("otro.operador"));

		Assert.Empty(await otro.ObtenerBorradoresAsync());
	}

	[Fact]
	public async Task Los_borradores_del_anterior_siguen_guardados()
	{
		// El complemento: no verlos no es haberlos borrado. Quien lo escribió lo recupera.
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarBorradorAsync(contexto);

		_ = await contexto.CrearRepositorioDe(new SesionFija("otro.operador")).ObtenerBorradoresAsync();

		var suyo = Assert.Single(await contexto.CrearRepositorio().ObtenerBorradoresAsync());
		Assert.Equal(clave, suyo.ClaveLocal);
		Assert.Equal(EstadoSincronizacion.Borrador, suyo.Estado);
	}

	[Fact]
	public async Task Sin_sesion_abierta_no_se_ve_ningun_borrador()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarBorradorAsync(contexto);
		var repositorio = contexto.CrearRepositorio();

		contexto.Sesion.Limpiar();

		Assert.Empty(await repositorio.ObtenerBorradoresAsync());
	}

	private static Task<string> GuardarBorradorAsync(ContextoSqlite contexto) =>
		contexto.CrearRepositorio().GuardarBorradorAsync(
			Objeto,
			"130+200",
			Gravedad.Media,
			"nota de prueba");
}
