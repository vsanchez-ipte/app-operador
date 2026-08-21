using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Descarga de los catálogos vigentes desde Jacob CCO (JTT-1394 CA 1 y 4).
/// </summary>
/// <remarks>
/// El nombre está en inglés por la misma convención que el resto de los contratos de
/// <c>Interfaces</c>, fijada en el documento de arquitectura.
/// </remarks>
public interface ICatalogosJacobClient
{
	/// <summary>
	/// Pide el catálogo completo, o <see langword="null"/> si no se pudo traer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Devuelve <see langword="null"/> en vez de lanzar</b>, y en vez de un resultado con
	/// motivo: para quien llama solo hay dos desenlaces útiles —hay catálogo nuevo o se sigue
	/// con el que ya está guardado—. Un fallo de red aquí no es un error que reportar al
	/// operador, porque el CA 2 dice que la copia local sostiene la operación sin conexión.
	/// </para>
	/// <para>
	/// También devuelve <see langword="null"/> si el servidor contesta un catálogo inservible
	/// —sin tipos o sin severidades—: sustituir una copia local buena por una vacía dejaría al
	/// operador sin poder capturar.
	/// </para>
	/// </remarks>
	Task<CatalogosOperacion?> ObtenerVigentesAsync(
		string accessToken,
		CancellationToken cancelacion = default);
}
