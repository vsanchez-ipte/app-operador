using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Infrastructure.Dispositivo;

/// <summary>
/// Lectura real del GPS del dispositivo, con las API de plataforma (JTT-1395 CA 1 y 2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sustituye al simulador que devolvía siempre <c>130+200</c>.</b> Aquel se registraba sin
/// condición —también con el canal real encendido—, así que hasta el 24-ago el formulario se
/// abría con un kilómetro constante rotulado «KM (GPS)». Una incidencia capturada así llegaba al
/// CCO en el kilómetro equivocado y declarada como lectura de satélite, que es peor que no traer
/// kilómetro: nadie duda de ella.
/// </para>
/// <para>
/// <b>No devuelve la última posición conocida.</b> <c>GetLastKnownLocationAsync</c> contesta al
/// instante y es justo lo que no sirve aquí: en un vehículo en marcha, una lectura de hace cinco
/// minutos son kilómetros de diferencia, y llegaría con su sello de tiempo viejo a una incidencia
/// que dice ser de ahora. Se pide siempre una lectura nueva y se acepta que tarde.
/// </para>
/// <para>
/// <b>Dónde corre.</b> Solo se registra en Android, iOS y Mac Catalyst. Compila también en el
/// destino <c>net10.0</c> —los proyectos de prueba arrastran el ensamblado—, pero ahí las API de
/// MAUI lanzan y eso se traduce en <see cref="MotivoSinKilometro.ServicioNoDisponible"/> en vez de
/// romper la pantalla.
/// </para>
/// </remarks>
public sealed class ServicioUbicacionDispositivo : ILocationService
{
	/// <summary>
	/// Cuánto se espera a que el GPS fije antes de rendirse.
	/// </summary>
	/// <remarks>
	/// Diez segundos. Un arranque en frío bajo cielo abierto tarda entre tres y ocho; menos que
	/// eso devolvería «no se pudo» a quien solo tenía que esperar un momento, y el operador
	/// aprendería a no usar el botón. Más que eso deja la pantalla en suspenso sin decir nada.
	/// </remarks>
	private static readonly TimeSpan TiempoDeEspera = TimeSpan.FromSeconds(10);

	/// <inheritdoc />
	public async Task<LecturaUbicacion> ObtenerPosicionAsync(CancellationToken cancelacion = default)
	{
		try
		{
			// El permiso se consulta antes de pedir la posición para poder distinguirlo: si se
			// deja que lo resuelva la excepción, el motivo llega mezclado con cualquier otro
			// fallo y la pantalla no puede ofrecer los ajustes, que es lo único accionable.
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

			// Sin precisión declarada no hay forma de saber si la lectura vale, y una posición sin
			// verificar proyectada sobre el corredor da un kilómetro perfectamente creíble. Se
			// devuelve un número imposible de aceptar en vez de suponer que estaba bien: el caso
			// de uso lo rechazará por precisión, que es exactamente lo que ocurre.
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
			// La cancelación es del llamador y no es un fallo del GPS: se deja subir.
			throw;
		}
		catch (Exception)
		{
			// Red de seguridad. Un fallo al leer la ubicación no puede tumbar la captura: el
			// operador tiene el kilómetro delante, en la paleta, y puede teclearlo.
			return LecturaUbicacion.Sin(MotivoSinKilometro.ErrorAlObtener);
		}
	}
}
