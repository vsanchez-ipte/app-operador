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

/// <summary>
/// Graba un video pidiéndole a la cámara que se detenga al llegar al tope del catálogo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué no basta con <c>MediaPicker</c>.</b> <c>MediaPickerOptions</c> no tiene ninguna
/// opción de tamaño ni de duración —solo título, calidad de compresión, dimensiones y cuántos
/// archivos—, así que la grabación abierta con él no tiene tope: el operador graba, y hasta que
/// suelta el botón no se entera de que se pasó. Para entonces el video ya no se puede recortar y
/// se pierde entero. Eso es lo que QA levantó como defecto.
/// </para>
/// <para>
/// <b>Qué se hace en su lugar.</b> Se lanza el mismo intent que lanza <c>MediaPicker</c>
/// —<c>MediaStore.ActionVideoCapture</c>, ya declarado en el bloque <c>&lt;queries&gt;</c> del
/// manifiesto— pero con <c>EXTRA_SIZE_LIMIT</c>. La cámara del sistema corta sola al llegar y
/// devuelve <b>lo que alcanzó a grabar</b>, que es justo lo contrario de perderlo todo.
/// </para>
/// <para>
/// <b>No todos los fabricantes lo respetan.</b> <c>EXTRA_SIZE_LIMIT</c> es una petición, no una
/// garantía: hay cámaras que lo ignoran. Por eso esto <b>no sustituye</b> a la validación de
/// <c>ReglaEvidenciaAdmisible</c>, que sigue corriendo después con el tamaño real, y por eso el
/// aviso que lee el operador dice el tope en vez de prometer que la grabación se detendrá.
/// </para>
/// <para>
/// <b>El video no pasa por la galería.</b> Se graba contra un archivo nuestro en la caché, igual
/// que hace <c>MediaPicker</c>, y no contra <c>MediaStore</c>: sin <c>EXTRA_OUTPUT</c> la cámara
/// lo guardaría en el carrete compartido, y la evidencia de una incidencia quedaría a la vista de
/// cualquiera que abra la galería del dispositivo.
/// </para>
/// </remarks>
public static class GrabacionAcotadaAndroid
{
	/// <summary>
	/// Código con el que se reconoce nuestra petición al volver.
	/// </summary>
	/// <remarks>
	/// Alto y propio para no cruzarse con los que usa MAUI por dentro. Si llega otro, se ignora
	/// y se deja pasar a quien sí lo espere.
	/// </remarks>
	private const int CodigoPeticion = 0x5643;

	private const string TipoVideo = "video/mp4";

	/// <summary>Lo que está esperando el resultado de la cámara, si hay algo esperándolo.</summary>
	private static TaskCompletionSource<bool>? _enCurso;

	/// <summary>Dónde se le pidió a la cámara que dejara el video.</summary>
	private static JavaFile? _destino;

	/// <summary>Un candado: dos grabaciones a la vez no tienen sentido y se estorbarían.</summary>
	private static readonly SemaphoreSlim Turno = new(1, 1);

	/// <summary>
	/// Graba con la cámara del sistema, pidiéndole que no pase de <paramref name="topeBytes"/>.
	/// </summary>
	/// <returns>
	/// <para>
	/// Lo grabado, o <see langword="null"/> <b>solo</b> si esta vía no se pudo usar: sin tope que
	/// pedir, sin actividad, sin proveedor de archivos o sin cámara que atienda el intent. Ese
	/// <see langword="null"/> significa «sigue por el camino de siempre», y quien llama vuelve a
	/// intentarlo con <c>MediaPicker</c>.
	/// </para>
	/// <para>
	/// <b>Cancelar no es null.</b> Si el operador cerró la cámara, se devuelve
	/// <see cref="SeleccionEvidencia.Cancelada"/> y ahí termina: confundir las dos cosas volvería
	/// a abrirle la cámara —y esta vez sin tope— a quien acaba de decir que no quería grabar.
	/// </para>
	/// </returns>
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

			// El proveedor que MAUI declara en el manifiesto combinado. Su configuración de rutas
			// incluye `cache-path`, que es donde se acaba de crear el archivo, así que la cámara
			// puede escribir ahí sin que el video salga del espacio privado de la app.
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
				// El proveedor de archivos no tiene declarada esta ruta. No es del operador y
				// tiene salida: el camino de siempre sigue funcionando.
				return null;
			}

			using var intent = new Intent(MediaStore.ActionVideoCapture);
			intent.PutExtra(MediaStore.ExtraSizeLimit, topeBytes);
			intent.PutExtra(MediaStore.ExtraOutput, destinoUri);
			intent.AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);

			// Con el bloque <queries> del manifiesto esto resuelve; sin él devolvería null y la
			// llamada reventaría con ActivityNotFoundException. Se comprueba igual: el manifiesto
			// puede cambiar y quedarse sin el intent, y eso no debe tumbar la captura.
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
				// Se lanza esperando a que el hilo de interfaz termine, y no en diferido: así una
				// excepción al abrir la cámara llega hasta el catch de abajo. En diferido se
				// quedaría en el hilo de interfaz —donde tumbaría la app— y esta espera no se
				// resolvería nunca, dejando el botón de grabar muerto sin decir nada.
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

			// La cámara dijo que sí y no dejó nada. No es del operador y no tiene por qué
			// repetirse solo: se le dice que no se pudo y se le ofrece la otra vía, que es lo
			// que ya hace el resto de la captura cuando el sistema falla.
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
			// Lo mismo: se contesta que no se pudo por este camino y quien llama usa el de
			// siempre. Tratarlo como fallo dejaría sin grabar a quien sí podía.
			return null;
		}
		finally
		{
			Turno.Release();
		}
	}

	/// <summary>
	/// Recoge el resultado de la cámara. Lo llama <c>MainActivity.OnActivityResult</c>.
	/// </summary>
	/// <remarks>
	/// Va por aquí y no por el registro de resultados de MAUI porque esa API ya no existe en la
	/// versión que usa el proyecto. La actividad es nuestra, así que sobrescribirla es el camino
	/// estable.
	/// </remarks>
	/// <returns>
	/// <see langword="true"/> si la petición era nuestra y se atendió.
	/// </returns>
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

		// Cancelar es una respuesta válida y se atiende en silencio, igual que en el resto de la
		// captura: se contesta que no hay archivo y no se avisa de nada.
		espera.TrySetResult(resultado == Result.Ok);

		if (resultado != Result.Ok && _destino is { } aMedias)
		{
			Borrar(aMedias);
		}

		return true;
	}

	/// <summary>Quita el archivo que la cámara dejó a medias, o vacío, al no grabar nada.</summary>
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
			// Un archivo de caché que no se pudo borrar lo recoge el sistema. No vale tumbar
			// nada por esto.
		}
	}
}
#endif
