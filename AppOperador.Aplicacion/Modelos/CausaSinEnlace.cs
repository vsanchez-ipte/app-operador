namespace AppOperador.Aplicacion.Modelos;

// Lo que importa es si el servidor contestó: sin respuesta es la red; con error, es soporte.
public enum CausaSinEnlace
{
	Ninguna = 0,

	SinTransporte,

	SesionRechazada,

	RespuestaDeError,

	SinSesion,
}
