using System.Collections.ObjectModel;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Cola local de registros pendientes de sincronizar (JTT-290 y JTT-291).
/// </summary>
public sealed partial class ColaViewModel : ObservableObject
{
	private readonly ISyncQueueService _cola;
	private readonly CapacidadesDeLaSesion _capacidades;

	[ObservableProperty]
	public partial int Pendientes { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	public ColaViewModel(
		ISyncQueueService cola,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace)
	{
		_cola = cola;
		_capacidades = capacidades;
		Enlace = enlace;
	}

	/// <summary>Indica si la sesión autoriza ver la cola (JTT-1385 CA 3 y 4).</summary>
	public bool PuedeConsultar => _capacidades.Puede(CapacidadOperador.ConsultarCola);

	/// <summary>Indica si la sesión autoriza sincronizar y no hay un envío en curso.</summary>
	public bool PuedeSincronizar =>
		!Ocupado && _capacidades.Puede(CapacidadOperador.Sincronizar);

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>Registros de la cola, listos para mostrarse. Los borradores no aparecen aquí.</summary>
	public ObservableCollection<RegistroColaVista> Registros { get; } = [];

	/// <summary>Resumen que encabeza la pantalla.</summary>
	public string TextoPendientes => $"{Pendientes} incidencias pendientes de sincronizar.";

	public bool HayRegistros => Registros.Count > 0;

	/// <summary>
	/// Recarga la cola desde el almacenamiento local.
	/// </summary>
	/// <remarks>
	/// Sin autorización no se lee nada y la pantalla queda vacía: los registros llevan datos de
	/// operación y no deben quedar a la vista de una sesión que ya no vale (CA 4).
	/// </remarks>
	public async Task ActualizarAsync()
	{
		Registros.Clear();

		if (!PuedeConsultar)
		{
			Pendientes = 0;
			NotificarAutorizacion();
			return;
		}

		foreach (var registro in await _cola.ObtenerRegistrosAsync())
		{
			Registros.Add(new RegistroColaVista(registro));
		}

		Pendientes = await _cola.ContarPendientesAsync();
		OnPropertyChanged(nameof(HayRegistros));
		NotificarAutorizacion();
	}

	/// <summary>
	/// Envía los pendientes a Jacob CCO.
	/// </summary>
	/// <remarks>
	/// La comprobación se repite aquí aunque el botón ya esté deshabilitado: ocultar el control
	/// es presentación, y la sesión puede cerrarse entre que la pantalla se pintó y alguien
	/// pulsa. La autorización se decide al ejecutar, no al dibujar.
	/// </remarks>
	[RelayCommand(CanExecute = nameof(PuedeSincronizar))]
	private async Task SincronizarAsync()
	{
		if (!_capacidades.Puede(CapacidadOperador.Sincronizar))
		{
			return;
		}

		Ocupado = true;
		try
		{
			await _cola.SincronizarAsync();
			await ActualizarAsync();
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>Reevalúa lo que la sesión autoriza. La sesión puede haber cambiado.</summary>
	private void NotificarAutorizacion()
	{
		OnPropertyChanged(nameof(PuedeConsultar));
		OnPropertyChanged(nameof(PuedeSincronizar));
		SincronizarCommand.NotifyCanExecuteChanged();
	}

	partial void OnPendientesChanged(int value) => OnPropertyChanged(nameof(TextoPendientes));

	partial void OnOcupadoChanged(bool value) => NotificarAutorizacion();
}
