namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Custodia del token de acceso de la sesión móvil.
/// </summary>
/// <remarks>
/// <para>
/// El nombre está fijado en inglés por el documento de arquitectura, que lo lista entre las
/// interfaces iniciales junto a <c>ISessionStore</c>.
/// </para>
/// <para>
/// <b>El token no va a SQLite.</b> La distribución de datos locales del documento de
/// arquitectura (§4.1) reserva el almacenamiento seguro de la plataforma para tokens y
/// secretos, y deja SQLite para los metadatos. Un token en la base local viajaría en
/// cualquier respaldo del dispositivo.
/// </para>
/// <para>
/// La contraseña <b>nunca</b> pasa por aquí: vive en memoria durante el acceso y se descarta.
/// </para>
/// </remarks>
public interface ITokenProvider
{
	/// <summary>Guarda el token de la sesión recién creada.</summary>
	Task GuardarAsync(string accessToken, CancellationToken cancelacion = default);

	/// <summary>Recupera el token vigente, o <see langword="null"/> si no hay ninguno.</summary>
	Task<string?> ObtenerAsync(CancellationToken cancelacion = default);

	/// <summary>
	/// Borra el token.
	/// </summary>
	/// <remarks>
	/// Se llama al cerrar sesión. No toca los pendientes de la cola local, que deben
	/// sobrevivir al cierre (JTT-279 CA8).
	/// </remarks>
	Task LimpiarAsync(CancellationToken cancelacion = default);
}
