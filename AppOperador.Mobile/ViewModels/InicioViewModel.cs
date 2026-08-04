using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Tablero de Inicio: identidad, estado de enlace y los cuatro indicadores operativos.
/// </summary>
/// <remarks>
/// Aquí no se captura nada: es solo lectura del estado local.
/// </remarks>
public sealed partial class InicioViewModel : ObservableObject
{
	private readonly ISessionStore _sesiones;
	private readonly IConnectivityService _conectividad;
	private readonly ISyncQueueService _cola;
	private readonly IClock _reloj;

	[ObservableProperty]
	public partial ResumenOperativo Resumen { get; set; }

	[ObservableProperty]
	public partial string HoraActualizacion { get; set; }

	public InicioViewModel(
		ISessionStore sesiones,
		IConnectivityService conectividad,
		ISyncQueueService cola,
		IClock reloj)
	{
		_sesiones = sesiones;
		_conectividad = conectividad;
		_cola = cola;
		_reloj = reloj;
		_conectividad.EnlaceCambio += (_, _) => NotificarEstadoEnlace();

		Resumen = ResumenOperativo.Vacio;
		HoraActualizacion = string.Empty;
	}

	/// <summary>Cuenta del operador que tiene la sesión abierta.</summary>
	public string Operador => _sesiones.Actual?.Operador ?? "-";

	/// <summary>Etiqueta de enlace: verde cuando hay comunicación con Jacob CCO.</summary>
	public string TextoEnlace => _conectividad.HayEnlace ? "ENLACE" : "OFFLINE";

	public bool HayEnlace => _conectividad.HayEnlace;

	/// <summary>Kilómetro conocido, o un guion cuando no hay lectura.</summary>
	public string KilometroActual => Resumen.KilometroActual ?? "-";

	/// <summary>Refresca los indicadores del tablero.</summary>
	public async Task ActualizarAsync()
	{
		var pendientes = await _cola.ContarPendientesAsync();
		var registros = await _cola.ObtenerRegistrosAsync();

		Resumen = new ResumenOperativo(
			PendientesSincronizar: pendientes,
			KilometroActual: registros.FirstOrDefault()?.Kilometro,
			Avisos: 0,
			EvidenciaLocal: registros.Count(r => r.Clase == ClaseRegistro.Evidencia));

		// La comparación es en UTC; al operador se le presenta su hora local (DA-10).
		HoraActualizacion = _reloj.UtcAhora.ToLocalTime().ToString("HH:mm:ss");
		OnPropertyChanged(nameof(Operador));
		OnPropertyChanged(nameof(KilometroActual));
	}

	// Los botones "Simular enlace" y "Simular fallo" de la maqueta son ayudas de
	// demostración para los desarrolladores, no funcionalidad del producto. No se
	// implementan aquí: obligarían al ViewModel a conocer un simulador concreto.

	private void NotificarEstadoEnlace()
	{
		OnPropertyChanged(nameof(TextoEnlace));
		OnPropertyChanged(nameof(HayEnlace));
	}

	partial void OnResumenChanged(ResumenOperativo value) => OnPropertyChanged(nameof(KilometroActual));
}
