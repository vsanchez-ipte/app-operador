namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Entrada del catálogo de tipos de incidencia de campo (JTT-1394).
/// </summary>
/// <remarks>
/// <para>
/// <b>El identificador es el entero de Jacob</b>, no una clave de texto propia. Antes era
/// <c>string Clave</c> con valores inventados en la maqueta —<c>OBJETO</c>, <c>VEHICULO</c>…—
/// que no existen en ningún servidor. El catálogo real identifica por entero autonumérico, y
/// **ese entero es estable**: lo que cambia con el tiempo es el nombre.
/// </para>
/// <para>
/// Por eso la app <b>envía el id y guarda además el nombre del momento</b>: una incidencia
/// capturada sin conexión puede sincronizarse días después, y el histórico tiene que seguir
/// diciendo lo que el operador leyó al capturar.
/// </para>
/// <para>
/// <b>Ninguna capa conoce la palabra «Otro».</b> La nota obligatoria se decide con
/// <see cref="ExigeDescripcion"/>, que publica el propio catálogo y el backend revalida. El id
/// de «Otro» ni siquiera es el mismo en todos los ambientes: <c>idtipoincidencia</c> es
/// <c>GENERATED ALWAYS AS IDENTITY</c> y cada base decide el suyo.
/// </para>
/// </remarks>
/// <param name="Id">Identificador del tipo en Jacob. Es lo que viaja al sincronizar.</param>
/// <param name="Nombre">Texto que ve el operador.</param>
/// <param name="ExigeDescripcion">Si obliga a capturar una nota mínima.</param>
public sealed record TipoIncidencia(int Id, string Nombre, bool ExigeDescripcion = false)
{
	public override string ToString() => Nombre;
}
