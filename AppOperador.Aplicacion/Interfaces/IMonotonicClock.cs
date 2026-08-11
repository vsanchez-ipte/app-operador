namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Contador de tiempo que no se puede mover desde los ajustes del dispositivo.
/// </summary>
/// <remarks>
/// <para>
/// Cuenta desde el último arranque del equipo y avanza siempre hacia adelante, aunque el
/// operador cambie la hora. Existe para medir la vigencia offline sin quedar a merced del
/// reloj (JTT-1383 CA 5).
/// </para>
/// <para>
/// <b>No sirve para saber qué hora es</b>, solo cuánto ha pasado, y únicamente dentro de un
/// mismo encendido: al apagar el equipo vuelve a cero. Esa limitación es conocida y la
/// cubre <c>TranscursoOffline</c>, que combina esta señal con la del reloj.
/// </para>
/// <para>
/// El nombre va en inglés como el resto de contratos de esta carpeta.
/// </para>
/// </remarks>
public interface IMonotonicClock
{
	/// <summary>Tiempo transcurrido desde el arranque del dispositivo.</summary>
	TimeSpan Transcurrido { get; }
}
