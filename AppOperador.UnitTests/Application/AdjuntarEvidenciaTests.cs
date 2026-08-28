using System.Text;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.UnitTests.Application;

/// <summary>
/// Adjuntar y quitar evidencia (JTT-1398 CA 1, 5 y 9).
/// </summary>
/// <remarks>
/// Lo que se prueba aquí es <b>el orden y sus consecuencias</b>: que no se copie lo que va a
/// rechazarse, y que no quede registrado lo que no se pudo copiar. Que la fila acabe escrita en
/// la base se prueba contra SQLite, en <c>RepositorioEvidenciasSqliteTests</c>.
/// </remarks>
public sealed class AdjuntarEvidenciaTests
{
	private const string Incidencia = "11111111-1111-1111-1111-111111111111";

	private static readonly LimitesEvidencia Limites =
		new(["image/jpeg", "application/pdf"], 5, 3);

	private readonly EvidenciasFalsas _evidencias = new();
	private readonly AlmacenFalso _almacen = new();
	private readonly CatalogoFalso _catalogo = new() { Limites = Limites };

	private AdjuntarEvidencia Crear() =>
		new(_evidencias, _almacen, _catalogo, new RelojFijo(), new BitacoraNula());

	private static ArchivoElegido Archivo(
		string nombre = "IMG_0001.jpg",
		string mime = "image/jpeg",
		long bytes = 1024) =>
		new(nombre, mime, bytes,
			_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("contenido"))));

	[Fact]
	public async Task UnArchivoValido_seCopiaYSeRegistra()
	{
		var resultado = await Crear().EjecutarAsync(Incidencia, Archivo());

		Assert.True(resultado.Exito);
		Assert.Single(_almacen.Guardados);
		var adjunta = Assert.Single(_evidencias.Registradas);
		Assert.Equal(Incidencia, adjunta.IncidenciaUuid);
		Assert.Equal("IMG_0001.jpg", adjunta.NombreOriginal);
		Assert.Equal(EstadoSincronizacion.Pendiente, adjunta.Estado);
	}

	[Fact]
	public async Task LaEvidenciaNaceComoPendiente_yNoComoSincronizada()
	{
		// Su camino de envío es propio y empieza en cero: el CA 9 de JTT-280 se cumple cuando
		// el CCO la tiene, no cuando el operador la eligió.
		var resultado = await Crear().EjecutarAsync(Incidencia, Archivo());

		Assert.Equal(EstadoSincronizacion.Pendiente, resultado.Adjuntada!.Estado);
	}

	[Fact]
	public async Task UnFormatoQueNoSeAdmite_niSiquieraSeCopia()
	{
		// Copiar quince megabytes de video para después rechazarlos es gasto puro en un
		// teléfono de campo. La validación va antes que el disco, y esto lo fija.
		var resultado = await Crear().EjecutarAsync(Incidencia, Archivo(mime: "video/mp4"));

		Assert.False(resultado.Exito);
		Assert.Equal(MotivoEvidenciaRechazada.FormatoNoAdmitido, resultado.Motivo);
		Assert.Empty(_almacen.Guardados);
		Assert.Empty(_evidencias.Registradas);
	}

	[Fact]
	public async Task ConElCupoLleno_noSeCopiaNiSeRegistra()
	{
		_evidencias.YaAdjuntas = 3;

		var resultado = await Crear().EjecutarAsync(Incidencia, Archivo());

		Assert.Equal(MotivoEvidenciaRechazada.CupoLleno, resultado.Motivo);
		Assert.Empty(_almacen.Guardados);
	}

	[Fact]
	public async Task SinCatalogoDescargado_noSeAdjunta()
	{
		_catalogo.Limites = LimitesEvidencia.Desconocidos;

		var resultado = await Crear().EjecutarAsync(Incidencia, Archivo());

		Assert.Equal(MotivoEvidenciaRechazada.LimitesDesconocidos, resultado.Motivo);
	}

	[Fact]
	public async Task SiLaCopiaFalla_noQuedaFilaApuntandoANada()
	{
		// Es la parte que rompe la cola si se hace al revés: una fila registrada cuyo archivo
		// no llegó se intentaría subir para siempre sin que nada dijera que está rota.
		_almacen.Falla = true;

		var resultado = await Crear().EjecutarAsync(Incidencia, Archivo());

		Assert.False(resultado.Exito);
		Assert.Empty(_evidencias.Registradas);
	}

	[Fact]
	public async Task CadaEvidenciaNaceConSuPropioIdentificador()
	{
		var primera = await Crear().EjecutarAsync(Incidencia, Archivo());
		var segunda = await Crear().EjecutarAsync(Incidencia, Archivo());

		Assert.NotEqual(primera.Adjuntada!.Uuid, segunda.Adjuntada!.Uuid);
	}

	// ── Quitar (CA 1: «y poder quitarlo») ─────────────────────────────────────────────

	[Fact]
	public async Task Quitar_borraElArchivoYLaFila()
	{
		var adjunta = (await Crear().EjecutarAsync(Incidencia, Archivo())).Adjuntada!;

		var quitada = await new QuitarEvidencia(_evidencias, _almacen, new BitacoraNula())
			.EjecutarAsync(adjunta.Uuid);

		Assert.True(quitada);
		Assert.Empty(_evidencias.Registradas);
		Assert.Contains(adjunta.RutaArchivo, _almacen.Borrados);
	}

	[Fact]
	public async Task Quitar_noSeLlevaUnaEvidenciaQueElCcoYaConfirmo()
	{
		// El archivo existe del otro lado; borrarlo aquí solo rompe la trazabilidad local.
		var adjunta = (await Crear().EjecutarAsync(Incidencia, Archivo())).Adjuntada!;
		_evidencias.MarcarSincronizada(adjunta.Uuid);

		var quitada = await new QuitarEvidencia(_evidencias, _almacen, new BitacoraNula())
			.EjecutarAsync(adjunta.Uuid);

		Assert.False(quitada);
		Assert.Single(_evidencias.Registradas);
		Assert.Empty(_almacen.Borrados);
	}

	[Fact]
	public async Task Quitar_loQueNoExisteNoRevienta()
	{
		var quitada = await new QuitarEvidencia(_evidencias, _almacen, new BitacoraNula())
			.EjecutarAsync("no-existe");

		Assert.False(quitada);
	}

	// ── Dobles ────────────────────────────────────────────────────────────────────────

	private sealed class EvidenciasFalsas : IRepositorioEvidencias
	{
		public List<EvidenciaAdjunta> Registradas { get; } = [];

		/// <summary>Cuántas dice tener ya la incidencia, para ejercitar el cupo.</summary>
		public int YaAdjuntas { get; set; }

		public void MarcarSincronizada(string uuid)
		{
			var indice = Registradas.FindIndex(e => e.Uuid == uuid);
			Registradas[indice] = Registradas[indice] with
			{
				Estado = EstadoSincronizacion.Sincronizado,
			};
		}

		public Task AgregarAsync(EvidenciaAdjunta evidencia, CancellationToken c = default)
		{
			Registradas.Add(evidencia);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerDeIncidenciaAsync(
			string incidenciaUuid, CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EvidenciaAdjunta>>(
				[.. Registradas.Where(e => e.IncidenciaUuid == incidenciaUuid)]);

		public Task<int> ContarDeIncidenciaAsync(string incidenciaUuid, CancellationToken c = default) =>
			Task.FromResult(YaAdjuntas);

		public Task<EvidenciaAdjunta?> ObtenerAsync(string uuid, CancellationToken c = default) =>
			Task.FromResult(Registradas.FirstOrDefault(e => e.Uuid == uuid));

		public Task EliminarAsync(string uuid, CancellationToken c = default)
		{
			Registradas.RemoveAll(e => e.Uuid == uuid);
			return Task.CompletedTask;
		}

		public Task<IReadOnlyList<EvidenciaAdjunta>> ObtenerPendientesDeIncidenciaAsync(
			string incidenciaUuid, CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EvidenciaAdjunta>>(
				[.. Registradas.Where(e => e.IncidenciaUuid == incidenciaUuid
					&& e.Estado != EstadoSincronizacion.Sincronizado)]);

		public Task ActualizarEnvioAsync(
			string uuid, EstadoSincronizacion estado, string? codigoError, CancellationToken c = default)
		{
			var indice = Registradas.FindIndex(e => e.Uuid == uuid);

			if (indice >= 0)
			{
				Registradas[indice] = Registradas[indice] with { Estado = estado };
			}

			return Task.CompletedTask;
		}
	}

	private sealed class AlmacenFalso : IAlmacenEvidencias
	{
		public List<string> Guardados { get; } = [];

		public List<string> Borrados { get; } = [];

		/// <summary>Simula que el archivo no se pudo copiar.</summary>
		public bool Falla { get; set; }

		public Task<string?> GuardarAsync(
			string uuidEvidencia, ArchivoElegido archivo, CancellationToken c = default)
		{
			if (Falla)
			{
				return Task.FromResult<string?>(null);
			}

			var ruta = $"/privado/evidencias/{uuidEvidencia}.jpg";
			Guardados.Add(ruta);
			return Task.FromResult<string?>(ruta);
		}

		public Task EliminarAsync(string rutaArchivo, CancellationToken c = default)
		{
			Borrados.Add(rutaArchivo);
			return Task.CompletedTask;
		}
	}

	private sealed class RelojFijo : IClock
	{
		public DateTime UtcAhora { get; } = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);
	}

	private sealed class BitacoraNula : IAuditLog
	{
		public Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken c = default) =>
			Task.CompletedTask;

		public Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken c = default) =>
			Task.FromResult<IReadOnlyList<EventoAuditoria>>([]);
	}

	private sealed class CatalogoFalso : ICatalogoRepository
	{
		public LimitesEvidencia Limites { get; set; } = LimitesEvidencia.Desconocidos;

		public Task<CatalogosOperacion> ObtenerAsync(CancellationToken c = default) =>
			Task.FromResult(new CatalogosOperacion(
				new DateOnly(2026, 8, 27),
				[new TipoIncidencia(11, "Objeto en camino")],
				[new SeveridadIncidencia(Guid.NewGuid(), "Crítico", 1, "#EB1409")],
				[],
				[],
				Limites));

		public Task ReemplazarAsync(CatalogosOperacion catalogos, CancellationToken c = default) =>
			Task.CompletedTask;
	}
}
