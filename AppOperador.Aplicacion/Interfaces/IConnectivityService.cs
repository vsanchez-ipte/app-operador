namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Estado de comunicación de la app con Jacob CCO.
/// </summary>
/// <remarks>
/// No informa si hay red genérica, sino si se puede hablar con Jacob: la maqueta muestra
/// ese estado como "ENLACE" u "OFFLINE" y de él dependen la cola y la sincronización.
/// El nombre está fijado en inglés por el documento de arquitectura.
/// </remarks>
public interface IConnectivityService
{
	/// <summary>Indica si en este momento hay enlace con Jacob CCO.</summary>
	bool HayEnlace { get; }

	/// <summary>Se dispara cuando el enlace se establece o se pierde.</summary>
	event EventHandler<bool>? EnlaceCambio;
}
