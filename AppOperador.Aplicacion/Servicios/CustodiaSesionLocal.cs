using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Servicios;

// Abre y cierra juntos los tres rastros de la sesión (memoria, token y persistida). Nunca toca los pendientes.
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

	public SesionOperador? Actual => _sesiones.Actual;

	public async Task AbrirAsync(
		SesionOfflinePersistida sesion,
		string? accessToken,
		CancellationToken cancelacion = default)
	{
		ArgumentNullException.ThrowIfNull(sesion);

		// Token nulo conserva el guardado. Va primero para no anunciar una sesión sin credencial.
		if (!string.IsNullOrWhiteSpace(accessToken))
		{
			await _tokens.GuardarAsync(accessToken, cancelacion);
		}

		await _persistida.GuardarAsync(sesion, cancelacion);
		_sesiones.Guardar(sesion.ComoSesionOperador());
	}

	public async Task RevocarAsync(CancellationToken cancelacion = default)
	{
		_sesiones.Limpiar();
		await _tokens.LimpiarAsync(cancelacion);
		await _persistida.LimpiarAsync(cancelacion);
	}

	public Task<SesionOfflinePersistida?> ObtenerPersistidaAsync(CancellationToken cancelacion = default) =>
		_persistida.ObtenerAsync(cancelacion);

	public Task<string?> ObtenerTokenAsync(CancellationToken cancelacion = default) =>
		_tokens.ObtenerAsync(cancelacion);
}
