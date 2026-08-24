using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Infrastructure.Almacenamiento;
using AppOperador.Infrastructure.Dispositivo;
using AppOperador.Infrastructure.Http;
using AppOperador.Infrastructure.Sqlite;
using AppOperador.Mobile.Configuracion;
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

		// Qué autoriza la sesión abierta (JTT-1385). Va aquí, junto al almacén de sesión, porque
		// es lo único que necesita: pregunta por la sesión vigente en cada consulta.
		servicios.AddSingleton<CapacidadesDeLaSesion>();

		// Datos que el perfil muestra junto a la sesión y que Jacob no devuelve porque no los
		// conoce. La versión de catálogos es fija mientras no exista su sincronización.
		servicios.AddSingleton(new DatosDeInstalacion(
			VersionAplicacion: AmbienteDeCompilacion.EtiquetaDeVersion(AppInfo.Current.VersionString),
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
		servicios.AddSingleton(sp => new BaseDatosLocal(
			rutaArchivo: null,
			claves: sp.GetService<IDatabaseKeyProvider>()));
		servicios.AddSingleton<ILocalDatabase>(sp => sp.GetRequiredService<BaseDatosLocal>());
		servicios.AddSingleton<IAuditLog, BitacoraAuditoriaSqlite>();
		servicios.AddSingleton<IIncidentRepository, RepositorioIncidenciasSqlite>();

		// Copia local del catálogo de Jacob (JTT-1394). Va aparte del repositorio de
		// incidencias: uno es caché reemplazable del servidor y el otro son datos que solo
		// existen en el dispositivo hasta que se sincronizan.
		servicios.AddSingleton<ICatalogoRepository, RepositorioCatalogoSqlite>();
		servicios.AddSingleton<ISyncQueueService, ColaSincronizacionSqlite>();

#if EXPORTAR_BASE_DATOS
		// Copia legible de la base para revisarla en el escritorio. Solo existe en paquetes
		// compilados con -p:HabilitarExportacionBaseDatos=true; en cualquier otro, ni esta
		// línea ni la clase que resuelve llegan al paquete.
		// Fábrica explícita: el constructor admite un directorio temporal opcional que solo
		// usan las pruebas, y el contenedor no debe intentar resolverlo como servicio.
		servicios.AddSingleton<IExportadorBaseDatos>(sp => new ExportadorBaseDatosSqlite(
			sp.GetRequiredService<BaseDatosLocal>(),
			sp.GetRequiredService<IClock>()));
#endif

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

		// La clave de la base local va al mismo almacén que el token (JTT-1388 CA 2). Solo se
		// registra donde SecureStorage existe: sin proveedor, la base se abre en claro, que es
		// lo que necesita el destino de escritorio de la demostración.
		servicios.AddSingleton<IDatabaseKeyProvider, ClaveBaseDatosSegura>();
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
	/// A qué servidor apunta y si el canal real está encendido lo decide el ambiente elegido
	/// al compilar (ver <see cref="AmbienteDeCompilacion"/>). Por omisión es el ambiente
	/// local, con el canal real contra el API que corre en el equipo de quien desarrolla.
	/// </para>
	/// </remarks>
	private static void RegistrarCanalJacob(IServiceCollection servicios)
	{
		var configuracion = AmbienteDeCompilacion.Resolver();

		servicios.AddSingleton(configuracion);

		if (!configuracion.UsarApiReal)
		{
			// Sin canal no hay a quién sondear: el estado de enlace se simula, como el resto
			// del recorrido.
			servicios.AddSingleton<IConnectivityService, ServicioConectividadSimulado>();

			// El catálogo también se simula (JTT-1394). Sin esto el recorrido simulado se
			// quedaría sin tipos ni severidades —la base dejó de sembrarlos— y no habría con
			// qué capturar. El simulador hace de Jacob, igual que con los permisos.
			servicios.AddSingleton<ICatalogosJacobClient, CatalogoSimulado>();
			servicios.AddSingleton<IIncidenciasJacobClient, EnvioIncidenciasSimulado>();
			servicios.AddSingleton<ActualizarCatalogoLocal>();
			servicios.AddSingleton<ConvertirBorradorEnIncidencia>();
			servicios.AddSingleton<ISincronizadorIncidencias, SincronizarIncidencias>();
			return;
		}

		servicios.AddSingleton<IAccesoJacobClient>(sp =>
		{
			var opciones = sp.GetRequiredService<ConfiguracionApi>();
			var http = new HttpClient { Timeout = opciones.TiempoDeEspera };
			return new ClienteAccesoJacob(http, opciones, sp.GetRequiredService<ITokenClaims>());
		});

		// Catálogos reales de Jacob (JTT-1394). Cliente propio y no una ruta más en
		// ClienteAccesoJacob: aquel no registra nada a propósito porque por él pasan
		// contraseñas y desafíos, y una consulta de catálogo no necesita esa disciplina.
		servicios.AddSingleton<ICatalogosJacobClient>(sp =>
		{
			var opciones = sp.GetRequiredService<ConfiguracionApi>();
			var http = new HttpClient { Timeout = opciones.TiempoDeEspera };
			return new ClienteCatalogosJacob(http, opciones);
		});

		servicios.AddSingleton<IIncidenciasJacobClient>(sp =>
		{
			var opciones = sp.GetRequiredService<ConfiguracionApi>();
			var http = new HttpClient { Timeout = opciones.TiempoDeEspera };
			return new ClienteIncidenciasJacob(http, opciones);
		});

		servicios.AddSingleton<ActualizarCatalogoLocal>();
		servicios.AddSingleton<ConvertirBorradorEnIncidencia>();
		servicios.AddSingleton<ISincronizadorIncidencias, SincronizarIncidencias>();

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
