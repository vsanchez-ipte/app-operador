using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Contador monotónico del sistema operativo (JTT-1383).
/// </summary>
/// <remarks>
/// <para>
/// En Android se usa <c>SystemClock.ElapsedRealtime</c>, que cuenta desde el arranque
/// <b>incluyendo el tiempo en suspensión</b>. Es la diferencia importante frente a
/// <c>UptimeMillis</c>, que se detiene cuando el equipo duerme: un teléfono guardado ocho
/// horas en la mochila devolvería un transcurso casi nulo y la ventana offline no
/// avanzaría.
/// </para>
/// <para>
/// En el resto de destinos se usa <see cref="Environment.TickCount64"/>, que da la misma
/// garantía —monotónico y ajeno a la hora del sistema— con la misma limitación de
/// reiniciarse con el equipo.
/// </para>
/// </remarks>
public sealed class RelojMonotonicoDispositivo : IMonotonicClock
{
	public TimeSpan Transcurrido =>
#if ANDROID
		TimeSpan.FromMilliseconds(Android.OS.SystemClock.ElapsedRealtime());
#else
		TimeSpan.FromMilliseconds(Environment.TickCount64);
#endif
}
