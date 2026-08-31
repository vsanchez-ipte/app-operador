using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Cámara, galería y selector de archivos del dispositivo, con las API de MAUI (JTT-1398 CA 2, 3 y 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>El permiso lo pide <c>MediaPicker</c> al invocarlo</b>, y eso es exactamente lo que piden
/// el CA 3 y los criterios 1 a 3 de JTT-1387: el diálogo del sistema sale cuando el operador
/// toca «tomar foto», no al iniciar sesión. Por eso aquí no hay una comprobación previa como la
/// que sí tiene la ubicación —donde el permiso es obligatorio para acceder—: aquí negarse es una
/// respuesta válida y termina en «no se adjunta», no en un aviso de error.
/// </para>
/// <para>
/// <b>Cancelar y negar el permiso devuelven lo mismo.</b> Al operador que cambió de idea y al que
/// dijo que no le pasa lo mismo: no hay archivo. Distinguirlos obligaría a un aviso para el
/// segundo que el CA 4 no pide y que interrumpiría una captura que puede seguir sin evidencia.
/// </para>
/// <para>
/// <b>Dónde corre.</b> Se registra en Android, iOS y Mac Catalyst. Compila en <c>net10.0</c>
/// porque los proyectos de prueba lo arrastran, pero ahí las API de MAUI lanzan y se responde
/// <c>NoDisponible</c>.
/// </para>
/// </remarks>
public sealed class SelectorEvidenciaDispositivo : ISelectorEvidencia
{
	/// <summary>Tipo que se asume cuando el sistema no declara ninguno.</summary>
	/// <remarks>
	/// Solo afecta a la validación local: <b>el tipo definitivo lo determina el servidor por
	/// contenido</b>, así que equivocarse aquí no deja pasar nada que allá no se acepte.
	/// </remarks>
	private const string TipoPorOmision = "application/octet-stream";

	/// <inheritdoc />
	public bool Disponible(OrigenEvidencia origen)
	{
		try
		{
			return origen switch
			{
				// Grabar exige cámara igual que fotografiar.
				OrigenEvidencia.Camara or OrigenEvidencia.Video =>
					MediaPicker.Default.IsCaptureSupported,
				OrigenEvidencia.Galeria or OrigenEvidencia.Archivo => true,
				_ => false,
			};
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException)
		{
			// En escritorio y en las pruebas, MediaPicker no existe.
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<SeleccionEvidencia> ElegirAsync(
		OrigenEvidencia origen,
		CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		try
		{
			// Los diálogos del sistema tienen que salir del hilo de interfaz, igual que los de
			// permisos de ubicación.
			var resultado = await MainThread.InvokeOnMainThreadAsync(() => origen switch
			{
				OrigenEvidencia.Camara => MediaPicker.Default.CapturePhotoAsync(),
				OrigenEvidencia.Video => MediaPicker.Default.CaptureVideoAsync(),
				OrigenEvidencia.Galeria => PrimeraDeLaGaleriaAsync(),
				OrigenEvidencia.Archivo => DelSelectorDeArchivosAsync(),
				_ => Task.FromResult<FileResult?>(null),
			});

			// Un null aquí es el operador cancelando el diálogo del sistema. Ojo: en Android 11+
			// también lo era un intent que no se podía resolver, y eso ya no pasa porque el
			// manifiesto declara el bloque <queries> — ver AndroidManifest.xml.
			if (resultado is null)
			{
				return SeleccionEvidencia.Cancelada;
			}

			var descrito = await DescribirAsync(resultado, origen, cancelacion);

			// El sistema entregó un archivo que no se puede leer. No es del operador.
			return descrito is null
				? SeleccionEvidencia.NoDisponible
				: SeleccionEvidencia.Elegido(descrito);
		}
		catch (PermissionException)
		{
			// El operador dijo que no. El CA 4 lo contempla: no se adjunta y la incidencia se
			// puede guardar igual.
			return SeleccionEvidencia.Cancelada;
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException or FeatureNotSupportedException)
		{
			// El dispositivo no ofrece la función. Antes se trataba como cancelar y el operador
			// se quedaba pulsando un botón mudo.
			return SeleccionEvidencia.NoDisponible;
		}
	}

	/// <summary>
	/// Abre la galería y se queda con la primera selección.
	/// </summary>
	/// <remarks>
	/// <b>Se usa la API de selección múltiple aunque aquí se tome una sola.</b> La de archivo
	/// único está obsoleta, y esta es además por donde entrará <b>JTT-289</b>: el PO fijó ocho
	/// archivos, y elegirlos de uno en uno son ocho recorridos por la galería. Cuando toque,
	/// será devolver la lista entera en vez del primero.
	/// </remarks>
	private static async Task<FileResult?> PrimeraDeLaGaleriaAsync()
	{
		var elegidas = await MediaPicker.Default.PickPhotosAsync().ConfigureAwait(false);
		return elegidas?.FirstOrDefault();
	}

	/// <summary>
	/// Abre el selector de archivos del sistema, no el carrete.
	/// </summary>
	/// <remarks>
	/// <b>No se filtra por tipo aquí.</b> Los formatos admitidos los publica el catálogo y cambian
	/// sin tocar la app; traducirlos a los tipos de cada plataforma —UTI en iOS, MIME en Android—
	/// sería codificar del lado del cliente justo lo que se decidió leer del servidor, y quedaría
	/// desalineado en cuanto el API publicara uno nuevo. Se deja elegir cualquier archivo y
	/// <c>ReglaEvidenciaAdmisible</c> lo rechaza con el motivo exacto, que además es el mismo
	/// camino que sigue una fotografía de un formato no admitido.
	/// </remarks>
	private static async Task<FileResult?> DelSelectorDeArchivosAsync()
	{
		var elegido = await FilePicker.Default.PickAsync().ConfigureAwait(false);
		return elegido;
	}

	/// <summary>Lee del archivo lo que hace falta para validarlo, sin cargarlo en memoria.</summary>
	private static async Task<ArchivoElegido?> DescribirAsync(
		FileResult archivo,
		OrigenEvidencia origen,
		CancellationToken cancelacion)
	{
		long bytes;

		try
		{
			// Se mide abriendo el flujo y no con FileInfo: en Android la ruta puede ser un
			// content:// que el sistema de archivos no sabe resolver.
			await using var flujo = await archivo.OpenReadAsync().ConfigureAwait(false);
			bytes = flujo.CanSeek ? flujo.Length : 0;
		}
		catch (Exception excepcion) when (
			excepcion is IOException or UnauthorizedAccessException)
		{
			return null;
		}

		cancelacion.ThrowIfCancellationRequested();

		return new ArchivoElegido(
			archivo.FileName,
			string.IsNullOrWhiteSpace(archivo.ContentType) ? TipoPorOmision : archivo.ContentType,
			bytes,
			_ => archivo.OpenReadAsync(),
			origen);
	}
}
