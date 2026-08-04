using AppOperador.Aplicacion.Interfaces;
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
	/// Hoy todas son simuladores que viven en <c>Mocks</c>: permiten recorrer las
	/// pantallas sin el canal móvil de Jacob ni base local. Al llegar JTT-1345 y
	/// JTT-1347, aquí se cambia el tipo concreto por el de <c>Infrastructure</c> y ni los
	/// ViewModels ni las vistas se enteran, porque solo conocen la interfaz.
	///
	/// El almacén y la conectividad son singleton a propósito: el estado tiene que ser
	/// el mismo en las cuatro pestañas.
	/// </remarks>
	private static void RegistrarServicios(IServiceCollection servicios)
	{
		servicios.AddSingleton<IClock, RelojSistema>();
		servicios.AddSingleton<IConnectivityService, ServicioConectividadSimulado>();
		servicios.AddSingleton<ISessionStore, AlmacenSesionEnMemoria>();
		servicios.AddSingleton<IAuditLog, BitacoraEnMemoria>();
		servicios.AddSingleton<ILocationService, ServicioUbicacionSimulado>();

		// Ocupa el lugar de la base SQLite: compartido por el repositorio y la cola.
		servicios.AddSingleton<AlmacenRegistrosEnMemoria>();
		servicios.AddSingleton<IIncidentRepository, RepositorioIncidenciasEnMemoria>();
		servicios.AddSingleton<ISyncQueueService, ColaSincronizacionEnMemoria>();
		servicios.AddSingleton<IAuthenticationService, ServicioAutenticacionSimulado>();
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
