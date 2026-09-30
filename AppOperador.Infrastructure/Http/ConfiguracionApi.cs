namespace AppOperador.Infrastructure.Http;

// Sin secretos: se puede versionar.
public sealed class ConfiguracionApi
{
	// 10.0.2.2 es el anfitrión visto desde el emulador; localhost sería el propio emulador.
	public const string UrlBaseEmuladorAndroid = "http://10.0.2.2:5231";

	public const string UrlBaseEscritorio = "http://localhost:5231";

	// Red interna y HTTP: el despliegue no publica el 443.
	public const string UrlBaseDesarrollo = "http://192.168.100.215:81";

	// HTTPS en el 81; el certificado es de una CA interna declarada en network_security_config.xml.
	public const string UrlBaseQa = "https://192.168.100.230:81";

	public string UrlBase { get; init; } = UrlBaseEscritorio;

	// Apagado, la app corre contra simuladores y no ejercita nada del canal real.
	public bool UsarApiReal { get; init; }

	public string Plataforma { get; init; } = "Android";

	// Corto: en campo un fallo rápido es mejor que una espera larga.
	public TimeSpan TiempoDeEspera { get; init; } = TimeSpan.FromSeconds(20);

	public const string RutaLlavePublica = "/ITS/Login/GetPublicKey";

	public const string RutaPreauth = "/ITS/AppLogin/Preauth";

	public const string RutaLogin = "/ITS/AppLogin";

	// Idempotente: cerrar dos veces responde 200.
	public const string RutaLogout = "/ITS/AppLogin/Logout";

	public const string RutaRevalidar = "/ITS/AppLogin/Revalidar";

	// Sonda liviana para uso frecuente.
	public const string RutaEstado = "/ITS/AppLogin/Estado";

	// Una sola llamada para una sola versión de catálogo. Exige el permiso general.
	public const string RutaCatalogos = "/ITS/AppCatalogos/Vigentes";

	// Exige el permiso de captura. Idempotente por uuid.
	public const string RutaIncidencias = "/ITS/AppIncidencias";
}
