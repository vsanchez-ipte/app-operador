using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Almacenamiento;

// Solo en Android, iOS y Mac Catalyst: en net10.0 SecureStorage lanza y en Windows exige identidad de paquete.
public sealed class AlmacenTokenSeguro : ITokenProvider
{
	internal const string Clave = "appoperador.sesion.token";

	public Task GuardarAsync(string accessToken, CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();
		return SecureStorage.Default.SetAsync(Clave, accessToken);
	}

	public Task<string?> ObtenerAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();
		return SecureStorage.Default.GetAsync(Clave);
	}

	public Task LimpiarAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		SecureStorage.Default.Remove(Clave);
		return Task.CompletedTask;
	}
}
