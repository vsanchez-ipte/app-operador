using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Estado de comunicación de la app con Jacob CCO.
/// </summary>
/// <remarks>
/// <para>
/// <b>No informa si hay red genérica, sino si se puede hablar con Jacob</b> (JTT-1391 CA 2).
/// En campo se dan las dos situaciones por separado: cobertura sin servidor alcanzable, y
/// servidor levantado al que no se llega. Un indicador basado solo en el WiFi mentiría en
/// las dos.
/// </para>
/// <para>
/// La maqueta muestra ese estado como "ENLACE" u "OFFLINE" y de él dependen la cola y la
/// sincronización. El nombre está fijado en inglés por el documento de arquitectura.
/// </para>
/// </remarks>
public interface IConnectivityService
{
    /// <summary>
    /// Último estado conocido del enlace con Jacob CCO.
    /// </summary>
    /// <remarks>
    /// Es una lectura cacheada, no una comprobación: consultarla no genera tráfico. Para
    /// forzar una comprobación está <see cref="ComprobarAsync"/>.
    /// </remarks>
    bool HayEnlace { get; }

    /// <summary>Se dispara cuando el enlace se establece o se pierde.</summary>
    event EventHandler<bool>? EnlaceCambio;

    /// <summary>
    /// Comprueba ahora mismo si se alcanza a Jacob CCO y actualiza el estado.
    /// </summary>
    /// <returns>
    /// El estado resultante del enlace y, si no lo hay, por qué. Quien presenta necesita esa
    /// causa: decir "sin conexión" cuando el servidor sí contestó manda a buscar el problema
    /// donde no está.
    /// </returns>
    /// <remarks>
    /// La usan el reintento manual del operador y la recuperación de red. No lanza: un fallo
    /// de comunicación es un desenlace normal y viaja dentro del resultado.
    /// </remarks>
    Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default);
}
