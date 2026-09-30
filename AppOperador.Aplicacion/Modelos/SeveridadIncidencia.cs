namespace AppOperador.Aplicacion.Modelos;

// Orden: menor es más grave. Se guardan nombre y orden del momento de capturar.
public sealed record SeveridadIncidencia(Guid Id, string Nivel, int Orden, string Hexadecimal)
{
	public override string ToString() => Nivel;
}
