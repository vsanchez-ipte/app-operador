namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Clave con la que se cifra la base de datos local (JTT-1388 CA 2).
/// </summary>
/// <remarks>
/// <para>
/// El nombre va en inglés como el resto de contratos publicados en el documento de
/// arquitectura (<c>ISessionStore</c>, <c>ITokenProvider</c>, …).
/// </para>
/// <para>
/// <b>La clave no se elige ni se deriva de nada que el operador teclee.</b> Se genera al azar
/// la primera vez y se custodia en el almacenamiento seguro de la plataforma. Derivarla de la
/// contraseña obligaría a tenerla a mano para abrir la base, y la contraseña no se almacena
/// (CA 3).
/// </para>
/// </remarks>
public interface IDatabaseKeyProvider
{
	/// <summary>
	/// Devuelve la clave de la base, generándola y guardándola si es la primera vez.
	/// </summary>
	/// <remarks>
	/// Llamarla dos veces devuelve la misma clave: si cambiara, la base dejaría de abrirse y
	/// con ella se perderían los pendientes.
	/// </remarks>
	Task<string> ObtenerAsync(CancellationToken cancelacion = default);
}
