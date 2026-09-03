using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.CasosDeUso;

/// <summary>
/// Comprueba el prerrequisito de ubicación antes de dejar entrar al operador (JTT-1380).
/// </summary>
/// <remarks>
/// <para>
/// JTT-279 declara como prerrequisito PR3 que el servicio de ubicación esté habilitado y que
/// la App Operador tenga permiso para usarlo. El backend no puede comprobarlo —no recibe
/// coordenadas ni ve el GPS de un teléfono—, así que la comprobación y el bloqueo ocurren
/// aquí, en la app.
/// </para>
/// <para>
/// Está en la capa de aplicación, y no dentro de un servicio de autenticación, por dos
/// razones: es un requisito del <b>dispositivo</b> y no de Jacob CCO, y así vale igual para
/// el acceso en línea y para la reanudación sin conexión, sin repetir la regla en cada
/// camino. También la hace probable sin dispositivo: todo lo que toca hardware queda detrás
/// de <see cref="ILocationPermissionService"/>.
/// </para>
/// <para>
/// Ningún método deja escapar una excepción del dispositivo: un fallo técnico se convierte en
/// <see cref="EstadoUbicacion.ErrorAlConsultar"/>, que bloquea igual pero no se le presenta al
/// operador como una negativa suya. Es el mismo criterio que impuso JTT-1378 CA 11 para no
/// disfrazar errores técnicos de credenciales inválidas.
/// </para>
/// </remarks>
public sealed class VerificarUbicacionParaAcceso
{
	private readonly ILocationPermissionService _ubicacion;

	public VerificarUbicacionParaAcceso(ILocationPermissionService ubicacion) => _ubicacion = ubicacion;

	/// <summary>
	/// Evalúa el estado sin pedirle nada al operador.
	/// </summary>
	/// <remarks>
	/// Es la comprobación que se repite al volver a la pantalla: si el operador salió a la
	/// configuración del sistema y corrigió el permiso, aquí es donde se entera la app.
	/// </remarks>
	public Task<ResultadoUbicacion> RevisarAsync(CancellationToken cancelacion = default) =>
		EvaluarAsync(() => _ubicacion.ConsultarEstadoAsync(cancelacion));

	/// <summary>
	/// Evalúa el estado y, si el permiso nunca se ha pedido, lo pide una vez.
	/// </summary>
	/// <remarks>
	/// Es la comprobación del momento del acceso. Solo se pide solo cuando nunca hubo
	/// respuesta: si el operador ya dijo que no, insistir sin que él lo pida es acoso, y en
	/// Android un segundo diálogo automático puede resolverse en negativa sin llegar a
	/// mostrarse. Con el permiso ya rechazado, la pantalla ofrece el botón y decide él.
	/// </remarks>
	public async Task<ResultadoUbicacion> ExigirAsync(CancellationToken cancelacion = default)
	{
		var resultado = await RevisarAsync(cancelacion);

		return resultado.Estado == EstadoUbicacion.NoSolicitado
			? await SolicitarPermisoAsync(cancelacion)
			: resultado;
	}

	/// <summary>
	/// Pide el permiso al sistema y devuelve el veredicto posterior.
	/// </summary>
	public Task<ResultadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default) =>
		EvaluarAsync(() => _ubicacion.SolicitarPermisoAsync(cancelacion));

	/// <summary>
	/// Abre la pantalla de configuración que corresponde a la acción indicada.
	/// </summary>
	/// <returns>
	/// <see langword="true"/> si se abrió. Si devuelve <see langword="false"/> la pantalla
	/// debe decirlo: un botón que no hace nada visible es peor que no tener botón.
	/// </returns>
	/// <remarks>
	/// No reevalúa el estado al terminar y no puede hacerlo: la app queda en segundo plano
	/// mientras el operador está en la configuración del sistema. La reevaluación ocurre al
	/// volver a la pantalla, con <see cref="RevisarAsync"/>.
	/// </remarks>
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
			// No hay configuración que abrir en este dispositivo, o el sistema la rechazó.
			// La pantalla lo informa; no es motivo para tumbar el acceso con una excepción.
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
			// La cancelación la pidió quien llama: no es un fallo de ubicación y se propaga.
			throw;
		}
		catch (Exception)
		{
			return ResultadoUbicacion.Para(EstadoUbicacion.ErrorAlConsultar);
		}
	}
}
