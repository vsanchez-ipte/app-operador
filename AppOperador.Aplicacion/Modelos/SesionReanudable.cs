namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Una sesión guardada que se podría reanudar ahora mismo sin volver a autenticarse.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que la pantalla de acceso pueda <b>decirlo antes de que el operador decida</b>.
/// Cuando Android mata el proceso —al revocar un permiso desde Ajustes, por ejemplo— la app
/// vuelve a abrirse en el acceso con la sesión intacta en el almacenamiento seguro, y el único
/// rastro es un botón que dice «Continuar offline», que con red no se lee como «reanudar mi
/// sesión» (JTT-1681).
/// </para>
/// <para>
/// Es una <b>lectura</b>: no reanuda, no renueva, no revoca ni escribe en la bitácora. Eso lo
/// sigue haciendo <c>ReanudarSesionOffline.ReanudarAsync</c> cuando el operador lo pide.
/// </para>
/// </remarks>
/// <param name="Operador">A quién pertenece la sesión, para que el aviso lo nombre.</param>
/// <param name="VenceUtc">Hasta cuándo se puede reanudar, como lo publica el servidor.</param>
public sealed record SesionReanudable(string Operador, DateTime VenceUtc);
