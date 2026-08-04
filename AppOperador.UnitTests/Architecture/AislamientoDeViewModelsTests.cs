using System.Text.RegularExpressions;

namespace AppOperador.UnitTests.Architecture;

/// <summary>
/// Prueba C: verifica que ningún ViewModel dependa de <c>AppOperador.Infrastructure</c>.
/// </summary>
/// <remarks>
/// <para>
/// Es el segundo criterio de la prueba de arquitectura que pide CA2 de JTT-1338. No basta
/// con revisar las referencias entre proyectos: <c>AppOperador.Mobile</c> referencia a
/// <c>AppOperador.Infrastructure</c> de forma legítima, porque <c>MauiProgram</c> tiene
/// que registrar las implementaciones concretas en el contenedor. Lo que no puede ocurrir
/// es que un ViewModel conozca un tipo de infraestructura: debe depender solo de las
/// interfaces de <c>AppOperador.Aplicacion</c>.
/// </para>
/// <para>
/// La verificación se hace sobre el código fuente, no sobre el ensamblado compilado, por
/// la misma razón que la prueba B: <c>AppOperador.Mobile</c> es multi-target y el
/// proyecto de pruebas no puede cargarlo. Esto detecta el caso realista —un <c>using</c>
/// o un nombre calificado— aunque no analice la IL.
/// </para>
/// </remarks>
public class AislamientoDeViewModelsTests
{
	private const string CarpetaViewModels = "ViewModels";
	private const string ProyectoMobile = "AppOperador.Mobile";
	private const string EspacioProhibido = "AppOperador.Infrastructure";

	[Fact]
	public void NingunViewModel_ReferenciaATiposDeInfrastructure()
	{
		var infractores = BuscarInfractores(EspacioProhibido);

		Assert.True(
			infractores.Count == 0,
			$"""
			Un ViewModel no puede conocer {EspacioProhibido}: debe depender solo de las
			interfaces de AppOperador.Aplicacion, para que la implementación se pueda
			sustituir sin tocar la presentación.

			Archivos infractores ({infractores.Count}):
			{string.Join(Environment.NewLine, infractores.Select(i => $"  - {i}"))}

			Corrige el ViewModel: inyecta la interfaz correspondiente y deja el registro del
			tipo concreto en MauiProgram, que es el único punto de composición.
			""");
	}

	[Fact]
	public void NingunViewModel_ReferenciaALosSimuladores()
	{
		// Los simuladores son temporales y desaparecen al integrar el canal real. Si un
		// ViewModel nombra uno, deja de poder trabajar contra la implementación definitiva.
		var infractores = BuscarInfractores($"{ProyectoMobile}.Mocks");
		infractores.AddRange(BuscarInfractores("Mocks."));

		Assert.True(
			infractores.Count == 0,
			$"""
			Un ViewModel no puede nombrar un simulador de la carpeta Mocks: son andamiaje
			temporal y se retiran al integrar el canal móvil de Jacob.

			Archivos infractores ({infractores.Distinct().Count()}):
			{string.Join(Environment.NewLine, infractores.Distinct().Select(i => $"  - {i}"))}
			""");
	}

	[Fact]
	public void LaCarpetaDeViewModels_TieneArchivosQueAnalizar()
	{
		// Sin este control, las dos pruebas anteriores pasarían siempre si la carpeta
		// estuviera vacía o cambiara de nombre.
		var archivos = ArchivosDeViewModels();

		Assert.True(
			archivos.Length > 0,
			$"No se encontró ningún archivo .cs en {ProyectoMobile}\\{CarpetaViewModels}. " +
			"Si la carpeta cambió de nombre, actualiza esta prueba.");
	}

	/// <summary>
	/// Devuelve los archivos de ViewModel que mencionan un espacio de nombres prohibido.
	/// </summary>
	private static List<string> BuscarInfractores(string espacioProhibido)
	{
		var infractores = new List<string>();
		var patron = new Regex($@"\b{Regex.Escape(espacioProhibido)}", RegexOptions.Compiled);

		foreach (var archivo in ArchivosDeViewModels())
		{
			var contenido = File.ReadAllText(archivo.FullName);
			if (patron.IsMatch(contenido))
			{
				infractores.Add(archivo.Name);
			}
		}

		return infractores;
	}

	private static FileInfo[] ArchivosDeViewModels()
	{
		var carpeta = new DirectoryInfo(
			Path.Combine(LocalizarRaizDelRepositorio().FullName, ProyectoMobile, CarpetaViewModels));

		return carpeta.Exists ? carpeta.GetFiles("*.cs", SearchOption.AllDirectories) : [];
	}

	/// <summary>
	/// Sube desde el directorio de ejecución hasta encontrar el archivo de solución.
	/// </summary>
	private static DirectoryInfo LocalizarRaizDelRepositorio()
	{
		var directorio = new DirectoryInfo(AppContext.BaseDirectory);

		while (directorio is not null)
		{
			if (directorio.EnumerateFiles("*.slnx").Any())
			{
				return directorio;
			}

			directorio = directorio.Parent;
		}

		throw new InvalidOperationException(
			"No se pudo localizar la raíz del repositorio: ningún directorio ascendente contiene un archivo .slnx." +
			$" Se buscó desde {AppContext.BaseDirectory}.");
	}
}
