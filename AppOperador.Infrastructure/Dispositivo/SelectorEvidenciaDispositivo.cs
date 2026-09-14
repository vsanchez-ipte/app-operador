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

		// Los permisos de captura se piden antes de invocar al selector, y para los DOS orígenes
		// que los usan: fotografiar y grabar. MediaPicker los pediría solo, pero al negarse lanza
		// una excepción que no dice si el sistema volverá a preguntar o si ya dejó de hacerlo, y
		// esa diferencia es justo la que el operador necesita.
		if (origen is OrigenEvidencia.Camara or OrigenEvidencia.Video)
		{
			var permiso = await PedirPermisosDeCapturaAsync();
			if (permiso is not null)
			{
				return permiso;
			}
		}

		try
		{
			// Los diálogos del sistema tienen que salir del hilo de interfaz, igual que los de
			// permisos de ubicación.
			var resultados = await MainThread.InvokeOnMainThreadAsync(() => origen switch
			{
				OrigenEvidencia.Camara => UnoAsync(MediaPicker.Default.CapturePhotoAsync()),
				OrigenEvidencia.Video => UnoAsync(MediaPicker.Default.CaptureVideoAsync()),
				OrigenEvidencia.Galeria => DeLaGaleriaAsync(),
				OrigenEvidencia.Archivo => DelSelectorDeArchivosAsync(),
				_ => Task.FromResult<IReadOnlyList<FileResult>>([]),
			});

			// Vacío aquí es el operador cancelando el diálogo del sistema. Ojo: en Android 11+
			// también lo era un intent que no se podía resolver, y eso ya no pasa porque el
			// manifiesto declara el bloque <queries> — ver AndroidManifest.xml.
			if (resultados.Count == 0)
			{
				return SeleccionEvidencia.Cancelada;
			}

			var descritos = new List<ArchivoElegido>(resultados.Count);
			foreach (var resultado in resultados)
			{
				var descrito = await DescribirAsync(resultado, origen, cancelacion);
				if (descrito is not null)
				{
					descritos.Add(descrito);
				}
			}

			// El sistema entregó archivos que no se pueden leer. No es del operador.
			return descritos.Count == 0
				? SeleccionEvidencia.NoDisponible
				: SeleccionEvidencia.Elegidos(descritos);
		}
		catch (PermissionException)
		{
			// Llegar aquí NO es cosa del operador. Los permisos que la captura exige ya se
			// pidieron arriba, uno por uno, y la galería y el selector de archivos no exigen
			// ninguno en ninguna versión de Android (selector de fotos del sistema y
			// GET_CONTENT). Lo que queda es un permiso que la librería exige y el paquete no
			// declara, y eso se arregla en el manifiesto, no tocando el botón otra vez.
			//
			// Antes se resolvía preguntando si la CÁMARA todavía se podía pedir, y con la cámara
			// concedida la respuesta era «no» y se contestaba «permiso bloqueado», con botón a la
			// configuración incluido. Así llegó JTT-1681: en Android 12, MediaPicker exigía
			// WRITE_EXTERNAL_STORAGE, no estaba declarado, y el operador que acababa de conceder
			// la cámara leía que no tenía permiso.
			return SeleccionEvidencia.NoDisponible;
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
	/// Pide los permisos que exige capturar con la cámara, o devuelve por qué no se puede seguir.
	/// </summary>
	/// <returns>
	/// <see langword="null"/> si se puede continuar, o el desenlace que hay que devolver.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>Se pide aquí y no antes.</b> El diálogo del sistema sale cuando el operador toca el
	/// botón, que es lo que piden el CA 3 de JTT-1398 y los criterios 1 a 3 de JTT-1387: pedir la
	/// cámara al iniciar sesión asusta y no se entiende.
	/// </para>
	/// <para>
	/// <b>Son dos permisos, no uno, y el segundo depende de la versión.</b> Además de la cámara,
	/// <c>MediaPicker</c> exige <c>StorageWrite</c> en Android 12 y anteriores —en 13+ ese
	/// permiso ya no existe y lo omite—. Se pide aquí con la misma condición que usa la
	/// librería, para que una negativa del operador se distinga de una definitiva igual que con
	/// la cámara, y no llegue como una excepción sin explicación (JTT-1681).
	/// </para>
	/// </remarks>
	private static async Task<SeleccionEvidencia?> PedirPermisosDeCapturaAsync()
	{
		try
		{
			var camara = await PedirAsync<Permissions.Camera>();
			if (camara is not null)
			{
				return camara;
			}

			// Misma condición que MediaPicker.android.cs: por debajo de la 13 exige el permiso
			// de escritura, y el manifiesto lo declara acotado a esas versiones (maxSdkVersion).
			if (OperatingSystem.IsAndroid() && !OperatingSystem.IsAndroidVersionAtLeast(33))
			{
				return await PedirAsync<Permissions.StorageWrite>();
			}

			return null;
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException or FeatureNotSupportedException)
		{
			// El destino no tiene cámara ni sistema de permisos: escritorio y pruebas.
			return SeleccionEvidencia.NoDisponible;
		}
	}

	/// <summary>Pide un permiso y, si no se concede, dice si fue negativa o bloqueo.</summary>
	/// <returns>
	/// <see langword="null"/> si se concedió, o el desenlace que hay que devolver.
	/// </returns>
	private static async Task<SeleccionEvidencia?> PedirAsync<TPermiso>()
		where TPermiso : Permissions.BasePermission, new()
	{
		var estado = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<TPermiso>);

		return estado == PermissionStatus.Granted
			? null
			: SegunSiTodaviaSePuedePedir<TPermiso>();
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
	/// contesta que no hay que razonar nada, y eso se confundiría con el permiso bloqueado. Y se
	/// consulta <b>por el permiso que se acaba de pedir</b>, no por la cámara a secas: preguntar
	/// por la cámara cuando lo que faltaba era el almacenamiento fue lo que convirtió un permiso
	/// sin declarar en un «bloqueado» falso (JTT-1681).
	/// </para>
	/// </remarks>
	private static SeleccionEvidencia SegunSiTodaviaSePuedePedir<TPermiso>()
		where TPermiso : Permissions.BasePermission, new() =>
		Permissions.ShouldShowRationale<TPermiso>()
			? SeleccionEvidencia.PermisoNegado
			: SeleccionEvidencia.PermisoBloqueado;

	/// <summary>Envuelve una captura única en la forma de lista que usa la selección.</summary>
	private static async Task<IReadOnlyList<FileResult>> UnoAsync(Task<FileResult?> captura)
	{
		var resultado = await captura.ConfigureAwait(false);
		return resultado is null ? [] : [resultado];
	}

	/// <summary>
	/// Abre la galería y devuelve todo lo que el operador marcó (JTT-289 CA 1).
	/// </summary>
	/// <remarks>
	/// El PO fijó ocho archivos por incidencia; elegirlos de uno en uno serían ocho recorridos
	/// por la galería. No se limita aquí cuántos: el cupo lo aplica quien adjunta, contra el
	/// catálogo, y avisa en el primero que no quepa.
	/// </remarks>
	private static async Task<IReadOnlyList<FileResult>> DeLaGaleriaAsync()
	{
		var elegidas = await MediaPicker.Default.PickPhotosAsync().ConfigureAwait(false);
		return elegidas?.Where(e => e is not null).Select(e => e!).ToList() ?? [];
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
	private static async Task<IReadOnlyList<FileResult>> DelSelectorDeArchivosAsync()
	{
		var elegidos = await FilePicker.Default.PickMultipleAsync().ConfigureAwait(false);
		return elegidos?.Where(e => e is not null).Select(e => e!).ToList() ?? [];
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
