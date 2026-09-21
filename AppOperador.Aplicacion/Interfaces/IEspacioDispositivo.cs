using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Cuánto espacio queda en el almacenamiento donde viven la base local y las evidencias.
/// </summary>
/// <remarks>
/// Lo usan la captura, para no grabar un video que no va a caber (JTT-289 CA 8), y el perfil,
/// para mostrar el porcentaje libre (JTT-292 CA 4). Es una medición, no una decisión: qué hacer
/// con el número lo dice <c>ReglaEspacioParaEvidencia</c>.
/// </remarks>
public interface IEspacioDispositivo
{
	/// <returns>La medición, con los bytes en <see langword="null"/> si el sistema no la dio.</returns>
	EspacioDispositivo Medir();
}
