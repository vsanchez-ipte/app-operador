using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Dispositivo;

public sealed class RelojMonotonicoDispositivo : IMonotonicClock
{
	// ElapsedRealtime cuenta también la suspensión; UptimeMillis se detiene con el teléfono dormido.
	public TimeSpan Transcurrido =>
#if ANDROID
		TimeSpan.FromMilliseconds(Android.OS.SystemClock.ElapsedRealtime());
#else
		TimeSpan.FromMilliseconds(Environment.TickCount64);
#endif
}
