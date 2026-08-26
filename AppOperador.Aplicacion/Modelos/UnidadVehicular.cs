namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Unidad del catálogo vehicular que el operador puede seleccionar al acceder.
/// </summary>
/// <remarks>
/// <para>
/// JTT-279 exige que la unidad se elija del catálogo y no admita texto libre, por eso el
/// acceso trabaja siempre con esta lista y nunca con una cadena escrita a mano.
/// Los campos definitivos de la unidad siguen abiertos (DA-04).
/// </para>
/// <para>
/// <b><see cref="Id"/> y <see cref="Clave"/> no son lo mismo y no son intercambiables.</b>
/// La clave es lo que ve el operador; el identificador es lo que viaja al API al completar
/// el acceso (JTT-1381 CA 6). Confundirlos hace que Jacob rechace la unidad, porque revalida
/// contra el identificador, no contra el número económico.
/// </para>
/// </remarks>
/// <param name="Id">
/// Identificador técnico que emite Jacob CCO. Solo puede venir del catálogo que devuelve la
/// preautenticación: la app no lo construye ni deja que se teclee (JTT-1381 CA 10).
/// </param>
/// <param name="Clave">Identificador visible, por ejemplo <c>VEH-01</c>.</param>
/// <param name="Descripcion">Texto de apoyo para distinguir la unidad.</param>
public sealed record UnidadVehicular(string Id, string Clave, string Descripcion)
{
	/// <summary>Lo que se muestra en la lista desplegable.</summary>
	public override string ToString() => Clave;
}
