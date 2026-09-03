namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Lectura de los datos que Jacob firma dentro del token de sesión.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que la capa de aplicación pueda cotejar los permisos contra el token
/// (JTT-1379 CA 8) sin saber que el token es un JWT ni cómo se decodifica. Esa parte es
/// detalle de infraestructura.
/// </para>
/// <para>
/// <b>No verifica la firma</b>: la app no tiene el secreto ni debe tenerlo. Quien valida de
/// verdad es el servidor en cada petición.
/// </para>
/// </remarks>
public interface ITokenClaims
{
	/// <summary>
	/// Módulos que el token declara, o vacío si no se pudo leer.
	/// </summary>
	/// <remarks>
	/// Un token ilegible devuelve una lista vacía, que no respalda ningún permiso: la
	/// consecuencia es bloquear, que es la salida correcta ante algo que no se entiende.
	/// </remarks>
	IReadOnlyList<string> ModulosDe(string? token);
}
