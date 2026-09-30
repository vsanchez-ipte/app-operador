namespace AppOperador.Aplicacion.Modelos;

public enum EstadoUbicacion
{
	Concedido = 1,

	NoSolicitado = 2,

	Rechazado = 3,

	// Android: «no volver a preguntar». iOS: cualquier negativa. También políticas del dispositivo.
	BloqueadoPermanentemente = 4,

	ServicioDesactivado = 5,

	NoDisponibleEnElDispositivo = 6,

	// Bloquea, pero se pide reintentar: un fallo de la app no es una negativa del operador.
	ErrorAlConsultar = 7,
}
