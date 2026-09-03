using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Cámara, galería y selector de archivos del dispositivo, con las API de MAUI (JTT-1398 CA 2, 3 y 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>El permiso de cámara se pide al tocar el botón</b>, no al iniciar sesión, que es lo que
/// piden el CA 3 y los criterios 1 a 3 de JTT-1387. Se pide para los <b>dos</b> orígenes que la
/// usan —fotografiar y grabar— y de forma explícita, en vez de dejar que lo pida
/// <c>MediaPicker</c> por dentro: al negarse, aquello lanza una excepción que no dice si el
/// sistema volverá a preguntar, y esa es justamente la diferencia que hay que contarle al
/// operador.
/// </para>
/// <para>
/// <b>Negarse una vez no se avisa; quedarse sin poder pedirlo, sí.</b> La primera negativa
/// termina en «no se adjunta» y la incidencia se guarda igual, como pide el CA 4. Pero Android
/// deja de mostrar el diálogo tras la segunda, y a partir de ahí el botón sería mudo para
/// siempre: ese caso se distingue y se acompaña de la única salida que queda, la configuración
/// del sistema.
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

		// La cámara se pide antes de invocar al selector, y para los DOS orígenes que la usan:
		// fotografiar y grabar. MediaPicker la pediría solo, pero al negarse lanza una excepción
		// que no dice si el sistema volverá a preguntar o si ya dejó de hacerlo, y esa diferencia
		// es justo la que el operador necesita.
		if (origen is OrigenEvidencia.Camara or OrigenEvidencia.Video)
		{
			var permiso = await PedirCamaraAsync();
			if (permiso is not null)
			{
				return permiso;
			}
		}

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
			// Red de seguridad: la cámara ya se pidió arriba, pero la galería y el selector de
			// archivos pueden exigir permisos propios según la versión de Android. Se resuelve
			// igual, mirando si el sistema todavía va a preguntar.
			return await SegunSiTodaviaSePuedePedirAsync();
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException or FeatureNotSupportedException)
		{
			// El dispositivo no ofrece la función. Antes se trataba como cancelar y el operador
			// se quedaba pulsando un botón mudo.
			return SeleccionEvidencia.NoDisponible;
		}
	}

	/// <inheritdoc />
	public Task AbrirConfiguracionAsync()
	{
		try
		{
			// Es la misma pantalla que usa el permiso de ubicación cuando queda bloqueado.
			AppInfo.Current.ShowSettingsUI();
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException)
		{
			// En escritorio y en las pruebas no hay pantalla de configuración que abrir. No se
			// propaga: quien llama solo ofrecía una salida, y no poder ofrecerla no es un fallo
			// que deba tumbar la captura.
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Pide el permiso de cámara, o devuelve por qué no se puede seguir.
	/// </summary>
	/// <returns>
	/// <see langword="null"/> si se puede continuar, o el desenlace que hay que devolver.
	/// </returns>
	/// <remarks>
	/// <b>Se pide aquí y no antes.</b> El diálogo del sistema sale cuando el operador toca el
	/// botón, que es lo que piden el CA 3 de JTT-1398 y los criterios 1 a 3 de JTT-1387: pedir la
	/// cámara al iniciar sesión asusta y no se entiende.
	/// </remarks>
	private static async Task<SeleccionEvidencia?> PedirCamaraAsync()
	{
		try
		{
			var estado = await MainThread.InvokeOnMainThreadAsync(
				Permissions.RequestAsync<Permissions.Camera>);

			if (estado == PermissionStatus.Granted)
			{
				return null;
			}

			return await SegunSiTodaviaSePuedePedirAsync();
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException or FeatureNotSupportedException)
		{
			// El destino no tiene cámara ni sistema de permisos: escritorio y pruebas.
			return SeleccionEvidencia.NoDisponible;
		}
	}

	/// <summary>
	/// Distingue una negativa reversible de una definitiva.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Es toda la diferencia para el operador.</b> Mientras el sistema siga dispuesto a
	/// preguntar, volver a tocar el botón vuelve a mostrar el diálogo y no hay nada que avisar.
	/// Cuando deja de preguntar —en Android, tras la segunda negativa— el botón se vuelve mudo
	/// para siempre y la única salida está en la configuración del sistema.
	/// </para>
	/// <para>
	/// Se consulta <b>después</b> de pedir y no antes: antes de la primera petición también
	/// contesta que no hay que razonar nada, y eso se confundiría con el permiso bloqueado.
	/// </para>
	/// </remarks>
	private static Task<SeleccionEvidencia> SegunSiTodaviaSePuedePedirAsync() =>
		Task.FromResult(Permissions.ShouldShowRationale<Permissions.Camera>()
			? SeleccionEvidencia.PermisoNegado
			: SeleccionEvidencia.PermisoBloqueado);

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
