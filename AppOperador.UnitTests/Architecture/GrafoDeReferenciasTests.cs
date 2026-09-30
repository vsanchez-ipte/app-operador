using System.Xml.Linq;

namespace AppOperador.UnitTests.Architecture;

public class GrafoDeReferenciasTests
{
	private const string Domain = "AppOperador.Domain";
	private const string Aplicacion = "AppOperador.Aplicacion";
	private const string Infrastructure = "AppOperador.Infrastructure";
	private const string Mobile = "AppOperador.Mobile";
	private const string UnitTests = "AppOperador.UnitTests";

	[Fact]
	public void Domain_NoTieneNingunaReferenciaDeProyecto()
	{
		var referencias = LeerReferencias(Domain);

		Assert.True(
			referencias.Count == 0,
			$"""
			{Domain} debe permanecer sin referencias a otros proyectos: es el centro de la arquitectura.

			Referencias que sobran ({referencias.Count}): {Enumerar(referencias)}
			Archivo: {RutaRelativaDelProyecto(Domain)}
			""");
	}

	[Fact]
	public void Aplicacion_ReferenciaUnicamenteADomain()
	{
		var referencias = LeerReferencias(Aplicacion);
		var sobran = referencias.Where(r => r != Domain).ToArray();

		Assert.True(
			sobran.Length == 0,
			$"""
			{Aplicacion} solo puede referenciar a {Domain}.

			Referencias que sobran ({sobran.Length}): {Enumerar(sobran)}
			Referencias actuales: {Enumerar(referencias)}
			Archivo: {RutaRelativaDelProyecto(Aplicacion)}
			""");

		Assert.True(
			referencias.Contains(Domain),
			$"{Aplicacion} debe referenciar a {Domain}, pero sus referencias son: {Enumerar(referencias)}");
	}

	[Fact]
	public void Infrastructure_NoReferenciaAMobile()
	{
		VerificarQueNoReferencia(Infrastructure, [Mobile],
			"la infraestructura no puede depender de la capa de presentación");
	}

	[Fact]
	public void UnitTests_NoReferenciaAInfrastructureNiAMobile()
	{
		VerificarQueNoReferencia(UnitTests, [Infrastructure, Mobile],
			"las pruebas de esta pasada cubren dominio y aplicación únicamente");
	}

	[Fact]
	public void LaRaizDelRepositorio_SeLocalizaDesdeElDirectorioDeEjecucion()
	{
		// Si esto falla, los demás mensajes de esta clase serían engañosos.
		var raiz = LocalizarRaizDelRepositorio();

		Assert.True(raiz.EnumerateFiles("*.slnx").Any(), $"No se encontró ningún .slnx en {raiz.FullName}.");
	}

	[Theory]
	[InlineData(Domain)]
	[InlineData(Aplicacion)]
	[InlineData(Infrastructure)]
	[InlineData(Mobile)]
	[InlineData(UnitTests)]
	public void CadaProyectoDeLaSolucion_ExisteEnDisco(string proyecto)
	{
		var ruta = RutaAbsolutaDelProyecto(proyecto);

		Assert.True(File.Exists(ruta), $"No se encontró el archivo de proyecto esperado en {ruta}.");
	}

	private static void VerificarQueNoReferencia(string proyecto, string[] prohibidos, string motivo)
	{
		var referencias = LeerReferencias(proyecto);
		var infractoras = referencias.Intersect(prohibidos).ToArray();

		Assert.True(
			infractoras.Length == 0,
			$"""
			{proyecto} no puede referenciar a {Enumerar(prohibidos)}: {motivo}.

			Referencias que sobran ({infractoras.Length}): {Enumerar(infractoras)}
			Referencias actuales: {Enumerar(referencias)}
			Archivo: {RutaRelativaDelProyecto(proyecto)}
			""");
	}

	private static IReadOnlyList<string> LeerReferencias(string proyecto)
	{
		var ruta = RutaAbsolutaDelProyecto(proyecto);

		if (!File.Exists(ruta))
		{
			throw new FileNotFoundException(
				$"No se encontró el archivo de proyecto de {proyecto}. Ruta esperada: {ruta}.", ruta);
		}

		return XDocument.Load(ruta)
			.Descendants()
			// Por nombre local: sirve con .csproj estilo SDK y con los antiguos.
			.Where(elemento => elemento.Name.LocalName == "ProjectReference")
			.Select(elemento => (string?)elemento.Attribute("Include"))
			.Where(include => !string.IsNullOrWhiteSpace(include))
			.Select(include => NombreDeProyectoDesdeRuta(include!))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string NombreDeProyectoDesdeRuta(string include)
	{
		var normalizada = include.Replace('\\', '/');
		var ultimoSegmento = normalizada[(normalizada.LastIndexOf('/') + 1)..];

		return Path.GetFileNameWithoutExtension(ultimoSegmento);
	}

	private static string RutaAbsolutaDelProyecto(string proyecto) =>
		Path.Combine(LocalizarRaizDelRepositorio().FullName, proyecto, $"{proyecto}.csproj");

	private static string RutaRelativaDelProyecto(string proyecto) =>
		Path.Combine(proyecto, $"{proyecto}.csproj");

	private static DirectoryInfo LocalizarRaizDelRepositorio()
	{
		var directorio = new DirectoryInfo(AppContext.BaseDirectory);
		var recorrido = new List<string>();

		while (directorio is not null)
		{
			recorrido.Add(directorio.FullName);

			if (directorio.EnumerateFiles("*.slnx").Any())
			{
				return directorio;
			}

			directorio = directorio.Parent;
		}

		throw new InvalidOperationException(
			"No se pudo localizar la raíz del repositorio: ningún directorio ascendente contiene un archivo .slnx." +
			Environment.NewLine + "Directorios recorridos:" + Environment.NewLine +
			string.Join(Environment.NewLine, recorrido.Select(d => $"  - {d}")));
	}

	private static string Enumerar(IReadOnlyCollection<string> nombres) =>
		nombres.Count == 0 ? "(ninguna)" : string.Join(", ", nombres);
}
