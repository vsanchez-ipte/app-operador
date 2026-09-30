using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

// Mide si se alcanza a Jacob, no si hay red: en campo hay cobertura sin servidor y servidor sin cobertura.
public interface IConnectivityService
{
    // Lectura en caché: no genera tráfico.
    bool HayEnlace { get; }

    event EventHandler<bool>? EnlaceCambio;

    Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default);

    // El acceso habla con Jacob antes de que haya sesión que sondear; un rechazo también prueba el enlace.
    void AnotarIntercambio(bool jacobRespondio);
}
