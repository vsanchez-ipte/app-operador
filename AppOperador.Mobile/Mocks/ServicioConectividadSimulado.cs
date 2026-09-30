using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

public sealed class ServicioConectividadSimulado : IConnectivityService
{
	private bool _hayEnlace = true;

	public bool HayEnlace
	{
		get => _hayEnlace;
		private set
		{
			if (_hayEnlace == value)
			{
				return;
			}

			_hayEnlace = value;
			EnlaceCambio?.Invoke(this, value);
		}
	}

	public event EventHandler<bool>? EnlaceCambio;

	public Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(HayEnlace
			? ResultadoSondeo.Alcanzado()
			: ResultadoSondeo.SinTransporte("Modo offline forzado desde el simulador."));

	public void AnotarIntercambio(bool jacobRespondio)
	{
	}

	public void Alternar() => HayEnlace = !HayEnlace;
}
