using System.Collections.ObjectModel;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Cola local de registros pendientes de sincronizar (JTT-290 y JTT-291).
/// </summary>
public sealed partial class ColaViewModel : ObservableObject
{
	private readonly ISyncQueueService _cola;

	[ObservableProperty]
	public partial int Pendientes { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	public ColaViewModel(ISyncQueueService cola) => _cola = cola;

	/// <summary>Registros de la cola, listos para mostrarse. Los borradores no aparecen aquí.</summary>
	public ObservableCollection<RegistroColaVista> Registros { get; } = [];

	/// <summary>Resumen que encabeza la pantalla.</summary>
	public string TextoPendientes => $"{Pendientes} incidencias pendientes de sincronizar.";

	public bool HayRegistros => Registros.Count > 0;

	/// <summary>Recarga la cola desde el almacenamiento local.</summary>
	public async Task ActualizarAsync()
	{
		Registros.Clear();
		foreach (var registro in await _cola.ObtenerRegistrosAsync())
		{
			Registros.Add(new RegistroColaVista(registro));
		}

		Pendientes = await _cola.ContarPendientesAsync();
		OnPropertyChanged(nameof(HayRegistros));
	}

	[RelayCommand]
	private async Task SincronizarAsync()
	{
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

	partial void OnPendientesChanged(int value) => OnPropertyChanged(nameof(TextoPendientes));
}
