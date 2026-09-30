using System.Text.RegularExpressions;

namespace AppOperador.UnitTests.Architecture;

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
		// Control: sin archivos, las pruebas anteriores pasarían siempre.
		var archivos = ArchivosDeViewModels();

		Assert.True(
			archivos.Length > 0,
			$"No se encontró ningún archivo .cs en {ProyectoMobile}\\{CarpetaViewModels}. " +
			"Si la carpeta cambió de nombre, actualiza esta prueba.");
	}

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
