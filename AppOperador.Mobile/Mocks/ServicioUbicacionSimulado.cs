using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Ubicación simulada, para el escritorio y el recorrido simulado.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ya no sustituye al GPS real.</b> Hasta JTT-1395 ésta era la única implementación de
/// <see cref="ILocationService"/> y se registraba sin condición, también con el canal real:
/// devolvía siempre <c>130+200</c> y el formulario se abría con ese kilómetro rotulado como
/// lectura de satélite. Ahora el dispositivo usa <c>ServicioUbicacionDispositivo</c> y esto queda
/// donde debía estar: en escritorio, donde no hay GPS que leer.
/// </para>
/// <para>
/// <b>Devuelve una coordenada, no un kilómetro.</b> Es deliberado: así el recorrido simulado
/// atraviesa la misma geometría y las mismas reglas de tolerancia que el real, y un error en la
/// proyección se ve también aquí. Un simulador que devolviera el kilómetro ya hecho volvería a
/// saltarse justo la parte que interesa probar.
/// </para>
/// </remarks>
public sealed class ServicioUbicacionSimulado : ILocationService
{
	/// <summary>Coordenada de la paleta PK 130 del KMZ del corredor.</summary>
	/// <remarks>
	/// Un punto real sobre la traza, no uno inventado: proyectado da <c>130+000</c>, así que
	/// cualquier desvío delata un fallo en la geometría o en la proyección.
	/// </remarks>
	private const double LatitudPk130 = 32.5275769;

	/// <inheritdoc cref="LatitudPk130" />
	private const double LongitudPk130 = -116.6861878;

	/// <summary>Permite forzar el caso «sin lectura» para recorrer la captura manual.</summary>
	public bool DevuelveLectura { get; set; } = true;

	/// <summary>Motivo con el que falla cuando <see cref="DevuelveLectura"/> está apagado.</summary>
	public MotivoSinKilometro MotivoDeFallo { get; set; } = MotivoSinKilometro.ServicioNoDisponible;

	/// <summary>Precisión que declara la lectura simulada, en metros.</summary>
	/// <remarks>Subirla por encima del umbral permite ejercitar el rechazo por precisión.</remarks>
	public double PrecisionMetros { get; set; } = 8;

	/// <inheritdoc />
	public Task<LecturaUbicacion> ObtenerPosicionAsync(CancellationToken cancelacion = default)
	{
		cancelacion.ThrowIfCancellationRequested();

		if (!DevuelveLectura)
		{
			return Task.FromResult(LecturaUbicacion.Sin(MotivoDeFallo));
		}

		return Task.FromResult(LecturaUbicacion.Con(PosicionDispositivo.Crear(
			LatitudPk130,
			LongitudPk130,
			PrecisionMetros,
			DateTime.UtcNow)));
	}
}
