namespace AppOperador.Aplicacion.Modelos;

// SesionRechazada no tiene estado propio: la app expulsa al acceso.
public enum EstadoComunicacion
{
	EnLinea,

	SinConexion,

	Revalidando,

	ErrorDeServicio,
}
