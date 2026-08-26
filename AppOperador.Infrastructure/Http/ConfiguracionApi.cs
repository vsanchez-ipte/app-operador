namespace AppOperador.Infrastructure.Http;

/// <summary>
/// Configuración de la comunicación con el API de Jacob CCO.
/// </summary>
/// <remarks>
/// <para>
/// Configuración tipada y no cadenas sueltas repartidas por el código: la URL se decide en
/// un solo punto y el compilador avisa si alguien la pide mal.
/// </para>
/// <para>
/// <b>No contiene secretos</b> y por eso puede versionarse. Las credenciales las teclea el
/// operador y los tokens irán a <c>SecureStorage</c> cuando exista sesión (JTT-1380).
/// </para>
/// </remarks>
public sealed class ConfiguracionApi
{
	/// <summary>
	/// URL base del API vista desde el <b>emulador de Android</b>.
	/// </summary>
	/// <remarks>
	/// <c>10.0.2.2</c> es la dirección con la que el emulador alcanza al equipo anfitrión.
	/// Dentro del emulador, <c>localhost</c> es el propio dispositivo virtual, así que
	/// apuntar a <c>http://localhost:5231</c> falla siempre con "conexión rechazada".
	/// En un teléfono físico hay que poner la IP de la máquina en la red local.
	/// </remarks>
	public const string UrlBaseEmuladorAndroid = "http://10.0.2.2:5231";

	/// <summary>URL base del API vista desde el escritorio (pruebas y Windows).</summary>
	public const string UrlBaseEscritorio = "http://localhost:5231";

	/// <summary>
	/// URL base del API en el servidor de desarrollo de IPTE.
	/// </summary>
	/// <remarks>
	/// Es una dirección de la red interna: el dispositivo tiene que estar en esa red para
	/// alcanzarla. Va por HTTP y no por HTTPS porque ese despliegue no publica el 443, así
	/// que el host figura en la lista de tráfico en claro de Android.
	/// </remarks>
	public const string UrlBaseDesarrollo = "http://192.168.100.215:81";

	/// <summary>
	/// URL base del API en el servidor de QA de IPTE.
	/// </summary>
	/// <remarks>
	/// Es el ambiente donde prueba el equipo de calidad, con sus propios datos. Vale lo mismo
	/// que para desarrollo: dirección de la red interna, por HTTP porque ese despliegue
	/// tampoco publica el 443, así que el host figura en la lista de tráfico en claro de
	/// Android.
	/// </remarks>
	public const string UrlBaseQa = "http://192.168.100.230:81";

	/// <summary>Dirección del API. Debe terminar sin barra final.</summary>
	public string UrlBase { get; init; } = UrlBaseEscritorio;

	/// <summary>
	/// Si la app habla con Jacob CCO de verdad o sigue con los simuladores.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Encendido es lo normal.</b> El acceso en dos pasos, la creación de la sesión, la
	/// reanudación sin conexión, la revalidación y el cierre están completos contra el canal
	/// móvil de Jacob.
	/// </para>
	/// <para>
	/// Apagado, la app conserva el recorrido íntegro contra simuladores. Sigue sirviendo
	/// para demostrar las pantallas sin levantar el servidor, pero <b>no</b> ejercita nada
	/// del canal real: ni sesión persistida, ni revalidación, ni cierre remoto.
	/// </para>
	/// </remarks>
	public bool UsarApiReal { get; init; }

	/// <summary>
	/// Plataforma declarada en la preautenticación. Solo alimenta la auditoría del API:
	/// no cambia permisos ni comportamiento.
	/// </summary>
	public string Plataforma { get; init; } = "Android";

	/// <summary>
	/// Tiempo máximo de espera de cada petición.
	/// </summary>
	/// <remarks>
	/// Corto a propósito: en campo, una espera larga contra un servidor inalcanzable es
	/// peor que un fallo rápido que deje al operador reintentar o seguir sin conexión.
	/// </remarks>
	public TimeSpan TiempoDeEspera { get; init; } = TimeSpan.FromSeconds(20);

	/// <summary>Ruta de la llave pública. Anónima.</summary>
	public const string RutaLlavePublica = "/ITS/Login/GetPublicKey";

	/// <summary>Ruta de la preautenticación. Anónima.</summary>
	public const string RutaPreauth = "/ITS/AppLogin/Preauth";

	/// <summary>
	/// Ruta del segundo paso del acceso, que crea la sesión. Anónima: el desafío es la
	/// credencial.
	/// </summary>
	public const string RutaLogin = "/ITS/AppLogin";

	/// <summary>
	/// Ruta del cierre de sesión. Exige el token en la cabecera.
	/// </summary>
	/// <remarks>
	/// Es idempotente en el servidor: cerrar dos veces la misma sesión responde
	/// <c>200</c>, no un error. Eso permite reintentar sin comprobar antes si ya se cerró.
	/// </remarks>
	public const string RutaLogout = "/ITS/AppLogin/Logout";

	/// <summary>
	/// Ruta de la revalidación de sesión. Exige el token y que la sesión siga activa.
	/// </summary>
	/// <remarks>
	/// Renueva la ventana offline otras ocho horas tras comprobar que la sesión, el
	/// operador, el permiso y la unidad siguen vigentes. No emite un token nuevo.
	/// </remarks>
	public const string RutaRevalidar = "/ITS/AppLogin/Revalidar";

	/// <summary>
	/// Ruta de la comprobación de comunicación. Exige el token y sesión activa.
	/// </summary>
	/// <remarks>
	/// Respuesta mínima y pensada para invocarse con frecuencia: no devuelve datos del
	/// operador ni de la unidad. Es la sonda del indicador de enlace (JTT-1391).
	/// </remarks>
	public const string RutaEstado = "/ITS/AppLogin/Estado";
}
