using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

/// <summary>
/// Punto único donde se abre y se cierra el rastro local de una sesión.
/// </summary>
/// <remarks>
/// <para>
/// Una sesión deja huella en <b>tres</b> sitios: la sesión viva en memoria
/// (<see cref="ISessionStore"/>), el token en el almacenamiento seguro
/// (<see cref="ITokenProvider"/>) y la sesión persistida para reanudar sin conexión
/// (<see cref="IOfflineSessionStore"/>).
/// </para>
/// <para>
/// <b>Existe porque tenerlos sueltos ya salió caro.</b> Cuatro casos de uso repetían la
/// secuencia y uno se dejaba la sesión persistida sin borrar, así que el rastro sobrevivía a
/// una revocación. Aquí se abre y se cierra entero o no se hace: quien llama no tiene que
/// acordarse de los tres pasos.
/// </para>
/// <para>
/// <b>Nunca toca los registros pendientes.</b> Lo capturado en campo es del operador y de la
/// unidad, no de la sesión, y sobrevive a cualquier cierre o revocación (JTT-1390 CA 5).
/// </para>
/// </remarks>
public sealed class CustodiaSesionLocal
{
	private readonly ISessionStore _sesiones;
	private readonly ITokenProvider _tokens;
	private readonly IOfflineSessionStore _persistida;

	public CustodiaSesionLocal(
		ISessionStore sesiones,
		ITokenProvider tokens,
		IOfflineSessionStore persistida)
	{
		_sesiones = sesiones;
		_tokens = tokens;
		_persistida = persistida;
	}

	/// <summary>Sesión viva, o <see langword="null"/> si nadie ha ingresado.</summary>
	public SesionOperador? Actual => _sesiones.Actual;

	/// <summary>
	/// Deja la sesión abierta en los tres sitios.
	/// </summary>
	/// <param name="accessToken">
	/// Token nuevo, o <see langword="null"/> para conservar el que ya está guardado. Al
	/// revalidar no llega uno nuevo y sobrescribirlo con vacío dejaría la sesión sin
	/// credencial.
	/// </param>
	/// <remarks>
	/// El token va primero: si fallara al guardarse, es preferible no haber anunciado una
	/// sesión que después no podría autenticar ninguna petición.
	/// </remarks>
	public async Task AbrirAsync(
		SesionOfflinePersistida sesion,
		string? accessToken,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(sesion);

		if (!string.IsNullOrWhiteSpace(accessToken))
		{
			await _tokens.GuardarAsync(accessToken, cancelacion);
		}

		await _persistida.GuardarAsync(sesion, cancelacion);
		_sesiones.Guardar(sesion.ComoSesionOperador());
	}

	/// <summary>Borra el rastro local completo de la sesión.</summary>
	/// <remarks>
	/// Lo usan el cierre voluntario (JTT-1390), la revocación del permiso (JTT-1379 CA 6) y
	/// la revalidación negada (JTT-1383 CA 11). Los tres quieren exactamente lo mismo.
	/// </remarks>
	public async Task RevocarAsync(CancellationToken cancelacion = default)
	{
		_sesiones.Limpiar();
		await _tokens.LimpiarAsync(cancelacion);
		await _persistida.LimpiarAsync(cancelacion);
	}

	/// <summary>Sesión guardada para reanudar sin conexión, si la hay.</summary>
	public Task<SesionOfflinePersistida?> ObtenerPersistidaAsync(CancellationToken cancelacion = default) =>
		_persistida.ObtenerAsync(cancelacion);

	/// <summary>Token de la sesión, si lo hay.</summary>
	public Task<string?> ObtenerTokenAsync(CancellationToken cancelacion = default) =>
		_tokens.ObtenerAsync(cancelacion);
}
