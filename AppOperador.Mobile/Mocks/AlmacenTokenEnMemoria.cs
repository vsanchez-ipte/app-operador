using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Mobile.Mocks;

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
