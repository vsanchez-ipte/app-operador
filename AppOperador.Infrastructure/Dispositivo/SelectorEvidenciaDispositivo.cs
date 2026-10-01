using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

// Compila en net10.0 por las pruebas; ahí MediaPicker lanza y se responde NoDisponible.
public sealed class SelectorEvidenciaDispositivo : ISelectorEvidencia
{
	// Solo afecta la validación local: el servidor determina el tipo por contenido.
	private const string TipoPorOmision = "application/octet-stream";

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

	public async Task<SeleccionEvidencia> ElegirAsync(
		OrigenEvidencia origen,
		long topeBytes = 0,
		CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		// Antes que MediaPicker: al negarse, él lanza sin decir si el sistema volverá a preguntar.
		if (origen is OrigenEvidencia.Camara or OrigenEvidencia.Video)
		{
			var permiso = await PedirPermisosDeCapturaAsync();
			if (permiso is not null)
			{
				return permiso;
			}
		}

		// Grabar crea el archivo: si se pasa del tope se pierde entero, así que se le pide a la cámara que corte.
#if ANDROID
		if (origen is OrigenEvidencia.Video && topeBytes > 0)
		{
			// Null es «esta vía no se pudo usar»: se sigue sin tope, que es mejor que no grabar.
			var acotado = await GrabacionAcotadaAndroid.GrabarAsync(topeBytes, cancelacion);
			if (acotado is not null)
			{
				return acotado;
			}
		}
#endif

		try
		{
			// Los diálogos del sistema salen del hilo de interfaz.
			var resultados = await MainThread.InvokeOnMainThreadAsync(() => origen switch
			{
				OrigenEvidencia.Camara => UnoAsync(MediaPicker.Default.CapturePhotoAsync()),
				OrigenEvidencia.Video => UnoAsync(MediaPicker.Default.CaptureVideoAsync()),
				OrigenEvidencia.Galeria => DeLaGaleriaAsync(),
				OrigenEvidencia.Archivo => DelSelectorDeArchivosAsync(),
				_ => Task.FromResult<IReadOnlyList<FileResult>>([]),
			});

			// Vacío es cancelar; un intent sin resolver ya no llega aquí por el bloque <queries> del manifiesto.
			if (resultados.Count == 0)
			{
				return SeleccionEvidencia.Cancelada;
			}

			var descritos = new List<ArchivoElegido>(resultados.Count);
			var ilegibles = 0;
			foreach (var resultado in resultados)
			{
				var descrito = await DescribirAsync(resultado, origen, cancelacion);
				if (descrito is null)
				{
					ilegibles++;
					continue;
				}

				descritos.Add(descrito);
			}

			// Si fueron algunos de varios, se dice cuántos quedaron fuera.
			return descritos.Count == 0
				? SeleccionEvidencia.NoDisponible
				: SeleccionEvidencia.Elegidos(descritos, ilegibles);
		}
		catch (PermissionException)
		{
			// Los permisos de captura ya se pidieron: lo que falta es un permiso sin declarar en el manifiesto.
			return SeleccionEvidencia.NoDisponible;
		}
		catch (Exception excepcion) when (
			excepcion is NotSupportedException or NotImplementedException or FeatureNotSupportedException)
		{
			// El dispositivo no ofrece la función.
			return SeleccionEvidencia.NoDisponible;
		}
	}

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
			// En escritorio y pruebas no hay configuración que abrir, y no es motivo para tumbar la captura.
		}

		return Task.CompletedTask;
	}

	// Cámara y, hasta Android 12, StorageWrite: pedirlos aquí distingue una negativa de un bloqueo.
	private static async Task<SeleccionEvidencia?> PedirPermisosDeCapturaAsync()
	{
		try
		{
			var camara = await PedirAsync<Permissions.Camera>();
			if (camara is not null)
			{
				return camara;
			}

			// Misma condición que MediaPicker; el manifiesto lo declara con maxSdkVersion.
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

	private static async Task<SeleccionEvidencia?> PedirAsync<TPermiso>()
		where TPermiso : Permissions.BasePermission, new()
	{
		var estado = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<TPermiso>);

		return estado == PermissionStatus.Granted
			? null
			: SegunSiTodaviaSePuedePedir<TPermiso>();
	}

	// Después de pedir y por el permiso pedido: antes de la primera petición también contesta que no.
	private static SeleccionEvidencia SegunSiTodaviaSePuedePedir<TPermiso>()
		where TPermiso : Permissions.BasePermission, new() =>
		Permissions.ShouldShowRationale<TPermiso>()
			? SeleccionEvidencia.PermisoNegado
			: SeleccionEvidencia.PermisoBloqueado;

	private static async Task<IReadOnlyList<FileResult>> UnoAsync(Task<FileResult?> captura)
	{
		var resultado = await captura.ConfigureAwait(false);
		return resultado is null ? [] : [resultado];
	}

	// Sin límite aquí: el cupo lo aplica quien adjunta.
	private static async Task<IReadOnlyList<FileResult>> DeLaGaleriaAsync()
	{
		var elegidas = await MediaPicker.Default.PickPhotosAsync().ConfigureAwait(false);
		return elegidas?.Where(e => e is not null).Select(e => e!).ToList() ?? [];
	}

	// Sin filtro por tipo: los formatos los publica el catálogo y ReglaEvidenciaAdmisible da el motivo.
	private static async Task<IReadOnlyList<FileResult>> DelSelectorDeArchivosAsync()
	{
		var elegidos = await FilePicker.Default.PickMultipleAsync().ConfigureAwait(false);
		return elegidos?.Where(e => e is not null).Select(e => e!).ToList() ?? [];
	}

	private static async Task<ArchivoElegido?> DescribirAsync(
		FileResult archivo,
		OrigenEvidencia origen,
		CancellationToken cancelacion)
	{
		long bytes;

		try
		{
			// Abriendo el flujo y no con FileInfo: en Android la ruta puede ser un content://.
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
