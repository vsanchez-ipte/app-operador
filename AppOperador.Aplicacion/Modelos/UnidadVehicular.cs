namespace AppOperador.Aplicacion.Modelos;

// Id viaja al API y Clave es lo que ve el operador; Jacob revalida contra el Id.
public sealed record UnidadVehicular(
	string Id,
	string Clave,
	string Descripcion,
	bool Disponible = true,
	EstadoUnidad? Estado = null)
{
	public override string ToString() => Clave;
}

// Color hexadecimal del catálogo de Jacob: el mismo de los badges del CCO.
public sealed record EstadoUnidad(string Clave, string Nombre, string Color);

public sealed record CatalogoUnidades(IReadOnlyList<UnidadVehicular> Unidades, DateTime? SincronizadoUtc);
