using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

// El backend no ve el GPS: el prerrequisito se comprueba y se bloquea aquí. Ningún fallo del dispositivo escapa.
public sealed class VerificarUbicacionParaAcceso
{
	private readonly ILocationPermissionService _ubicacion;

	public VerificarUbicacionParaAcceso(ILocationPermissionService ubicacion) => _ubicacion = ubicacion;

	// Se repite al volver a la pantalla, por si el operador corrigió el permiso en Ajustes.
	public Task<ResultadoUbicacion> RevisarAsync(CancellationToken cancelacion = default) =>
		EvaluarAsync(() => _ubicacion.ConsultarEstadoAsync(cancelacion));

	// Solo se pide solo si nunca hubo respuesta: tras una negativa, decide el operador.
	public async Task<ResultadoUbicacion> ExigirAsync(CancellationToken cancelacion = default)
	{
		var resultado = await RevisarAsync(cancelacion);

		return resultado.Estado == EstadoUbicacion.NoSolicitado
			? await SolicitarPermisoAsync(cancelacion)
			: resultado;
	}

	public Task<ResultadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default) =>
		EvaluarAsync(() => _ubicacion.SolicitarPermisoAsync(cancelacion));

	// Si devuelve false la pantalla lo dice: un botón que no hace nada es peor que ninguno.
	public async Task<bool> AbrirAjustesAsync(AccionUbicacion accion, CancellationToken cancelacion = default)
	{
		try
		{
			return accion switch
			{
				AccionUbicacion.AbrirAjustesDeLaApp => await _ubicacion.AbrirAjustesDeLaAppAsync(cancelacion),
				AccionUbicacion.AbrirAjustesDeUbicacion => await _ubicacion.AbrirAjustesDeUbicacionAsync(cancelacion),
				_ => false,
			};
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static async Task<ResultadoUbicacion> EvaluarAsync(Func<Task<EstadoUbicacion>> consulta)
	{
		try
		{
			return ResultadoUbicacion.Para(await consulta());
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception)
		{
			return ResultadoUbicacion.Para(EstadoUbicacion.ErrorAlConsultar);
		}
	}
}
