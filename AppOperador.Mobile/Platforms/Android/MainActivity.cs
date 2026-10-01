using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AppOperador.Infrastructure.Dispositivo;

namespace AppOperador.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	// Se llama primero a la base: MAUI atiende aquí sus propias peticiones y saltárselas las dejaría colgadas.
	protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
	{
		base.OnActivityResult(requestCode, resultCode, data);

		GrabacionAcotadaAndroid.EntregarResultado(requestCode, resultCode, data);
	}
}
