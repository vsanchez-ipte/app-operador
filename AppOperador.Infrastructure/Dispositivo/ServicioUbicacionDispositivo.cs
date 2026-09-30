using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Infrastructure.Dispositivo;

// Siempre una lectura nueva: la última conocida puede estar a kilómetros en un vehículo en marcha.
public sealed class ServicioUbicacionDispositivo : ILocationService
{
	// Un arranque en frío tarda de tres a ocho segundos.
	private static readonly TimeSpan TiempoDeEspera = TimeSpan.FromSeconds(10);

	public async Task<LecturaUbicacion> ObtenerPosicionAsync(CancellationToken cancelacion = default)
	{
		try
		{
			// Antes de leer, para que el permiso llegue como motivo propio y la pantalla ofrezca Ajustes.
			var permiso = await MainThread.InvokeOnMainThreadAsync(
				() => Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>());

			if (permiso != PermissionStatus.Granted)
			{
				return LecturaUbicacion.Sin(MotivoSinKilometro.PermisoDenegado);
			}

			var peticion = new GeolocationRequest(GeolocationAccuracy.High, TiempoDeEspera);
			var lectura = await Geolocation.GetLocationAsync(peticion, cancelacion);

			if (lectura is null)
			{
				// Sin excepción y sin lectura: el GPS no fijó dentro del tiempo de espera.
				return LecturaUbicacion.Sin(MotivoSinKilometro.ErrorAlObtener);
			}

			// Sin precisión declarada, se trata como imprecisa.
			var precision = lectura.Accuracy ?? double.MaxValue;

			return LecturaUbicacion.Con(PosicionDispositivo.Crear(
				lectura.Latitude,
				lectura.Longitude,
				precision,
				lectura.Timestamp.UtcDateTime));
		}
		catch (FeatureNotSupportedException)
		{
			// El dispositivo no tiene GPS.
			return LecturaUbicacion.Sin(MotivoSinKilometro.ServicioNoDisponible);
		}
		catch (FeatureNotEnabledException)
		{
			// Lo tiene y está apagado. Para el operador es lo mismo: no hay de dónde leer.
			return LecturaUbicacion.Sin(MotivoSinKilometro.ServicioNoDisponible);
		}
		catch (PermissionException)
		{
			return LecturaUbicacion.Sin(MotivoSinKilometro.PermisoDenegado);
		}
		catch (NotImplementedException)
		{
			// Destino net10.0: las API de MAUI no existen fuera del dispositivo.
			return LecturaUbicacion.Sin(MotivoSinKilometro.ServicioNoDisponible);
		}
		catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
		{
			throw;
		}
		// Un fallo del GPS no tumba la captura: el kilómetro se puede teclear.
		catch (Exception)
		{
			return LecturaUbicacion.Sin(MotivoSinKilometro.ErrorAlObtener);
		}
	}
}
