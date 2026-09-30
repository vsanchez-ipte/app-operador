using AppOperador.Infrastructure.Http;

namespace AppOperador.Mobile.Configuracion;

// Ambientes cerrados y no una URL libre: cada uno tiene su dirección revisada y su permiso de tráfico en claro.
internal static class AmbienteDeCompilacion
{
	public const string Nombre =
#if AMBIENTE_DESARROLLO
		"Desarrollo";
#elif AMBIENTE_QA
		"QA";
#elif AMBIENTE_SIMULADO
		"Simulado";
#else
		"Local";
#endif

	// Instalados, un paquete de Dev y uno local se ven iguales: la versión lleva el ambiente.
	public static string EtiquetaDeVersion(string version) =>
#if AMBIENTE_DESARROLLO || AMBIENTE_QA || AMBIENTE_SIMULADO
		$"{version} · {Nombre}";
#else
		version;
#endif

	public static ConfiguracionApi Resolver() => new()
	{
#if AMBIENTE_SIMULADO
		UsarApiReal = false,
		UrlBase = ConfiguracionApi.UrlBaseEscritorio,
#elif AMBIENTE_DESARROLLO
		UsarApiReal = true,
		UrlBase = ConfiguracionApi.UrlBaseDesarrollo,
#elif AMBIENTE_QA
		UsarApiReal = true,
		UrlBase = ConfiguracionApi.UrlBaseQa,
#elif ANDROID
		UsarApiReal = true,
		UrlBase = ConfiguracionApi.UrlBaseEmuladorAndroid,
#else
		UsarApiReal = true,
		UrlBase = ConfiguracionApi.UrlBaseEscritorio,
#endif

#if ANDROID
		Plataforma = "Android",
#elif IOS
		Plataforma = "iOS",
#endif
	};
}
