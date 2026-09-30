namespace AppOperador.Aplicacion.Modelos;

public enum MotivoRechazoAcceso
{
	CredencialInvalida = 1,

	SinPermiso = 2,

	UbicacionNoDisponible = 3,

	SesionOfflineExpirada = 4,

	SinComunicacion = 5,

	CuentaInactiva = 6,

	CuentaBloqueada = 7,

	SinUnidades = 8,

	// Para no confundir un fallo del servicio con un rechazo de credenciales.
	ErrorDelServicio = 9,

	// Los tres códigos se unifican: distinguirlos solo le serviría a un atacante.
	DesafioNoValido = 10,

	// Hay que refrescar la lista, no reintentar con la misma unidad.
	UnidadNoAutorizada = 11,

	SesionRevocada = 12,
}
