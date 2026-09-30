using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

// No lee coordenadas. Ni Android ni iOS distinguen «nunca pedido» de «negado»: por eso la marca en Preferences.
public sealed class ServicioPermisoUbicacionDispositivo : ILocationPermissionService
{
	internal const string ClavePermisoSolicitado = "ubicacion.permiso.solicitado";

	public async Task<EstadoUbicacion> ConsultarEstadoAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		if (!CaracteristicaDisponible())
		{
			return EstadoUbicacion.NoDisponibleEnElDispositivo;
		}

		var permiso = await MainThread.InvokeOnMainThreadAsync(
			() => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());

		return Clasificar(permiso);
	}

	public async Task<EstadoUbicacion> SolicitarPermisoAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		if (!CaracteristicaDisponible())
		{
			return EstadoUbicacion.NoDisponibleEnElDispositivo;
		}

		// El diálogo tiene que salir del hilo de interfaz.
		await MainThread.InvokeOnMainThreadAsync(
			() => Permissions.RequestAsync<Permissions.LocationWhenInUse>());

		// Se marca aunque descarte el diálogo: importa que ya se le preguntó.
		Preferences.Default.Set(ClavePermisoSolicitado, true);

		return await ConsultarEstadoAsync(cancelacion);
	}

	public Task<bool> AbrirAjustesDeLaAppAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		AppInfo.Current.ShowSettingsUI();
		return Task.FromResult(true);
	}

	public Task<bool> AbrirAjustesDeUbicacionAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

#if ANDROID
		var intencion = new Android.Content.Intent(Android.Provider.Settings.ActionLocationSourceSettings);

		// Desde el contexto de la aplicación Android exige una tarea nueva.
		intencion.AddFlags(Android.Content.ActivityFlags.NewTask);
		Android.App.Application.Context.StartActivity(intencion);

		return Task.FromResult(true);
#else
		// iOS no deja abrir los ajustes de ubicación: se abre la ficha de la app, que tiene su entrada.
		AppInfo.Current.ShowSettingsUI();
		return Task.FromResult(true);
#endif
	}

	private static EstadoUbicacion Clasificar(PermissionStatus permiso)
	{
		// Limited es el permiso aproximado de iOS y basta para el prerrequisito.
		if (permiso is PermissionStatus.Granted or PermissionStatus.Limited)
		{
			return ServicioHabilitado() ? EstadoUbicacion.Concedido : EstadoUbicacion.ServicioDesactivado;
		}

		if (permiso == PermissionStatus.Disabled)
		{
			return EstadoUbicacion.ServicioDesactivado;
		}

		// Restringido por políticas del dispositivo: el operador no puede concederlo.
		if (permiso == PermissionStatus.Restricted)
		{
			return EstadoUbicacion.BloqueadoPermanentemente;
		}

		if (!Preferences.Default.Get(ClavePermisoSolicitado, false))
		{
			return EstadoUbicacion.NoSolicitado;
		}

		// Si el sistema aún admite explicar el porqué, volverá a preguntar. En iOS siempre es falso.
		return Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>()
			? EstadoUbicacion.Rechazado
			: EstadoUbicacion.BloqueadoPermanentemente;
	}

	private static bool ServicioHabilitado()
	{
#if ANDROID
		if (Android.App.Application.Context.GetSystemService(Android.Content.Context.LocationService)
			is not Android.Locations.LocationManager gestor)
		{
			return false;
		}

		if (OperatingSystem.IsAndroidVersionAtLeast(28))
		{
			return gestor.IsLocationEnabled;
		}

		return gestor.IsProviderEnabled(Android.Locations.LocationManager.GpsProvider)
			|| gestor.IsProviderEnabled(Android.Locations.LocationManager.NetworkProvider);
#elif IOS || MACCATALYST
		return CoreLocation.CLLocationManager.LocationServicesEnabled;
#else
		return true;
#endif
	}

	private static bool CaracteristicaDisponible()
	{
#if ANDROID
		return Android.App.Application.Context.PackageManager
			?.HasSystemFeature(Android.Content.PM.PackageManager.FeatureLocation) ?? false;
#else
		return true;
#endif
	}
}
