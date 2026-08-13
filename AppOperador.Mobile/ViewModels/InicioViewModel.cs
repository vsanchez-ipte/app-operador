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
		IClock reloj,
		EstadoEnlaceViewModel enlace)
	{
		Enlace = enlace;
		_sesiones = sesiones;
		_conectividad = conectividad;
		_cola = cola;
		_reloj = reloj;
		_conectividad.EnlaceCambio += (_, _) => NotificarEstadoEnlace();

		Resumen = ResumenOperativo.Vacio;
		HoraActualizacion = string.Empty;
	}

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>Cuenta del operador que tiene la sesión abierta.</summary>
	public string Operador => _sesiones.Actual?.Operador ?? "-";

	/// <summary>
	/// Rol funcional con el que ingresó el operador (JTT-1386 CA 1).
	/// </summary>
	/// <remarks>
	/// Sale de la sesión, como el resto de lo que se muestra aquí (CA 9). Antes la pantalla
	/// llevaba un «Operador de campo» escrito en el XAML que parecía el rol y no lo era: quien
	/// ingresara con otro rol veía igualmente ese texto.
	/// </remarks>
	public string Rol => _sesiones.Actual?.Rol ?? "-";

	/// <summary>
	/// Unidad con la que el operador está trabajando (JTT-1381 CA 13).
	/// </summary>
	/// <remarks>
	/// Se muestra la <b>clave</b>, no el identificador técnico: al operador le sirve el número
	/// económico que lleva pintado la unidad, no el uuid que usa Jacob.
	/// </remarks>
	public string UnidadVehicular => _sesiones.Actual?.UnidadVehicular ?? "-";

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
		OnPropertyChanged(nameof(Rol));
		OnPropertyChanged(nameof(UnidadVehicular));
		OnPropertyChanged(nameof(KilometroActual));
	}

	// Los botones "Simular enlace" y "Simular fallo" de la maqueta son ayudas de
	// demostración para los desarrolladores, no funcionalidad del producto. No se
	// implementan aquí: obligarían al ViewModel a conocer un simulador concreto.

	private void NotificarEstadoEnlace() => OnPropertyChanged(nameof(HayEnlace));

	partial void OnResumenChanged(ResumenOperativo value) => OnPropertyChanged(nameof(KilometroActual));
}
