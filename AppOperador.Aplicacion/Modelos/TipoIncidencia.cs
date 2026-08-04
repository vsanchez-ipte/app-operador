namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Entrada del catálogo de tipos de incidencia de campo.
/// </summary>
/// <remarks>
/// El catálogo definitivo lo entrega Jacob y todavía no está cerrado. Lo único fijado es
/// que existe un tipo "Otro" que obliga a describir la incidencia (JTT-333), y por eso
/// se marca con <see cref="ExigeDescripcion"/> en lugar de comparar el nombre por texto.
/// </remarks>
/// <param name="Clave">Identificador estable del tipo.</param>
/// <param name="Nombre">Texto que ve el operador.</param>
/// <param name="ExigeDescripcion">Si obliga a capturar una nota mínima.</param>
public sealed record TipoIncidencia(string Clave, string Nombre, bool ExigeDescripcion = false)
{
	public override string ToString() => Nombre;
}
