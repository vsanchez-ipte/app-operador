using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

public sealed class ActualizarCatalogoLocal
{
	private readonly ICatalogosJacobClient _cliente;
	private readonly ICatalogoRepository _catalogo;
	private readonly IAuditLog? _bitacora;

	public ActualizarCatalogoLocal(
		ICatalogosJacobClient cliente,
		ICatalogoRepository catalogo,
		IAuditLog? bitacora = null)
	{
		_cliente = cliente;
		_catalogo = catalogo;
		_bitacora = bitacora;
	}

	// Nunca falla hacia afuera: si no se pudo refrescar, se conserva la copia anterior.
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
