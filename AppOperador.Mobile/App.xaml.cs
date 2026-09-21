using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Mobile;

public partial class App : Application
{
	/// <remarks>
	/// Recibe la sincronización automática porque <b>es el único punto que vive tanto como la
	/// aplicación</b> (JTT-1406). Un ViewModel no sirve: son transitorios, y el enlace vuelve
	/// justo cuando el operador no tiene ninguna pantalla suya abierta, que es el caso que esta
	/// historia viene a arreglar.
	/// </remarks>
	public App(SincronizacionAutomatica sincronizacionAutomatica)
	{
		InitializeComponent();

		// La maqueta define una sola apariencia y el operador trabaja siempre con ella,
		// así que se fija el tema claro en lugar de seguir el del sistema.
		UserAppTheme = AppTheme.Light;

		// Solo se suscribe al evento de enlace. No sincroniza ahora: al arrancar todavía no hay
		// sesión con la que autenticarse, y el sincronizador la exige antes de tocar la cola.
		sincronizacionAutomatica.Iniciar();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
