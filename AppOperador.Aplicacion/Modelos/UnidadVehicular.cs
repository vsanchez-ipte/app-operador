namespace AppOperador.Aplicacion.Modelos;

// Id viaja al API y Clave es lo que ve el operador; Jacob revalida contra el Id.
public sealed record UnidadVehicular(string Id, string Clave, string Descripcion)
{
	public override string ToString() => Clave;
}
