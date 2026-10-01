#if ANDROID
using Android.App;
using Android.Content;
using Android.Provider;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using AndroidUri = Android.Net.Uri;
using JavaFile = Java.IO.File;

namespace AppOperador.Infrastructure.Dispositivo;

// MediaPicker no admite tope; EXTRA_SIZE_LIMIT es una petición que algunas cámaras ignoran.
public static class GrabacionAcotadaAndroid
{
	// Alto y propio para no cruzarse con los códigos que usa MAUI.
	private const int CodigoPeticion = 0x5643;

	private const string TipoVideo = "video/mp4";

	private static TaskCompletionSource<bool>? _enCurso;

	private static JavaFile? _destino;

	// Dos grabaciones a la vez se estorbarían.
	private static readonly SemaphoreSlim Turno = new(1, 1);

	// Null solo si esta vía no se pudo usar; cancelar devuelve Cancelada para no reabrir la cámara sin tope.
	public static async Task<SeleccionEvidencia?> GrabarAsync(
		long topeBytes,
		CancellationToken cancelacion = default)
	{
		if (topeBytes <= 0 || Platform.CurrentActivity is not { } actividad)
		{
			return null;
		}

		await Turno.WaitAsync(cancelacion).ConfigureAwait(false);

		try
		{
			var destino = new JavaFile(
				FileSystem.CacheDirectory,
				$"evidencia-{Guid.NewGuid():N}.mp4");

			// Proveedor de MAUI con cache-path: el video no sale del espacio privado ni llega a la galería.
			var autoridad = $"{actividad.PackageName}.fileProvider";

			AndroidUri? destinoUri;
			try
			{
				destinoUri = AndroidX.Core.Content.FileProvider.GetUriForFile(
					actividad, autoridad, destino);
			}
			catch (Exception excepcion) when (
				excepcion is Java.Lang.IllegalArgumentException or NotSupportedException)
			{
				// El proveedor no declara esta ruta: se sigue por el camino de siempre.
				return null;
			}

			using var intent = new Intent(MediaStore.ActionVideoCapture);
			intent.PutExtra(MediaStore.ExtraSizeLimit, topeBytes);
			intent.PutExtra(MediaStore.ExtraOutput, destinoUri);
			intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);

			// Se comprueba aunque el manifiesto lo declare: si cambia, no debe tumbar la captura.
			if (intent.ResolveActivity(actividad.PackageManager!) is null)
			{
				return null;
			}

			var espera = new TaskCompletionSource<bool>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			_enCurso = espera;
			_destino = destino;

			try
			{
				// Sin diferir: una excepción al abrir la cámara debe llegar al catch y no tumbar la app.
				await MainThread.InvokeOnMainThreadAsync(
					() => actividad.StartActivityForResult(intent, CodigoPeticion));

				await using (cancelacion.Register(() => espera.TrySetResult(false)))
				{
					if (!await espera.Task.ConfigureAwait(false))
					{
						// Cerró la cámara. Se atiende en silencio, como el resto de la captura.
						return SeleccionEvidencia.Cancelada;
					}
				}
			}
			finally
			{
				_enCurso = null;
				_destino = null;
			}

			// La cámara dijo que sí y no dejó nada: se ofrece la otra vía.
			if (!destino.Exists() || destino.Length() <= 0)
			{
				Borrar(destino);
				return SeleccionEvidencia.NoDisponible;
			}

			var ruta = destino.AbsolutePath;
			var bytes = destino.Length();

			return SeleccionEvidencia.Elegido(new ArchivoElegido(
				destino.Name,
				TipoVideo,
				bytes,
				_ => Task.FromResult<Stream>(File.OpenRead(ruta)),
				OrigenEvidencia.Video));
		}
		catch (Exception excepcion) when (
			excepcion is ActivityNotFoundException or Java.Lang.SecurityException or IOException)
		{
			// Se sigue por el camino de siempre: tratarlo como fallo dejaría sin grabar a quien sí podía.
			return null;
		}
		finally
		{
			Turno.Release();
		}
	}

	// Lo llama MainActivity: el registro de resultados de MAUI ya no existe en esta versión.
	public static bool EntregarResultado(int codigoPeticion, Result resultado, Intent? datos)
	{
		_ = datos;

		if (codigoPeticion != CodigoPeticion)
		{
			return false;
		}

		var espera = _enCurso;
		if (espera is null)
		{
			return false;
		}

		// Cancelar es una respuesta válida y se atiende en silencio.
		espera.TrySetResult(resultado == Result.Ok);

		if (resultado != Result.Ok && _destino is { } aMedias)
		{
			Borrar(aMedias);
		}

		return true;
	}

	private static void Borrar(JavaFile destino)
	{
		try
		{
			if (destino.Exists())
			{
				destino.Delete();
			}
		}
		catch (Java.Lang.SecurityException)
		{
			// Un archivo de caché que no se pudo borrar lo recoge el sistema.
		}
	}
}
#endif
