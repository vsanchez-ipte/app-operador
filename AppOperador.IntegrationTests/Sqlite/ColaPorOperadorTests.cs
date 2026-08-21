using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.IntegrationTests.Sqlite;

/// <summary>
/// La cola pertenece a quien tiene la sesión abierta (JTT-1390 CA 5, 7 y 8).
/// </summary>
/// <remarks>
/// Lo que se protege aquí es la combinación de dos criterios que tiran en direcciones
/// opuestas: los pendientes <b>no se borran</b> al cerrar sesión, pero el operador
/// siguiente <b>no debe verlos</b>. Ambas cosas a la vez solo funcionan si los registros
/// siguen en la base y la consulta los filtra.
/// </remarks>
public sealed class ColaPorOperadorTests
{

	// Niveles del catálogo real de Jacob: Crítico 1, Advertencia 2, Información 3 (JTT-1394).
	private static readonly SeveridadIncidencia Critica =
		new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Crítico", 1, "#EB1409");

	private static readonly SeveridadIncidencia Advertencia =
		new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Advertencia", 2, "#EDD611");

	private static readonly SeveridadIncidencia Informacion =
		new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Información", 3, "#120AF2");
	private static readonly TipoIncidencia Objeto = new(11, "Objeto en camino");

	[Fact]
	public async Task El_operador_siguiente_no_ve_la_cola_del_anterior()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto);

		var otro = contexto.CrearColaDe(new SesionFija("otro.operador"));

		Assert.Empty(await otro.ObtenerRegistrosAsync());
		Assert.Equal(0, await otro.ContarPendientesAsync());
	}

	[Fact]
	public async Task Los_pendientes_del_anterior_siguen_guardados()
	{
		// El complemento del caso anterior: no verlos no es haberlos borrado.
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);

		_ = await contexto.CrearColaDe(new SesionFija("otro.operador")).ObtenerRegistrosAsync();

		var suya = Assert.Single(await contexto.CrearCola().ObtenerRegistrosAsync());
		Assert.Equal(clave, suya.ClaveLocal);
		Assert.Equal(EstadoSincronizacion.Pendiente, suya.Estado);
	}

	[Fact]
	public async Task Las_operaciones_pendientes_conservan_su_identidad_original()
	{
		// CA 8: la clave local nace con el registro y no cambia porque alguien entre o salga.
		await using var contexto = new ContextoSqlite();
		var clave = await GuardarAsync(contexto);
		var sesion = contexto.Sesion;

		sesion.Limpiar();
		var recuperada = new SesionFija();

		var registro = Assert.Single(await contexto.CrearColaDe(recuperada).ObtenerRegistrosAsync());
		Assert.Equal(clave, registro.ClaveLocal);
	}

	[Fact]
	public async Task Sin_sesion_abierta_la_cola_se_ve_vacia()
	{
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto);
		var cola = contexto.CrearCola();

		contexto.Sesion.Limpiar();

		Assert.Empty(await cola.ObtenerRegistrosAsync());
		Assert.Equal(0, await cola.ContarPendientesAsync());
	}

	[Fact]
	public async Task Sin_sesion_abierta_no_se_sincroniza_nada()
	{
		// No hay con qué autenticarse ante Jacob, y los pendientes deben esperar intactos.
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto);
		var cola = contexto.CrearCola();
		contexto.Sesion.Limpiar();

		Assert.Equal(0, await cola.SincronizarAsync());

		var recuperada = contexto.CrearColaDe(new SesionFija());
		var registro = Assert.Single(await recuperada.ObtenerRegistrosAsync());
		Assert.Equal(EstadoSincronizacion.Pendiente, registro.Estado);
	}

	[Fact]
	public async Task No_se_envia_lo_capturado_por_otro_operador()
	{
		// Enviarlo con este token se lo atribuiria a quien no lo capturo.
		await using var contexto = new ContextoSqlite();
		await GuardarAsync(contexto);

		var confirmados = await contexto.CrearColaDe(new SesionFija("otro.operador")).SincronizarAsync();

		Assert.Equal(0, confirmados);
	}

	private static Task<string> GuardarAsync(ContextoSqlite contexto) =>
		contexto.CrearRepositorio().GuardarAsync(
			Objeto,
			Kilometer.Crear("130+200"),
			KilometerSource.GPS,
			Advertencia,
			"nota de prueba");
}
