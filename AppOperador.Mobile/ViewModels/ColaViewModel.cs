using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Cola local de registros pendientes de sincronizar (JTT-290 y JTT-291).
/// </summary>
public sealed partial class ColaViewModel : ObservableObject
{
	private readonly ISyncQueueService _cola;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly CapacidadesDeLaSesion _capacidades;

	[ObservableProperty]
	public partial int Pendientes { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	/// <summary>
	/// Qué pasó en la última sincronización, o <see langword="null"/> si no se ha pulsado
	/// (JTT-1401 CA 10).
	/// </summary>
	/// <remarks>
	/// <b>Sin esto, pulsar «Sincronizar» sin enlace no produce ningún cambio visible</b> y el
	/// operador no puede distinguir un botón que no respondió de una cola que no tenía nada que
	/// enviar. El criterio pide que la app muestre el motivo.
	/// </remarks>
	[ObservableProperty]
	public partial string? MensajeSincronizacion { get; set; }

	public ColaViewModel(
		ISyncQueueService cola,
		ISincronizadorIncidencias sincronizador,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace)
	{
		_cola = cola;
		_sincronizador = sincronizador;
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

	/// <summary>Indica si hay algo que decir sobre la última sincronización.</summary>
	public bool HayMensajeSincronizacion => !string.IsNullOrEmpty(MensajeSincronizacion);

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
			var resultado = await _sincronizador.EjecutarAsync();
			MensajeSincronizacion = TextoDe(resultado);
			await ActualizarAsync();
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>
	/// Traduce el resultado de la sincronización al aviso que lee el operador (CA 10).
	/// </summary>
	/// <remarks>
	/// Los literales son provisionales: Producto no ha fijado los de esta pantalla. Lo que no es
	/// provisional es que <b>cada motivo diga algo distinto</b>: «no se pudo sincronizar» deja al
	/// operador sin saber si esperar, buscar señal o llamar al CCO.
	/// </remarks>
	private static string TextoDe(ResultadoSincronizacion resultado) => resultado.MotivoBloqueo switch
	{
		MotivoNoSincroniza.SinPermiso =>
			"Su cuenta no tiene autorizado sincronizar. Solicite el acceso al CCO.",
		MotivoNoSincroniza.SinEnlaceConJacob =>
			"Sin enlace con el CCO. Lo capturado se conserva y se enviará al recuperar la señal.",
		MotivoNoSincroniza.SinSesion =>
			"La sesión expiró. Vuelva a ingresar para sincronizar.",
		_ when resultado.Intentados == 0 =>
			"No hay incidencias pendientes de enviar.",
		_ when resultado.Confirmados == resultado.Intentados =>
			$"{resultado.Confirmados} incidencias enviadas al CCO.",
		// Ninguna salió y el fallo fue del camino: Jacob no llegó a evaluarlas. Decir que las
		// rechazó mandaría al operador a revisar capturas que están bien.
		_ when resultado.Confirmados == 0
			&& resultado.FamiliaUltimoError == FamiliaErrorSincronizacion.Tecnico =>
			"No se pudo contactar al CCO. Lo pendiente se conserva y se reintentará.",

		// Ninguna salió y Jacob las rechazó: se muestra SU mensaje, que dice qué corregir.
		_ when resultado.Confirmados == 0 && resultado.MensajeUltimoError is { } motivo =>
			$"El CCO rechazó el envío: {motivo}",

		// Que unas salgan y otras no es lo normal, no un fallo: el CA 13 pide justamente que
		// una falla no detenga a las demás. Se dice el reparto en vez de un «error» a secas.
		_ => $"{resultado.Confirmados} de {resultado.Intentados} enviadas. El resto sigue pendiente.",
	};

	/// <summary>Reevalúa lo que la sesión autoriza. La sesión puede haber cambiado.</summary>
	private void NotificarAutorizacion()
	{
		OnPropertyChanged(nameof(PuedeConsultar));
		OnPropertyChanged(nameof(PuedeSincronizar));
		SincronizarCommand.NotifyCanExecuteChanged();
	}

	partial void OnPendientesChanged(int value) => OnPropertyChanged(nameof(TextoPendientes));

	partial void OnMensajeSincronizacionChanged(string? value) =>
		OnPropertyChanged(nameof(HayMensajeSincronizacion));

	partial void OnOcupadoChanged(bool value) => NotificarAutorizacion();
}
