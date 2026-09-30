using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppOperador.Mobile.ViewModels;

public sealed partial class InicioViewModel : ObservableObject
{
	// Lo que muestra la tarjeta «KM actual» mientras el GPS fija y cuando no hay lectura útil.
	private const string LeyendoKilometro = "…";
	private const string SinKilometro = "-";

	private readonly ISessionStore _sesiones;
	private readonly IConnectivityService _conectividad;
	private readonly ISyncQueueService _cola;
	private readonly IClock _reloj;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly ObtenerKilometroPorUbicacion _kilometro;

	// Una lectura por vez: la pantalla reaparece más rápido de lo que el GPS fija posición.
	private bool _leyendoKilometro;

	[ObservableProperty]
	public partial ResumenOperativo Resumen { get; set; }

	[ObservableProperty]
	public partial string HoraActualizacion { get; set; }

	[ObservableProperty]
	public partial string KilometroActual { get; set; }

	public InicioViewModel(
		ISessionStore sesiones,
		IConnectivityService conectividad,
		ISyncQueueService cola,
		IClock reloj,
		CapacidadesDeLaSesion capacidades,
		ObtenerKilometroPorUbicacion kilometro,
		EstadoEnlaceViewModel enlace)
	{
		Enlace = enlace;
		_sesiones = sesiones;
		_conectividad = conectividad;
		_cola = cola;
		_reloj = reloj;
		_capacidades = capacidades;
		_kilometro = kilometro;
		_conectividad.EnlaceCambio += (_, _) => NotificarEstadoEnlace();

		Resumen = ResumenOperativo.Vacio;
		HoraActualizacion = string.Empty;
		KilometroActual = SinKilometro;
	}

	public EstadoEnlaceViewModel Enlace { get; }

	public string Operador => _sesiones.Actual?.Operador ?? "-";

	public string Rol => _sesiones.Actual?.Rol ?? "-";

	// La clave, que es el número económico pintado en la unidad, no el id técnico.
	public string UnidadVehicular => _sesiones.Actual?.UnidadVehicular ?? "-";

	// La insignia «CAPTURA» solo aparece si la sesión permite capturar.
	public bool PuedeCapturar => _capacidades.Puede(CapacidadOperador.RegistrarIncidencia);

	public bool HayEnlace => _conectividad.HayEnlace;

	public async Task ActualizarAsync()
	{
		var pendientes = await _cola.ContarPendientesAsync();
		var registros = await _cola.ObtenerRegistrosAsync();

		Resumen = new ResumenOperativo(
			PendientesSincronizar: pendientes,
			Avisos: 0,
			EvidenciaLocal: registros.Count(r => r.Clase == ClaseRegistro.Evidencia));

		// Sin esperar: el GPS tarda hasta diez segundos y los contadores no deben aguardarlo.
		_ = LeerKilometroAsync();

		// Se guarda en UTC; al operador se le muestra su hora local.
		HoraActualizacion = _reloj.UtcAhora.ToLocalTime().ToString("HH:mm:ss");
		OnPropertyChanged(nameof(Operador));
		OnPropertyChanged(nameof(Rol));
		OnPropertyChanged(nameof(UnidadVehicular));
		// Las capacidades llegan con la sesión, así que se refrescan con la identidad.
		OnPropertyChanged(nameof(PuedeCapturar));
	}

	// Lectura del GPS de este momento, no el KM de la última incidencia: «KM actual» promete dónde está.
	private async Task LeerKilometroAsync()
	{
		if (_leyendoKilometro)
		{
			return;
		}

		_leyendoKilometro = true;
		KilometroActual = LeyendoKilometro;
		try
		{
			var resultado = await _kilometro.EjecutarAsync();
			KilometroActual = resultado.HayKilometro ? resultado.Kilometro!.Valor : SinKilometro;
		}
		catch (Exception)
		{
			// Nadie espera esta tarea: una excepción aquí cerraría la app.
			KilometroActual = SinKilometro;
		}
		finally
		{
			_leyendoKilometro = false;
		}
	}

	private void NotificarEstadoEnlace() => OnPropertyChanged(nameof(HayEnlace));
}
