using System.Reflection;
using AppOperador.Domain.ValueObjects;
using NetArchTest.Rules;

namespace AppOperador.UnitTests.Architecture;

/// <summary>
/// Prueba A: verifica sobre el ensamblado compilado de <c>AppOperador.Domain</c> que la
/// capa de dominio no depende de ninguna capa externa ni de ninguna tecnología.
/// </summary>
public class DependenciasDelDominioTests
{
	private static readonly Assembly EnsambladoDeDominio = typeof(Kilometer).Assembly;

	/// <summary>
	/// Espacios de nombres prohibidos en el dominio, con el motivo de cada veto.
	/// </summary>
	public static TheoryData<string, string> DependenciasProhibidas => new()
	{
		{ "AppOperador.Aplicacion", "las dependencias apuntan hacia el dominio, no al revés" },
		{ "AppOperador.Infrastructure", "el dominio no conoce la infraestructura" },
		{ "AppOperador.Mobile", "el dominio no conoce la capa de presentación" },
		{ "Microsoft.Maui", "el dominio no depende del framework de interfaz" },
		{ "SQLite", "el dominio no conoce el motor de persistencia" },
		{ "SQLitePCLRaw", "el dominio no conoce el motor de persistencia" },
		{ "System.Net.Http", "el dominio no hace acceso a red" },
	};

	[Theory]
	[MemberData(nameof(DependenciasProhibidas))]
	public void ElDominio_NoDependeDe(string espacioDeNombresProhibido, string motivo)
	{
		var resultado = Types.InAssembly(EnsambladoDeDominio)
			.ShouldNot()
			.HaveDependencyOn(espacioDeNombresProhibido)
			.GetResult();

		Assert.True(resultado.IsSuccessful, ConstruirMensaje(resultado, [espacioDeNombresProhibido], motivo));
	}

	[Fact]
	public void ElDominio_NoDependeDeNingunaDependenciaProhibida()
	{
		var prohibidas = DependenciasProhibidas.Select(fila => (string)fila[0]).ToArray();

		var resultado = Types.InAssembly(EnsambladoDeDominio)
			.ShouldNot()
			.HaveDependencyOnAny(prohibidas)
			.GetResult();

		Assert.True(
			resultado.IsSuccessful,
			ConstruirMensaje(resultado, prohibidas, "el dominio debe permanecer libre de capas externas y tecnologías"));
	}

	[Fact]
	public void ElDetectorDeDependencias_RealmenteDetecta()
	{
		// Contra-prueba: si NetArchTest no viera las dependencias del ensamblado, todas
		// las reglas "ShouldNot" de arriba pasarían sin comprobar nada. Kilometer usa
		// System.Text.RegularExpressions, así que prohibirlo TIENE que fallar.
		var resultado = Types.InAssembly(EnsambladoDeDominio)
			.ShouldNot()
			.HaveDependencyOn("System.Text.RegularExpressions")
			.GetResult();

		Assert.False(
			resultado.IsSuccessful,
			"La contra-prueba pasó, lo que significa que NetArchTest no está detectando " +
			"dependencias reales y las demás reglas de esta clase no verifican nada.");

		Assert.Contains(
			resultado.FailingTypeNames ?? [],
			nombre => nombre.Contains(nameof(Kilometer), StringComparison.Ordinal));
	}

	[Fact]
	public void ElEnsambladoDeDominio_TieneTiposQueAnalizar()
	{
		// Una regla "ShouldNot" sobre un ensamblado vacío pasaría siempre. Este control
		// evita que las pruebas anteriores queden en verde por no tener nada que revisar.
		var tipos = Types.InAssembly(EnsambladoDeDominio).GetTypes().ToArray();

		Assert.NotEmpty(tipos);
	}

	/// <summary>
	/// Construye un mensaje que nombra los tipos infractores, en vez de limitarse a
	/// decir que la regla falló.
	/// </summary>
	private static string ConstruirMensaje(TestResult resultado, IReadOnlyCollection<string> prohibidas, string motivo)
	{
		var infractores = resultado.FailingTypeNames?.ToArray() ?? [];

		var detalle = infractores.Length > 0
			? string.Join(Environment.NewLine, infractores.Select(t => $"  - {t}"))
			: "  (NetArchTest no devolvió los nombres de los tipos infractores)";

		return $"""
			AppOperador.Domain violó la regla de dependencias: {motivo}.

			Espacios de nombres prohibidos:
			  {string.Join(", ", prohibidas)}

			Tipos infractores ({infractores.Length}):
			{detalle}

			Corrige el dominio: mueve esa dependencia a AppOperador.Aplicacion o a
			AppOperador.Infrastructure, o exprésala como una interfaz del dominio.
			""";
	}
}
