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

	/// <summary>Dirección del API. Debe terminar sin barra final.</summary>
	public string UrlBase { get; init; } = UrlBaseEscritorio;

	/// <summary>
	/// Si la app habla con Jacob CCO de verdad o sigue con los simuladores.
	/// </summary>
	/// <remarks>
	/// <para>
	/// JTT-1378 solo implementa la preautenticación: no crea sesión ni entra a la app. Con
	/// el interruptor apagado la app conserva el recorrido completo contra simuladores, que
	/// es lo que permite seguir demostrando las cinco pantallas mientras el canal móvil no
	/// esté desplegado.
	/// </para>
	/// <para>
	/// Encendido, el botón de acceso ejerce el flujo real
	/// <c>GetPublicKey → cifrado → Preauth</c> y se detiene ahí a propósito.
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
}
