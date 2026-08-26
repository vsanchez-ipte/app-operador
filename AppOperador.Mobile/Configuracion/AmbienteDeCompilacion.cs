using AppOperador.Infrastructure.Http;

namespace AppOperador.Mobile.Configuracion;

/// <summary>
/// A qué ambiente apunta el paquete que se está compilando.
/// </summary>
/// <remarks>
/// <para>
/// El ambiente se elige al compilar, con la propiedad <c>Ambiente</c> de MSBuild:
/// </para>
/// <code>
/// dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=Desarrollo
/// </code>
/// <para>
/// Sin esa propiedad se compila <c>Local</c>, que es lo que necesita quien desarrolla. El
/// <c>.csproj</c> traduce el valor a un símbolo de compilación y aborta si no reconoce el
/// nombre, así que un ambiente mal escrito falla en el build y no en el dispositivo.
/// </para>
/// <para>
/// <b>Ambientes cerrados y no una URL libre a propósito.</b> Cada ambiente trae su
/// dirección ya revisada y la lista de tráfico en claro de Android solo admite esos
/// destinos; con una URL suelta en la línea de comandos, un paquete podría salir apuntando
/// a cualquier parte sin que nadie lo notara al revisarlo.
/// </para>
/// </remarks>
internal static class AmbienteDeCompilacion
{
	/// <summary>Nombre del ambiente al que apunta este paquete.</summary>
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

	/// <summary>
	/// Acompaña la versión con el ambiente cuando el paquete no es el de desarrollo local.
	/// </summary>
	/// <remarks>
	/// Un paquete armado para el servidor de desarrollo y uno armado en el equipo de quien
	/// programa se ven idénticos una vez instalados. Como el perfil ya muestra la versión,
	/// llevarla acompañada del ambiente evita revisar el comportamiento de un paquete
	/// creyendo que es otro. El ambiente local no se anuncia: ahí no hay confusión posible.
	/// </remarks>
	public static string EtiquetaDeVersion(string version) =>
#if AMBIENTE_DESARROLLO || AMBIENTE_QA || AMBIENTE_SIMULADO
		$"{version} · {Nombre}";
#else
		version;
#endif

	/// <summary>
	/// Configuración del canal de Jacob CCO que corresponde a este ambiente.
	/// </summary>
	/// <remarks>
	/// La plataforma solo alimenta la auditoría del API. La URL, en cambio, sí depende de
	/// dónde corra la app: en el ambiente local, el emulador de Android alcanza al equipo
	/// anfitrión por <c>10.0.2.2</c>, mientras que el servidor de desarrollo tiene una
	/// dirección de red que se ve igual desde cualquier destino.
	/// </remarks>
	public static ConfiguracionApi Resolver() => new()
	{
#if AMBIENTE_SIMULADO
		// Recorrido completo contra simuladores: sirve para mostrar las pantallas sin
		// depender de que haya un servidor levantado y alcanzable.
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
