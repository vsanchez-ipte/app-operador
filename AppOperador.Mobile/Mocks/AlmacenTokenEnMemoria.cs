using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Token de sesión en memoria, para los destinos sin almacenamiento seguro utilizable.
/// </summary>
/// <remarks>
/// El token se pierde al cerrar la app, que para la demostración en escritorio es aceptable:
/// restaurar la sesión al arrancar no es de esta historia. En Android e iOS se registra
/// <c>AlmacenTokenSeguro</c>, que sí lo conserva.
/// </remarks>
public sealed class AlmacenTokenEnMemoria : ITokenProvider
{
	private string? _token;

	public Task GuardarAsync(string accessToken, CancellationToken cancelacion = default)
	{
		_token = accessToken;
		return Task.CompletedTask;
	}

	public Task<string?> ObtenerAsync(CancellationToken cancelacion = default) => Task.FromResult(_token);

	public Task LimpiarAsync(CancellationToken cancelacion = default)
	{
		_token = null;
		return Task.CompletedTask;
	}
}
