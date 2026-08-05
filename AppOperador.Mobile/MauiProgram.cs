using AppOperador.Aplicacion.Interfaces;
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
		servicios.AddSingleton<IConnectivityService, ServicioConectividadSimulado>();
		servicios.AddSingleton<ISessionStore, AlmacenSesionEnMemoria>();
		servicios.AddSingleton<ILocationService, ServicioUbicacionSimulado>();
		servicios.AddSingleton<IAuthenticationService, ServicioAutenticacionSimulado>();

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
