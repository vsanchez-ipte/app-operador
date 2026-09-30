using System.Security.Cryptography;
using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Almacenamiento;

// Al azar y una sola vez; si cambiara, la base dejaría de abrirse y se perderían los pendientes.
public sealed class ClaveBaseDatosSegura : IDatabaseKeyProvider
{
	internal const string Clave = "appoperador.basedatos.clave";

	private const int BytesDeClave = 32;

	public async Task<string> ObtenerAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		var guardada = await SecureStorage.Default.GetAsync(Clave);
		if (!string.IsNullOrWhiteSpace(guardada))
		{
			return guardada;
		}

		var nueva = Convert.ToBase64String(RandomNumberGenerator.GetBytes(BytesDeClave));
		await SecureStorage.Default.SetAsync(Clave, nueva);
		return nueva;
	}
}
