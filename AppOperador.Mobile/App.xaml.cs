namespace AppOperador.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// La maqueta define una sola apariencia y el operador trabaja siempre con ella,
		// así que se fija el tema claro en lugar de seguir el del sistema.
		UserAppTheme = AppTheme.Light;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
