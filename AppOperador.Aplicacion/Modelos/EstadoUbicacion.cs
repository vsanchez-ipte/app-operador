namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Situación de la ubicación del dispositivo: el servicio del sistema y el permiso que la
/// App Operador tiene sobre él, resumidos en un solo valor.
/// </summary>
/// <remarks>
/// <para>
/// JTT-1380 exige <b>distinguir</b> estos casos entre sí. Colapsarlos en un booleano
/// "hay ubicación / no hay" deja al operador sin saber qué hacer: no es lo mismo un permiso
/// que nunca se pidió —basta con concederlo— que uno bloqueado de forma permanente, que
/// solo se corrige desde la configuración del sistema.
/// </para>
/// <para>
/// La traducción a texto y a botones vive en la presentación; aquí solo se identifica la
/// situación, para que la capa de aplicación no dependa de cadenas de interfaz. Es el mismo
/// criterio que sigue <see cref="MotivoRechazoAcceso"/>.
/// </para>
/// </remarks>
public enum EstadoUbicacion
{
	/// <summary>Servicio habilitado y permiso concedido. Es el único estado que deja acceder.</summary>
	Concedido = 1,

	/// <summary>El permiso nunca se le ha pedido al operador en este dispositivo.</summary>
	/// <remarks>
	/// Se separa de <see cref="Rechazado"/> a propósito: aquí todavía no hubo una negativa,
	/// así que la app puede pedirlo sin insistir sobre una decisión ya tomada.
	/// </remarks>
	NoSolicitado = 2,

	/// <summary>El operador negó el permiso, pero el sistema todavía admite volver a pedirlo.</summary>
	Rechazado = 3,

	/// <summary>
	/// El permiso está negado y el sistema ya no muestra el diálogo: solo se puede cambiar
	/// desde la configuración de la app.
	/// </summary>
	/// <remarks>
	/// En Android corresponde al "no volver a preguntar"; en iOS, a cualquier negativa, porque
	/// el sistema no vuelve a preguntar nunca. También cubre el permiso restringido por
	/// controles parentales o por política del dispositivo.
	/// </remarks>
	BloqueadoPermanentemente = 4,

	/// <summary>
	/// El permiso puede estar concedido, pero el servicio de ubicación del dispositivo está
	/// apagado. Se corrige en la configuración del sistema, no en la de la app.
	/// </summary>
	ServicioDesactivado = 5,

	/// <summary>El dispositivo no cuenta con servicio de ubicación.</summary>
	/// <remarks>No hay acción que ofrecer: no es algo que el operador pueda activar.</remarks>
	NoDisponibleEnElDispositivo = 6,

	/// <summary>No se pudo determinar el estado por un fallo técnico.</summary>
	/// <remarks>
	/// Existe por la misma razón que <see cref="MotivoRechazoAcceso.ErrorDelServicio"/>: un
	/// fallo de la app no puede presentarse como una negativa del operador. Bloquea el acceso
	/// —el requisito es obligatorio y no se pudo comprobar—, pero se le dice al operador que
	/// reintente, no que conceda algo que quizá ya concedió.
	/// </remarks>
	ErrorAlConsultar = 7,
}
