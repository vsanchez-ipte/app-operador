using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Almacenamiento;

/// <summary>
/// Token de sesión guardado en el almacenamiento seguro de la plataforma (JTT-1382).
/// </summary>
/// <remarks>
/// <para>
/// Usa <c>SecureStorage</c> de MAUI, que en Android respalda la clave en el KeyStore y en
/// iOS en el llavero. <b>El token no entra a SQLite</b>: la base local viaja en cualquier
/// respaldo del dispositivo, y el documento de arquitectura (§4.1) reserva el almacenamiento
/// seguro para tokens y secretos.
/// </para>
/// <para>
/// La contraseña del operador nunca pasa por aquí: vive en memoria durante el acceso y se
/// descarta al terminar.
/// </para>
/// <para>
/// Solo se registra en Android, iOS y Mac Catalyst. En escritorio se usa el almacén en
/// memoria: <c>SecureStorage</c> lanza en el destino <c>net10.0</c> y en Windows exige
/// identidad de paquete, que la demostración no siempre tiene.
/// </para>
/// </remarks>
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

		// Remove devuelve false si no había nada guardado; da igual, el efecto buscado es
		// que no quede token.
		SecureStorage.Default.Remove(Clave);
		return Task.CompletedTask;
	}
}
