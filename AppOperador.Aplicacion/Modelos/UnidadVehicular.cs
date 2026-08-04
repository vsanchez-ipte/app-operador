namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Unidad del catálogo vehicular que el operador puede seleccionar al acceder.
/// </summary>
/// <remarks>
/// JTT-279 exige que la unidad se elija del catálogo y no admita texto libre, por eso el
/// acceso trabaja siempre con esta lista y nunca con una cadena escrita a mano.
/// Los campos definitivos de la unidad siguen abiertos (DA-04).
/// </remarks>
/// <param name="Clave">Identificador visible, por ejemplo <c>VEH-01</c>.</param>
/// <param name="Descripcion">Texto de apoyo para distinguir la unidad.</param>
public sealed record UnidadVehicular(string Clave, string Descripcion)
{
	/// <summary>Lo que se muestra en la lista desplegable.</summary>
	public override string ToString() => Clave;
}
