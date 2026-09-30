using AppOperador.Aplicacion.Interfaces;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

public sealed class ServicioUbicacionSimulado : ILocationService
{
	private const double LatitudPk130 = 32.5275769;

	private const double LongitudPk130 = -116.6861878;

	public bool DevuelveLectura { get; set; } = true;

	public MotivoSinKilometro MotivoDeFallo { get; set; } = MotivoSinKilometro.ServicioNoDisponible;

	public double PrecisionMetros { get; set; } = 8;

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
