using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Estado real de la ubicación del dispositivo, resuelto con las API de plataforma (JTT-1380).
/// </summary>
/// <remarks>
/// <para>
/// <b>No obtiene coordenadas.</b> Solo responde si el servicio existe, si está encendido y en
/// qué situación está el permiso. Leer una posición para "comprobar" la ubicación sería
/// recolectar un dato personal que esta historia no necesita; la lectura de GPS pertenece a
/// <see cref="ILocationService"/> y a la captura de incidencias.
/// </para>
/// <para>
/// <b>Por qué hace falta recordar si ya se preguntó.</b> Ni Android ni iOS distinguen
/// "todavía no se ha pedido" de "el operador dijo que no": las dos situaciones llegan como
/// permiso denegado. Sin esa marca, la app trataría el primer arranque como un rechazo y
/// mandaría al operador a la configuración del sistema por un permiso que nadie le pidió. La
/// marca vive en <c>Preferences</c>, no en la base local: es estado del dispositivo, no del
/// operador, y debe sobrevivir a cerrar sesión.
/// </para>
/// <para>
/// <b>Dónde corre.</b> Solo se registra en Android, iOS y Mac Catalyst; en escritorio se
/// registra el simulador. Compila igual en el destino <c>net10.0</c> —los proyectos de
/// prueba lo arrastran—, pero ahí las API de MAUI lanzan, y el caso de uso convierte eso en
/// <see cref="EstadoUbicacion.ErrorAlConsultar"/> en vez de conceder el paso.
/// </para>
/// </remarks>
public sealed class ServicioPermisoUbicacionDispositivo : ILocationPermissionService
{
	/// <summary>Marca de que el diálogo del sistema ya se le mostró al operador alguna vez.</summary>
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

		// El diálogo de permisos tiene que salir del hilo de interfaz.
		await MainThread.InvokeOnMainThreadAsync(
			() => Permissions.RequestAsync<Permissions.LocationWhenInUse>());

		// Se marca aunque el operador haya descartado el diálogo: lo que importa es que ya
		// se le preguntó, no lo que contestó.
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

		// Se lanza desde el contexto de la aplicación, no desde una actividad, así que
		// necesita su propia tarea o Android rechaza el intent.
		intencion.AddFlags(Android.Content.ActivityFlags.NewTask);
		Android.App.Application.Context.StartActivity(intencion);

		return Task.FromResult(true);
#else
		// iOS no deja abrir los ajustes de ubicación del sistema desde una app. Lo más cerca
		// que se puede llevar al operador es la ficha de la app, que tiene su propia entrada
		// de Ubicación.
		AppInfo.Current.ShowSettingsUI();
		return Task.FromResult(true);
#endif
	}

	/// <summary>
	/// Traduce el permiso del sistema al estado que entiende la app.
	/// </summary>
	private static EstadoUbicacion Clasificar(PermissionStatus permiso)
	{
		// "Limited" es el permiso aproximado de iOS: alcanza de sobra para el prerrequisito,
		// que solo exige que la app pueda ubicarse.
		if (permiso is PermissionStatus.Granted or PermissionStatus.Limited)
		{
			return ServicioHabilitado() ? EstadoUbicacion.Concedido : EstadoUbicacion.ServicioDesactivado;
		}

		if (permiso == PermissionStatus.Disabled)
		{
			return EstadoUbicacion.ServicioDesactivado;
		}

		// Restringido por controles parentales o política del dispositivo: el operador no
		// puede concederlo desde el diálogo, así que se trata como bloqueo.
		if (permiso == PermissionStatus.Restricted)
		{
			return EstadoUbicacion.BloqueadoPermanentemente;
		}

		if (!Preferences.Default.Get(ClavePermisoSolicitado, false))
		{
			return EstadoUbicacion.NoSolicitado;
		}

		// Si el sistema todavía admite explicar por qué se pide, es que volverá a preguntar.
		// Cuando deja de admitirlo, el diálogo ya no aparece y solo queda la configuración.
		// En iOS siempre es falso, que es justo el comportamiento correcto ahí: una vez
		// negado, el sistema no vuelve a preguntar nunca.
		return Permissions.ShouldShowRationale<Permissions.LocationWhenInUse>()
			? EstadoUbicacion.Rechazado
			: EstadoUbicacion.BloqueadoPermanentemente;
	}

	/// <summary>Indica si el interruptor de ubicación del sistema está encendido.</summary>
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
		// En escritorio no hay interruptor equivalente. Este destino no se registra en la
		// app; la respuesta solo evita que el código deje de compilar.
		return true;
#endif
	}

	/// <summary>Indica si el dispositivo tiene hardware de ubicación.</summary>
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
