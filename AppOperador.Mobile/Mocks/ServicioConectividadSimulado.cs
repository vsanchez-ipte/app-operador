using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Estado de enlace simulado, conmutable a mano desde la interfaz.
/// </summary>
/// <remarks>
/// Solo se registra con el canal real apagado, para poder demostrar las pantallas sin
/// servidor. Responde que siempre hay enlace y <see cref="Alternar"/> permite forzar el
/// modo offline a mano. El estado real lo determina
/// <c>ServicioConectividadJacob</c> (JTT-1391).
/// </remarks>
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

	/// <summary>
	/// Comprobación simulada: devuelve lo que ya se tenga, sin consultar nada.
	/// </summary>
	/// <remarks>
	/// El modo offline se fuerza a mano con <see cref="Alternar"/>, así que aquí la falta de
	/// enlace es siempre de transporte: no hay servidor que pueda contestar un error.
	/// </remarks>
	public Task<ResultadoSondeo> ComprobarAsync(CancellationToken cancelacion = default) =>
		Task.FromResult(HayEnlace
			? ResultadoSondeo.Alcanzado()
			: ResultadoSondeo.SinTransporte("Modo offline forzado desde el simulador."));

	/// <summary>Alterna el estado de enlace. Solo existe mientras trabajamos con simuladores.</summary>
	public void Alternar() => HayEnlace = !HayEnlace;
}
