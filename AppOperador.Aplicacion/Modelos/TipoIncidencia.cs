namespace AppOperador.Aplicacion.Modelos;

// El Id es estable y es lo que viaja; la nota obligatoria sale de ExigeDescripcion, nunca del nombre.
public sealed record TipoIncidencia(int Id, string Nombre, bool ExigeDescripcion = false)
{
	public override string ToString() => Nombre;
}
