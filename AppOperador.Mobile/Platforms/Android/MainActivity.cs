using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AppOperador.Infrastructure.Dispositivo;

namespace AppOperador.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	/// <summary>
	/// Recoge lo que devuelven las actividades que la app lanza por su cuenta.
	/// </summary>
	/// <remarks>
	/// Hoy solo es la grabación de video acotada al tope del catálogo, que no puede ir por
	/// <c>MediaPicker</c> porque aquello no admite un tamaño máximo. El registro de resultados
	/// que MAUI ofrecía para esto ya no existe en la versión que usa el proyecto, así que se
	/// sobrescribe la actividad, que es nuestra. <b>Se llama primero a la base</b>: MAUI atiende
	/// aquí sus propias peticiones —el selector de archivos, entre otras— y saltárselas las
	/// dejaría colgadas.
	/// </remarks>
	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);

		GrabacionAcotadaAndroid.EntregarResultado(requestCode, resultCode, data);
	}
}
