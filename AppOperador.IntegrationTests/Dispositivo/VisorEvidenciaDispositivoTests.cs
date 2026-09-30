using AppOperador.Aplicacion.Interfaces;
using AppOperador.Infrastructure.Dispositivo;

namespace AppOperador.IntegrationTests.Dispositivo;

/// <summary>
/// La copia que se le entrega al visor del sistema, contra el disco real.
/// </summary>
/// <remarks>
/// <para>
/// <b>Qué se prueba aquí y qué no.</b> Lanzar el visor es del dispositivo y en pruebas no existe:
/// esa parte responde <see cref="ResultadoApertura.NoSePudo"/> y se recorre a mano. Lo que sí se
/// puede fijar —y es donde estaban los dos riesgos reales— es <b>la copia</b>: que se haga en la
/// caché, que lleve el nombre que el operador reconoce en vez del UUID del disco, y que no se
/// intente nada cuando el archivo ya no está.
/// </para>
/// <para>
/// <b>Por qué importa el nombre.</b> En el espacio privado la evidencia se llama por su UUID,
/// porque dos <c>IMG_0001.jpg</c> son lo normal en un teléfono y una pisaría a la otra. Sin esta
/// copia, el operador abriría su foto y leería como título una cadena sin sentido.
/// </para>
/// </remarks>
public sealed class VisorEvidenciaDispositivoTests : IDisposable
{
	private readonly string _raiz =
		Path.Combine(Path.GetTempPath(), $"visor-{Guid.NewGuid():N}");

	private string Cache => Path.Combine(_raiz, "cache");

	/// <summary>
	/// La subcarpeta que el visor crea dentro de la caché.
	/// </summary>
	/// <remarks>
	/// Se busca en vez de nombrarla: su nombre es un detalle interno del visor, y fijarlo aquí
	/// ataría la prueba a algo que puede cambiar sin que nada se rompa de verdad.
	/// </remarks>
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

	/// <summary>Deja una evidencia como la guarda la app: nombrada por su UUID.</summary>
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
		// Pasa de verdad: alguien limpia el almacenamiento de la app desde la configuración del
		// sistema y las filas siguen apuntando a archivos que ya no existen.
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
		// El CA 9 de JTT-1398 ya decía que el archivo del operador no se toca. Ver una evidencia
		// tampoco puede sacarla de su sitio: si se moviera, la cola perdería lo que iba a subir.
		var enElDispositivo = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "foto.jpg", "image/jpeg");

		Assert.True(File.Exists(enElDispositivo));
	}

	[Fact]
	public async Task UnNombreConCaracteresProhibidos_seLimpiaEnVezDeFallar()
	{
		// El nombre viene de otra galería o de otro dispositivo: puede traer cualquier cosa, y
		// reventar al copiar dejaría al operador sin ver una evidencia que sí está.
		var enElDispositivo = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(enElDispositivo, "a/b:c*d?.jpg", "image/jpeg");

		var copia = Path.GetFileName(Assert.Single(Directory.GetFiles(CarpetaDeCopias)));
		Assert.Equal("abcd.jpg", copia);
	}

	[Fact]
	public async Task SinExtensionEnElNombre_seTomaLaDelArchivoReal()
	{
		// Es la que hace que el visor sepa con qué abrirlo. El nombre declarado no es de fiar;
		// el archivo guardado sí, porque la extensión se la puso la app al copiarlo.
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
		// La caché no se deja crecer con copias de quince megabytes: la de la vez pasada ya no
		// hace falta, y el original sigue donde estaba.
		var primera = GuardarComoEnElDispositivo();
		var segunda = GuardarComoEnElDispositivo();
		var visor = new VisorEvidenciaDispositivo(Cache);

		await visor.AbrirAsync(primera, "primera.jpg", "image/jpeg");
		await visor.AbrirAsync(segunda, "segunda.jpg", "image/jpeg");

		var copia = Path.GetFileName(Assert.Single(Directory.GetFiles(CarpetaDeCopias)));
		Assert.Equal("segunda.jpg", copia);
	}
}
