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
}
