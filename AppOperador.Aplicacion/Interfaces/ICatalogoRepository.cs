using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Copia local de los catálogos de Jacob (JTT-1394 CA 2).
/// </summary>
/// <remarks>
/// Es lo que sostiene la captura sin conexión. Se separa de <see cref="IIncidentRepository"/>
/// porque son dos cosas de naturaleza distinta: el catálogo es <b>caché reemplazable</b> del
/// servidor y las incidencias son datos que solo existen en el dispositivo hasta que se
/// sincronizan. Mezclarlas invitaba a borrar unas creyendo que se limpiaba lo otro.
/// </remarks>
public interface ICatalogoRepository
{
	/// <summary>
	/// Catálogo guardado, o <see cref="CatalogosOperacion.Vacio"/> si nunca se ha descargado.
	/// </summary>
	Task<CatalogosOperacion> ObtenerAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Sustituye el catálogo guardado por el que entregó el servidor.
	/// </summary>
	/// <remarks>
	/// <b>Reemplaza, no mezcla.</b> Un tipo retirado del catálogo tiene que desaparecer de la
	/// lista, y fusionar lo nuevo con lo viejo lo dejaría ofreciéndose para siempre. Las
	/// incidencias ya capturadas no se ven afectadas: guardan el nombre del tipo y del nivel del
	/// momento, así que su histórico se lee igual aunque el catálogo cambie.
	/// </remarks>
	Task ReemplazarAsync(CatalogosOperacion catalogos, CancellationToken cancelacion = default);
}
