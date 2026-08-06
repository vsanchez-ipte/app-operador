namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Causas por las que se niega el acceso a la app.
/// </summary>
/// <remarks>
/// Cada valor corresponde a uno de los mensajes literales que exige JTT-279. El texto
/// vive en la capa de presentación; aquí solo se identifica la causa, para que la capa
/// de aplicación no dependa de cadenas de interfaz.
/// </remarks>
public enum MotivoRechazoAcceso
{
	/// <summary>Usuario o contraseña no válidos.</summary>
	CredencialInvalida = 1,

	/// <summary>La cuenta existe pero no tiene el permiso funcional de la App Operador.</summary>
	SinPermiso = 2,

	/// <summary>El servicio de ubicación está apagado o sin permiso concedido.</summary>
	UbicacionNoDisponible = 3,

	/// <summary>No hay una validación en línea vigente para reanudar sin conexión.</summary>
	SesionOfflineExpirada = 4,

	/// <summary>No se pudo alcanzar a Jacob CCO para validar por primera vez.</summary>
	SinComunicacion = 5,

	/// <summary>La cuenta está desactivada (<c>appoperador.cuenta.inactiva</c>).</summary>
	/// <remarks>
	/// JTT-279 no fijó texto para este caso; queda pendiente de acordar con Producto.
	/// </remarks>
	CuentaInactiva = 6,

	/// <summary>
	/// Bloqueo temporal tras varios intentos fallidos (<c>appoperador.cuenta.bloqueada</c>).
	/// </summary>
	/// <remarks>
	/// El API devuelve los segundos restantes dentro de <c>mensajeError</c>. JTT-1378 solo
	/// recibe y distingue el caso; la cuenta regresiva es de una historia posterior.
	/// </remarks>
	CuentaBloqueada = 7,

	/// <summary>
	/// El operador no tiene unidades activas asignadas (<c>appoperador.sin.vehiculos</c>).
	/// </summary>
	/// <remarks>JTT-279 tampoco fijó texto para este caso.</remarks>
	SinUnidades = 8,

	/// <summary>
	/// Jacob respondió algo que la app no sabe interpretar: código desconocido, cuerpo que
	/// no es un <c>Envelope</c>, <c>401</c> sin cuerpo o error interno del API.
	/// </summary>
	/// <remarks>
	/// Existe para no confundir un fallo del servicio con un rechazo de credenciales: son
	/// situaciones distintas y el operador debe poder distinguirlas.
	/// </remarks>
	ErrorDelServicio = 9,
}
