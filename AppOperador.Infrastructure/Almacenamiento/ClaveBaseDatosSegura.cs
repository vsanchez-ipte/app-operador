using System.Security.Cryptography;
using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Infrastructure.Almacenamiento;

/// <summary>
/// Clave de la base local, custodiada en el almacenamiento seguro (JTT-1388 CA 2).
/// </summary>
/// <remarks>
/// <para>
/// Misma custodia que el token: <c>SecureStorage</c>, respaldado por el KeyStore en Android y
/// por el llavero en iOS. Guardarla junto a la base no protegería de nada —quien copie el
/// archivo copiaría también la clave— y por eso va al único sitio del sistema pensado para
/// secretos.
/// </para>
/// <para>
/// <b>Se genera una vez y no vuelve a cambiar.</b> Si cambiara, la base dejaría de abrirse y
/// con ella se perderían los pendientes, que es justo lo que los criterios 8 y 11 exigen
/// conservar.
/// </para>
/// <para>
/// 32 bytes al azar de un generador criptográfico, en Base64. No se deriva de la contraseña
/// del operador: haría falta tenerla para abrir la base y la contraseña no se almacena (CA 3).
/// </para>
/// </remarks>
public sealed class ClaveBaseDatosSegura : IDatabaseKeyProvider
{
	internal const string Clave = "appoperador.basedatos.clave";

	private const int BytesDeClave = 32;

	/// <inheritdoc />
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
