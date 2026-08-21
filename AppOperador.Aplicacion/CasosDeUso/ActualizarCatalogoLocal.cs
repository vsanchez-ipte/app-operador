using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Refresca la copia local del catálogo desde Jacob (JTT-1394 CA 4).
/// </summary>
/// <remarks>
/// <para>
/// El criterio dice que los catálogos locales se actualizan «durante una validación en línea o
/// sincronización». Es decir, <b>en los momentos que ya existen</b>: no hay temporizador nuevo
/// ni sondeo propio. Un reloj en segundo plano gastaría batería en campo para adelantar unos
/// minutos un catálogo que cambia de mes en mes.
/// </para>
/// <para>
/// <b>Nunca falla hacia afuera.</b> Si no hay red, si el token no sirve o si el servidor
/// contesta algo ilegible, se conserva la copia que ya estaba y quien llamó sigue su camino: el
/// acceso del operador no puede depender de que el catálogo se haya podido refrescar. Es la
/// misma razón por la que el CA 2 existe.
/// </para>
/// </remarks>
public sealed class ActualizarCatalogoLocal
{
	private readonly ICatalogosJacobClient _cliente;
	private readonly ICatalogoRepository _catalogo;
	private readonly IAuditLog? _bitacora;

	/// <param name="bitacora">
	/// Opcional: deja constancia de que el catálogo se renovó. No se registra el fallo, que es
	/// el caso normal sin conexión y llenaría la bitácora de ruido.
	/// </param>
	public ActualizarCatalogoLocal(
		ICatalogosJacobClient cliente,
		ICatalogoRepository catalogo,
		IAuditLog? bitacora = null)
	{
		_cliente = cliente;
		_catalogo = catalogo;
		_bitacora = bitacora;
	}

	/// <summary>
	/// Descarga el catálogo y sustituye el guardado.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> si se actualizó; <see langword="false"/> si se conservó el
	/// anterior, que no es un error.
	/// </returns>
	public async Task<bool> EjecutarAsync(string? accessToken, CancellationToken cancelacion = default)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			return false;
		}

		var descargado = await _cliente.ObtenerVigentesAsync(accessToken, cancelacion);
		if (descargado is null)
		{
			return false;
		}

		await _catalogo.ReemplazarAsync(descargado, cancelacion);

		if (_bitacora is not null)
		{
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Info,
				$"Catálogo actualizado a la versión {descargado.Version:yyyy-MM-dd}: "
					+ $"{descargado.Tipos.Count} tipos y {descargado.Severidades.Count} severidades.",
				cancelacion);
		}

		return true;
	}
}
