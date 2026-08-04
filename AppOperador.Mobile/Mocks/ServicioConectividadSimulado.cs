using AppOperador.Aplicacion.Interfaces;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Estado de enlace simulado, conmutable a mano desde la interfaz.
/// </summary>
/// <remarks>
/// La maqueta trae un botón "Simular enlace" con el que el desarrollador alterna entre
/// tener y no tener comunicación con Jacob CCO. Ese botón es de la maqueta, no del
/// producto: cuando exista el canal móvil real, esta clase se reemplaza por una que
/// consulte <c>Connectivity.Current</c> y verifique el alcance del API.
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

	/// <summary>Alterna el estado de enlace. Solo existe mientras trabajamos con simuladores.</summary>
	public void Alternar() => HayEnlace = !HayEnlace;
}
