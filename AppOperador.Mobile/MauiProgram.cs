using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Infrastructure.Almacenamiento;
using AppOperador.Infrastructure.Dispositivo;
using AppOperador.Infrastructure.Http;
using AppOperador.Infrastructure.Sqlite;
using AppOperador.Mobile.Mocks;
using AppOperador.Mobile.ViewModels;
using AppOperador.Mobile.Vistas;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace AppOperador.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		QuitarSubrayadoDeCampos();
		RegistrarServicios(builder.Services);
		RegistrarVistas(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	/// <summary>
	/// Quita el subrayado que Android dibuja bajo los campos de texto.
	/// </summary>
	/// <remarks>
	/// La maqueta usa campos con recuadro limpio, no con la línea inferior del estilo
	/// Material. Como los campos ya van dentro de un <c>Border</c>, el subrayado nativo
	/// sobra y desalinea el diseño.
	/// </remarks>
	private static void QuitarSubrayadoDeCampos()
	{
#if ANDROID
		var transparente = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(
			"SinSubrayado", (handler, _) => handler.PlatformView.BackgroundTintList = transparente);

		Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(
			"SinSubrayado", (handler, _) => handler.PlatformView.BackgroundTintList = transparente);

		Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping(
			"SinSubrayado", (handler, _) => handler.PlatformView.BackgroundTintList = transparente);
#endif
	}

	/// <summary>
	/// Registra las implementaciones de los contratos de la capa de aplicación.
	/// </summary>
	/// <remarks>
	/// Aquí conviven dos orígenes y la diferencia importa:
	///
	/// <list type="bullet">
	/// <item><b>SQLite</b> para incidencias, cola y bitácora: es persistencia real, los
	/// datos sobreviven al cierre de la app.</item>
	/// <item><b>Simuladores</b> de <c>Mocks</c> para lo que depende del canal móvil de
	/// Jacob (autenticación) o del hardware (ubicación, conectividad). Se sustituyen al
	/// llegar JTT-1345 y JTT-1347 sin que vistas ni ViewModels se enteren.</item>
	/// </list>
	///
	/// Todo singleton: la base mantiene una sola conexión al archivo, y el estado debe
	/// ser el mismo en las cuatro pestañas.
	/// </remarks>
	private static void RegistrarServicios(IServiceCollection servicios)
	{
		servicios.AddSingleton<IClock, RelojSistema>();
		servicios.AddSingleton<IMonotonicClock, RelojMonotonicoDispositivo>();
		servicios.AddSingleton<ISessionStore, AlmacenSesionEnMemoria>();

		// Datos que el perfil muestra junto a la sesión y que Jacob no devuelve porque no los
		// conoce. La versión de catálogos es fija mientras no exista su sincronización.
		servicios.AddSingleton(new DatosDeInstalacion(
			VersionAplicacion: AppInfo.Current.VersionString,
			VersionCatalogos: new DateOnly(2026, 7, 23)));

		RegistrarCustodiaDelToken(servicios);
		servicios.AddSingleton<ILocationService, ServicioUbicacionSimulado>();
		servicios.AddSingleton<IAuthenticationService, ServicioAutenticacionSimulado>();

		RegistrarUbicacion(servicios);

		// Persistencia real. BaseDatosLocal se registra por su tipo concreto además de por
		// la interfaz porque los repositorios necesitan su conexión interna, que el
		// contrato ILocalDatabase no expone a propósito.
		// Fábrica explícita: el constructor recibe una ruta opcional y el contenedor no
		// debe intentar resolverla como si fuera un servicio.
		servicios.AddSingleton(_ => new BaseDatosLocal());
		servicios.AddSingleton<ILocalDatabase>(sp => sp.GetRequiredService<BaseDatosLocal>());
		servicios.AddSingleton<IAuditLog, BitacoraAuditoriaSqlite>();
		servicios.AddSingleton<IIncidentRepository, RepositorioIncidenciasSqlite>();
		servicios.AddSingleton<ISyncQueueService, ColaSincronizacionSqlite>();

		// Sesión persistida y lectura de los claims del token (JTT-1383). El token sigue
		// aparte, en el almacenamiento seguro: aquí solo van los metadatos.
		servicios.AddSingleton<IOfflineSessionStore, AlmacenSesionOfflineSqlite>();
		servicios.AddSingleton<ITokenClaims, LectorClaimsToken>();

		// Punto único donde se abre y se cierra el rastro local de la sesión: token, sesión
		// viva y sesión persistida. Los cuatro casos de uso que la tocan pasan por aquí.
		servicios.AddSingleton<CustodiaSesionLocal>();

		// Por qué terminó la última sesión, para poder decírselo al operador al volver al
		// acceso. Singleton: lo escribe quien cierra la sesión y lo consume otra pantalla.
		servicios.AddSingleton<AvisoDeSesionTerminada>();
		servicios.AddSingleton<ComprobarVigenciaOffline>();

		// Singleton: el aviso de modo offline debe verse igual en las cuatro pestañas, y una
		// instancia por pantalla haría que cada una mostrara lo suyo (JTT-1383 CA 8).
		servicios.AddSingleton<EstadoEnlaceViewModel>();

		RegistrarCanalJacob(servicios);

		// Después del canal: el cierre recibe el cliente de Jacob si está registrado, y se
		// queda con el cierre puramente local si no lo está (JTT-1390).
		servicios.AddSingleton<CerrarSesionMovil>();
	}

	/// <summary>
	/// Registra dónde se custodia el token de la sesión (JTT-1382).
	/// </summary>
	/// <remarks>
	/// El almacenamiento seguro de la plataforma solo existe en el dispositivo:
	/// <c>SecureStorage</c> lanza en el destino de escritorio, y en Windows exige identidad
	/// de paquete que la demostración no siempre tiene. Ahí se usa el almacén en memoria, que
	/// pierde el token al cerrar la app — aceptable, porque restaurar la sesión al arrancar
	/// no es de esta historia.
	/// </remarks>
	private static void RegistrarCustodiaDelToken(IServiceCollection servicios)
	{
#if ANDROID || IOS || MACCATALYST
		servicios.AddSingleton<ITokenProvider, AlmacenTokenSeguro>();
#else
		servicios.AddSingleton<ITokenProvider, AlmacenTokenEnMemoria>();
#endif
	}

	/// <summary>
	/// Registra la comprobación del prerrequisito de ubicación (JTT-1380).
	/// </summary>
	/// <remarks>
	/// <para>
	/// La implementación real solo se registra donde existen las API de permisos del sistema.
	/// En Windows se registra el simulador, que responde "concedido": es el destino con el que
	/// se demuestran las cinco pantallas, y ahí no hay permiso de ubicación que conceder ni
	/// configuración que abrir. Devolver "no disponible" dejaría la app inutilizable en
	/// escritorio por un requisito que en escritorio no aplica.
	/// </para>
	/// <para>
	/// Como con el canal de Jacob, el interruptor es la inyección de dependencias: ni el
	/// caso de uso ni el ViewModel saben en qué plataforma corren.
	/// </para>
	/// </remarks>
	private static void RegistrarUbicacion(IServiceCollection servicios)
	{
#if ANDROID || IOS || MACCATALYST
		servicios.AddSingleton<ILocationPermissionService, ServicioPermisoUbicacionDispositivo>();
#else
		servicios.AddSingleton<ILocationPermissionService, ServicioPermisoUbicacionSimulado>();
#endif

		servicios.AddSingleton<VerificarUbicacionParaAcceso>();
	}

	/// <summary>
	/// Registra la comunicación con el canal móvil de Jacob CCO (JTT-1378, JTT-1382).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Este es el único interruptor entre la app simulada y la real.</b> Con
	/// <see cref="ConfiguracionApi.UsarApiReal"/> apagado no se registra
	/// <see cref="IAccesoJacobClient"/>, el <c>AccesoViewModel</c> recibe nulo el caso de uso
	/// del acceso y la pantalla se comporta exactamente como antes: recorrido completo contra
	/// simuladores.
	/// </para>
	/// <para>
	/// Encendido, el acceso ejecuta el flujo entero contra el API —credenciales, unidad y
	/// apertura de sesión— y entra a la app.
	/// </para>
	/// <para>
	/// La URL depende de dónde corra la app. Desde el emulador de Android hay que usar
	/// <c>10.0.2.2</c>, porque ahí <c>localhost</c> es el propio dispositivo virtual.
	/// </para>
	/// </remarks>
	private static void RegistrarCanalJacob(IServiceCollection servicios)
	{
		var configuracion = new ConfiguracionApi
		{
			// Encendido por defecto desde JTT-1383: el acceso, la sesión, la reanudación
			// sin conexión, la revalidación y el cierre ya funcionan contra Jacob CCO, así
			// que el canal real es el comportamiento normal de la app y no una prueba.
			// Apagarlo deja el recorrido completo contra simuladores, útil para demostrar
			// las pantallas sin levantar el servidor.
			UsarApiReal = true,
#if ANDROID
			UrlBase = ConfiguracionApi.UrlBaseEmuladorAndroid,
			Plataforma = "Android",
#elif IOS
			UrlBase = ConfiguracionApi.UrlBaseEscritorio,
			Plataforma = "iOS",
#else
			UrlBase = ConfiguracionApi.UrlBaseEscritorio,
#endif
		};

		servicios.AddSingleton(configuracion);

		if (!configuracion.UsarApiReal)
		{
			// Sin canal no hay a quién sondear: el estado de enlace se simula, como el resto
			// del recorrido.
			servicios.AddSingleton<IConnectivityService, ServicioConectividadSimulado>();
			return;
		}

		servicios.AddSingleton<IAccesoJacobClient>(sp =>
		{
			var opciones = sp.GetRequiredService<ConfiguracionApi>();
			var http = new HttpClient { Timeout = opciones.TiempoDeEspera };
			return new ClienteAccesoJacob(http, opciones, sp.GetRequiredService<ITokenClaims>());
		});

		// Estado de enlace real: red del dispositivo más una sonda autenticada a Jacob. Va
		// aquí porque necesita el cliente que se acaba de registrar (JTT-1391).
		servicios.AddSingleton<IConnectivityService, ServicioConectividadJacob>();

		// Transitorio a propósito: cada pantalla de acceso retiene su propio desafío, así que
		// uno no puede filtrarse de un intento a otro.
		servicios.AddTransient<AbrirSesionMovil>();

		// Va aquí y no fuera: sin canal real no hay sesión persistida que reanudar, y
		// registrarla igual dejaría el recorrido del simulador sin su modo offline
		// (JTT-1383).
		servicios.AddSingleton<ReanudarSesionOffline>();
		servicios.AddSingleton<RevalidarSesionMovil>();
	}

	/// <summary>
	/// Registra las páginas y sus ViewModels.
	/// </summary>
	/// <remarks>
	/// Transitorios: cada navegación construye una instancia nueva y limpia. El estado
	/// que debe sobrevivir vive en los servicios, no en el ViewModel.
	/// </remarks>
	private static void RegistrarVistas(IServiceCollection servicios)
	{
		servicios.AddTransient<AccesoViewModel>();
		servicios.AddTransient<InicioViewModel>();
		servicios.AddTransient<CapturaViewModel>();
		servicios.AddTransient<ColaViewModel>();
		servicios.AddTransient<PerfilViewModel>();

		servicios.AddTransient<AccesoPage>();
		servicios.AddTransient<InicioPage>();
		servicios.AddTransient<CapturaPage>();
		servicios.AddTransient<ColaPage>();
		servicios.AddTransient<PerfilPage>();
	}
}
