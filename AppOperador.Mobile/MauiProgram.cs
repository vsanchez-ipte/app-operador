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

	// Los campos ya van dentro de un Border; el subrayado de Material sobra y desalinea.
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

	// Todo singleton: una sola conexión a la base y el mismo estado en las cuatro pestañas.
	private static void RegistrarServicios(IServiceCollection servicios)
	{
		servicios.AddSingleton<IClock, RelojSistema>();
		servicios.AddSingleton<IMonotonicClock, RelojMonotonicoDispositivo>();
		servicios.AddSingleton<ISessionStore, AlmacenSesionEnMemoria>();

		servicios.AddSingleton<CapacidadesDeLaSesion>();

		// Datos que Jacob no devuelve porque no los conoce.
		servicios.AddSingleton(new DatosDeInstalacion(
			VersionAplicacion: AmbienteDeCompilacion.EtiquetaDeVersion(AppInfo.Current.VersionString),
			VersionCatalogos: new DateOnly(2026, 7, 23)));

		RegistrarCustodiaDelToken(servicios);
		servicios.AddSingleton<IAuthenticationService, ServicioAutenticacionSimulado>();

		RegistrarUbicacion(servicios);

		// Por su tipo además de la interfaz: los repositorios necesitan la conexión que ILocalDatabase no expone.
		servicios.AddSingleton(sp => new BaseDatosLocal(
			rutaArchivo: null,
			claves: sp.GetService<IDatabaseKeyProvider>()));
		servicios.AddSingleton<ILocalDatabase>(sp => sp.GetRequiredService<BaseDatosLocal>());
		servicios.AddSingleton<IAuditLog, BitacoraAuditoriaSqlite>();
		servicios.AddSingleton<IIncidentRepository, RepositorioIncidenciasSqlite>();

		// Aparte de las incidencias: es caché reemplazable del servidor, no datos que solo existen aquí.
		servicios.AddSingleton<ICatalogoRepository, RepositorioCatalogoSqlite>();
		servicios.AddSingleton<ISyncQueueService, ColaSincronizacionSqlite>();

		// Aparte: la evidencia se reintenta por su cuenta y no revierte su incidencia.
		servicios.AddSingleton<IRepositorioEvidencias, RepositorioEvidenciasSqlite>();

		// Datos y no caché: el sistema vacía la caché y se perdería una evidencia pendiente.
		servicios.AddSingleton<IAlmacenEvidencias>(
			_ => new AlmacenEvidenciasDispositivo(FileSystem.AppDataDirectory));

		// Sin variante simulada: en escritorio responde que no está disponible y la pantalla no ofrece el botón.
		servicios.AddSingleton<ISelectorEvidencia, SelectorEvidenciaDispositivo>();

		// Caché: la copia para el visor es desechable y el proveedor de archivos no expone el directorio de datos.
		servicios.AddSingleton<IVisorEvidencia>(
			_ => new VisorEvidenciaDispositivo(FileSystem.CacheDirectory));

		// Sobre el directorio de datos, donde caen la base y las evidencias.
		servicios.AddSingleton<IEspacioDispositivo>(
			_ => new MedidorEspacioDispositivo(FileSystem.AppDataDirectory));
		servicios.AddSingleton<AdjuntarEvidencia>();
		servicios.AddSingleton<ConsultarAlmacenamientoLocal>();
		servicios.AddSingleton<QuitarEvidencia>();
		servicios.AddSingleton<ObtenerEvidenciasDeIncidencia>();
		servicios.AddSingleton<EliminarBorrador>();

#if EXPORTAR_BASE_DATOS
		// Solo en paquetes compilados con -p:HabilitarExportacionBaseDatos=true.
		servicios.AddSingleton<IExportadorBaseDatos>(sp => new ExportadorBaseDatosSqlite(
			sp.GetRequiredService<BaseDatosLocal>(),
			sp.GetRequiredService<IClock>()));
#endif

		// El token va aparte, en el almacenamiento seguro; aquí solo los metadatos.
		servicios.AddSingleton<IOfflineSessionStore, AlmacenSesionOfflineSqlite>();
		servicios.AddSingleton<ITokenClaims, LectorClaimsToken>();

		// Punto único donde se abre y se cierra el rastro local de la sesión.
		servicios.AddSingleton<CustodiaSesionLocal>();

		// Singleton: lo escribe quien cierra la sesión y lo lee la pantalla de acceso.
		servicios.AddSingleton<AvisoDeSesionTerminada>();
		servicios.AddSingleton<ComprobarVigenciaOffline>();

		// Singleton: el aviso de modo offline debe verse igual en las cuatro pestañas.
		servicios.AddSingleton<EstadoEnlaceViewModel>();

		RegistrarCanalJacob(servicios);

		// Después del canal: el cierre usa el cliente de Jacob si está registrado.
		servicios.AddSingleton<CerrarSesionMovil>();

		// Uno solo: dos dispararían dos tandas por reconexión. Va después del canal porque necesita el sincronizador.
		servicios.AddSingleton<SincronizacionAutomatica>();
	}

	// SecureStorage solo existe en el dispositivo; en escritorio el token vive en memoria.
	private static void RegistrarCustodiaDelToken(IServiceCollection servicios)
	{
#if ANDROID || IOS || MACCATALYST
		servicios.AddSingleton<ITokenProvider, AlmacenTokenSeguro>();

		// Mismo almacén que el token; sin proveedor, la base se abre en claro (escritorio).
		servicios.AddSingleton<IDatabaseKeyProvider, ClaveBaseDatosSegura>();
#else
		servicios.AddSingleton<ITokenProvider, AlmacenTokenEnMemoria>();
#endif
	}

	// En Windows el simulador responde «concedido»: ahí no hay permiso de ubicación que conceder.
	private static void RegistrarUbicacion(IServiceCollection servicios)
	{
#if ANDROID || IOS || MACCATALYST
		servicios.AddSingleton<ILocationPermissionService, ServicioPermisoUbicacionDispositivo>();
		servicios.AddSingleton<ILocationService, ServicioUbicacionDispositivo>();
#else
		servicios.AddSingleton<ILocationPermissionService, ServicioPermisoUbicacionSimulado>();
		servicios.AddSingleton<ILocationService, ServicioUbicacionSimulado>();
#endif

		servicios.AddSingleton<VerificarUbicacionParaAcceso>();
		servicios.AddSingleton<IRepositorioGeometriaCorredor, RepositorioGeometriaCorredorEmbebido>();
		servicios.AddSingleton<ObtenerKilometroPorUbicacion>();
	}

	// Único interruptor entre el recorrido simulado y el real; lo decide el ambiente de compilación.
	private static void RegistrarCanalJacob(IServiceCollection servicios)
	{
		var configuracion = AmbienteDeCompilacion.Resolver();

		servicios.AddSingleton(configuracion);

		if (!configuracion.UsarApiReal)
		{
			// Sin canal no hay a quién sondear: el enlace se simula.
			servicios.AddSingleton<IConnectivityService, ServicioConectividadSimulado>();

			// Sin esto el recorrido simulado no tendría tipos ni severidades con qué capturar.
			servicios.AddSingleton<ICatalogosJacobClient, CatalogoSimulado>();
			servicios.AddSingleton<IIncidenciasJacobClient, EnvioIncidenciasSimulado>();
			servicios.AddSingleton<ActualizarCatalogoLocal>();
			servicios.AddSingleton<ConvertirBorradorEnIncidencia>();
			servicios.AddSingleton<CorregirIncidenciaRechazada>();
			servicios.AddSingleton<ISincronizadorIncidencias, SincronizarIncidencias>();
			return;
		}

		servicios.AddSingleton<IAccesoJacobClient>(sp =>
		{
			var opciones = sp.GetRequiredService<ConfiguracionApi>();
			var http = new HttpClient { Timeout = opciones.TiempoDeEspera };
			return new ClienteAccesoJacob(http, opciones, sp.GetRequiredService<ITokenClaims>());
		});

		// Cliente propio: ClienteAccesoJacob no registra nada porque por él pasan contraseñas.
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

		// Multipart y con más tiempo de espera: 15 MB por datos móviles no caben en el margen de un JSON.
		servicios.AddSingleton<IEvidenciasJacobClient>(sp =>
		{
			var opciones = sp.GetRequiredService<ConfiguracionApi>();
			var http = new HttpClient { Timeout = opciones.TiempoDeEspera * 4 };
			return new ClienteEvidenciasJacob(http, opciones);
		});

		servicios.AddSingleton<ActualizarCatalogoLocal>();
		servicios.AddSingleton<ConvertirBorradorEnIncidencia>();
		servicios.AddSingleton<CorregirIncidenciaRechazada>();
		servicios.AddSingleton<ISincronizadorIncidencias, SincronizarIncidencias>();

		// Red del dispositivo más una sonda autenticada a Jacob.
		servicios.AddSingleton<IConnectivityService, ServicioConectividadJacob>();

		// Transitorio: cada pantalla de acceso retiene su propio desafío.
		servicios.AddTransient<AbrirSesionMovil>();

		// Solo con canal real: sin él no hay sesión persistida que reanudar.
		servicios.AddSingleton<ReanudarSesionOffline>();
		servicios.AddSingleton<RevalidarSesionMovil>();
	}

	// Transitorios: el estado que debe sobrevivir vive en los servicios.
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
