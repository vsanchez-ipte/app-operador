using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Dispositivo;

namespace AppOperador.IntegrationTests.Dispositivo;

// Lanzar el visor no existe en pruebas; lo que se fija es la copia: caché, nombre y archivo ausente.
public sealed class VisorEvidenciaDispositivoTests : IDisposable
{
	private readonly string _raiz =
		Path.Combine(Path.GetTempPath(), $"visor-{Guid.NewGuid():N}");

	private string Cache => Path.Combine(_raiz, "cache");

	// Se busca en vez de nombrarla: su nombre es un detalle interno del visor.
	private string CarpetaDeCopias =>
		Directory.Exists(Cache)
			? Directory.GetDirectories(Cache).SingleOrDefault() ?? Path.Combine(Cache, "sin-crear")
			: Path.Combine(Cache, "sin-crear");

	public void Dispose()
	{
		try
		{
			if (Directory.Exists(_raiz))
			{
				Directory.Delete(_raiz, recursive: true);
			}
		}
		catch (IOException)
		{
			// Limpiar el temporal no es parte de lo que se prueba.
		}
	}

	private string GuardarComoEnElDispositivo(string extension = ".jpg")
	{
		var privado = Path.Combine(_raiz, "datos", "evidencias");
		Directory.CreateDirectory(privado);

		var ruta = Path.Combine(privado, $"{Guid.NewGuid():N}{extension}");
		File.WriteAllBytes(ruta, [1, 2, 3, 4]);

		return ruta;
	}

	[Fact]
	public async Task SiElArchivoYaNoEsta_seDiceEsoYNoSeCopiaNada()
	{
		// Pasa cuando alguien limpia el almacenamiento de la app desde la configuración.
		var visor = new VisorEvidenciaDispositivo(Cache);

		var resultado = await visor.AbrirAsync(
			Path.Combine(_raiz, "no-existe.jpg"), "foto.jpg", "image/jpeg");

		Assert.Equal(ResultadoApertura.ArchivoNoEncontrado, resultado);
		Assert.False(Directory.Exists(CarpetaDeCopias));
	}

	[Fact]
	public async Task SinRuta_seDiceQueNoEstaYNoRevienta()
	{
		var visor = new VisorEvidenciaDispositivo(Cache);

		var resultado = await visor.AbrirAsync(string.Empty, "foto.jpg", "image/jpeg");

		Assert.Equal(ResultadoApertura.ArchivoNoEncontrado, resultado);
	}

	[Fact]
	public async Task LaCopiaLlevaElNombreQueElOperadorReconoce()
	{
		var enElDispositivo = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "Choque km 42.jpg", "image/jpeg");

		var copias = Directory.GetFiles(CarpetaDeCopias);
		Assert.Equal("Choque km 42.jpg", Path.GetFileName(Assert.Single(copias)));
	}

	[Fact]
	public async Task ElOriginalNoSeMueve()
	{
		// Si se moviera, la cola perdería lo que iba a subir.
		var enElDispositivo = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "foto.jpg", "image/jpeg");

		Assert.True(File.Exists(enElDispositivo));
	}

	[Fact]
	public async Task UnNombreConCaracteresProhibidos_seLimpiaEnVezDeFallar()
	{
		// El nombre puede traer cualquier cosa, y fallar al copiar dejaría sin ver una evidencia que sí está.
		var enElDispositivo = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "a/b:c*d?.jpg", "image/jpeg");

		var copia = Path.GetFileName(Assert.Single(Directory.GetFiles(CarpetaDeCopias)));
		Assert.Equal("abcd.jpg", copia);
	}

	[Fact]
	public async Task SinExtensionEnElNombre_seTomaLaDelArchivoReal()
	{
		// El nombre declarado no es de fiar; la extensión del archivo guardado la puso la app.
		var enElDispositivo = GuardarComoEnElDispositivo(".mp4");
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "video del incidente", "video/mp4");

		var copia = Path.GetFileName(Assert.Single(Directory.GetFiles(CarpetaDeCopias)));
		Assert.Equal("video del incidente.mp4", copia);
	}

	[Fact]
	public async Task UnNombreQueNoDejaNadaUtilizable_caeEnElDelDisco()
	{
		var enElDispositivo = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "///", "image/jpeg");

		var copia = Path.GetFileName(Assert.Single(Directory.GetFiles(CarpetaDeCopias)));
		Assert.Equal(Path.GetFileName(enElDispositivo), copia);
	}

	[Fact]
	public async Task CadaAperturaLimpiaLaAnterior()
	{
		// La caché no crece con copias: la de la vez pasada ya no hace falta.
		var primera = GuardarComoEnElDispositivo();
		var segunda = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(primera, "primera.jpg", "image/jpeg");
		await visor.AbrirAsync(segunda, "segunda.jpg", "image/jpeg");

		var copia = Path.GetFileName(Assert.Single(Directory.GetFiles(CarpetaDeCopias)));
		Assert.Equal("segunda.jpg", copia);
	}
}
