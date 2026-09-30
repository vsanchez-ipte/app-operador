using AppOperador.Aplicacion.Servicios;

namespace AppOperador.Mobile;

public partial class App : Application
{
	// La sincronización automática vive aquí porque App es lo único que dura lo que la aplicación.
	public App(SincronizacionAutomatica sincronizacionAutomatica)
	{
		InitializeComponent();

		// La maqueta define una sola apariencia.
		UserAppTheme = AppTheme.Light;

		// Solo se suscribe: al arrancar aún no hay sesión con que sincronizar.
		sincronizacionAutomatica.Iniciar();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
