namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Datos de la instalación que el perfil muestra junto a la sesión y que Jacob CCO no
/// devuelve porque no los conoce.
/// </summary>
/// <remarks>
/// Existe para que la capa de aplicación pueda armar la sesión completa sin preguntarle a
/// MAUI qué versión tiene la app: quien sí lo sabe es el punto de composición, y lo entrega
/// ya resuelto.
/// </remarks>
/// <param name="VersionAplicacion">Versión instalada, tal como la reporta la plataforma.</param>
/// <param name="VersionCatalogos">
/// Fecha del último catálogo descargado. Mientras no exista sincronización de catálogos
/// (JTT-291) es un valor fijo de la compilación.
/// </param>
public sealed record DatosDeInstalacion(string VersionAplicacion, DateOnly VersionCatalogos);
