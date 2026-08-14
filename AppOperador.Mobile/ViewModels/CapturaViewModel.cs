using System.Collections.ObjectModel;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Captura de una incidencia de campo (JTT-280).
/// </summary>
/// <remarks>
/// El kilómetro se intenta obtener del GPS al abrir la pantalla; si no hay lectura
/// válida, queda habilitada la captura manual. La validación del formato la hace el
/// value object <see cref="Kilometer"/>, no este ViewModel.
/// </remarks>
public sealed partial class CapturaViewModel : ObservableObject
{
	// Textos fijados por JTT-280.
	private const string MensajeKilometroInvalido = "Capture un KM válido";
	private const string MensajeDescripcionRequerida = "Describa la incidencia de tipo Otro";
	private const string MensajeGpsNoDisponible = "No se pudo obtener el GPS. Capture el KM manualmente.";

	/// <summary>Mínimo de caracteres de la nota cuando el tipo es "Otro".</summary>
	private const int MinimoCaracteresOtro = 8;

	private readonly IIncidentRepository _incidencias;
	private readonly ILocationService _ubicacion;
	private readonly CapacidadesDeLaSesion _capacidades;

	[ObservableProperty]
	public partial TipoIncidencia? TipoSeleccionado { get; set; }

	[ObservableProperty]
	public partial string Kilometro { get; set; }

	[ObservableProperty]
	public partial Gravedad GravedadSeleccionada { get; set; }

	[ObservableProperty]
	public partial string Nota { get; set; }

	[ObservableProperty]
	public partial string? MensajeError { get; set; }

	[ObservableProperty]
	public partial string? AvisoGps { get; set; }

	/// <summary>
	/// Origen del kilómetro: GPS mientras la lectura sea válida, Manual en cuanto el
	/// operador lo escriba a mano.
	/// </summary>
	[ObservableProperty]
	public partial KilometerSource FuenteKilometro { get; set; }

	public CapturaViewModel(
		IIncidentRepository incidencias,
		ILocationService ubicacion,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace)
	{
		_incidencias = incidencias;
		_ubicacion = ubicacion;
		_capacidades = capacidades;
		Enlace = enlace;

		Kilometro = string.Empty;
		Nota = string.Empty;
		GravedadSeleccionada = Gravedad.Media;
		FuenteKilometro = KilometerSource.Manual;
	}

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>
	/// Catálogo de tipos de incidencia.
	/// </summary>
	/// <remarks>Observable por la misma razón que las unidades: se carga tras el enlace.</remarks>
	public ObservableCollection<TipoIncidencia> Tipos { get; } = [];

	/// <summary>Niveles de gravedad disponibles.</summary>
	public IReadOnlyList<Gravedad> Gravedades { get; } =
		[Gravedad.Baja, Gravedad.Media, Gravedad.Alta, Gravedad.Critica];

	/// <summary>Borradores guardados, listados bajo el formulario.</summary>
	public ObservableCollection<RegistroColaVista> Borradores { get; } = [];

	public bool HayError => !string.IsNullOrEmpty(MensajeError);

	public bool HayAvisoGps => !string.IsNullOrEmpty(AvisoGps);

	public bool HayBorradores => Borradores.Count > 0;

	/// <summary>Contador de caracteres de la nota, como en la maqueta.</summary>
	public string ContadorNota => $"{Nota.Length} car.";

	/// <summary>Carga catálogos e intenta situar al operador por GPS.</summary>
	public async Task InicializarAsync()
	{
		if (Tipos.Count == 0)
		{
			foreach (var tipo in await _incidencias.ObtenerTiposAsync())
			{
				Tipos.Add(tipo);
			}

			TipoSeleccionado = Tipos.FirstOrDefault();
		}

		// Lo que la sesión autoriza se reevalúa al entrar: pudo cerrarse o revocarse mientras la
		// pantalla no estaba a la vista (JTT-1385 CA 3).
		NotificarAutorizacion();

		await IntentarUbicarAsync();
		await RecargarBorradoresAsync();
	}

	/// <summary>
	/// Intenta completar el kilómetro con la lectura del GPS.
	/// </summary>
	/// <remarks>
	/// Si la posición no pertenece al corredor o no hay lectura válida, se avisa y queda
	/// la captura manual, tal como describe el flujo 5.3 del documento de arquitectura.
	/// </remarks>
	private async Task IntentarUbicarAsync()
	{
		var lectura = await _ubicacion.ObtenerKilometroAsync();
		if (lectura is null)
		{
			AvisoGps = MensajeGpsNoDisponible;
			FuenteKilometro = KilometerSource.Manual;
			return;
		}

		AvisoGps = null;
		Kilometro = lectura.Valor;
		FuenteKilometro = KilometerSource.GPS;
	}

	/// <summary>Indica si la sesión autoriza registrar incidencias (JTT-1385 CA 3 y 4).</summary>
	public bool PuedeRegistrar => _capacidades.Puede(CapacidadOperador.RegistrarIncidencia);

	/// <summary>Reevalúa lo que la sesión autoriza. La llaman la pantalla y el guardado.</summary>
	public void NotificarAutorizacion()
	{
		OnPropertyChanged(nameof(PuedeRegistrar));
		GuardarIncidenciaCommand.NotifyCanExecuteChanged();
		GuardarBorradorCommand.NotifyCanExecuteChanged();
	}

	/// <summary>
	/// Registra la incidencia en la cola local.
	/// </summary>
	/// <remarks>
	/// La comprobación se repite aunque el botón esté deshabilitado: deshabilitarlo es
	/// presentación, y la sesión puede cerrarse entre que la pantalla se pintó y alguien pulsa.
	/// La autorización se decide al ejecutar, no al dibujar (CA 4).
	/// </remarks>
	[RelayCommand(CanExecute = nameof(PuedeRegistrar))]
	private async Task GuardarIncidenciaAsync()
	{
		if (!_capacidades.Puede(CapacidadOperador.RegistrarIncidencia))
		{
			return;
		}

		if (TipoSeleccionado is null)
		{
			return;
		}

		// El value object es la única autoridad sobre el formato del kilómetro.
		if (!Kilometer.IntentarCrear(Kilometro, out var kilometro))
		{
			MensajeError = MensajeKilometroInvalido;
			return;
		}

		var nota = Nota.Trim();
		if (TipoSeleccionado.ExigeDescripcion && nota.Length < MinimoCaracteresOtro)
		{
			MensajeError = MensajeDescripcionRequerida;
			return;
		}

		MensajeError = null;
		await _incidencias.GuardarAsync(TipoSeleccionado, kilometro, FuenteKilometro, GravedadSeleccionada, nota);
		LimpiarFormulario();
		await RecargarBorradoresAsync();
	}

	[RelayCommand(CanExecute = nameof(PuedeRegistrar))]
	private async Task GuardarBorradorAsync()
	{
		// Un borrador es captura a medias, así que necesita la misma autorización que registrar.
		if (!_capacidades.Puede(CapacidadOperador.RegistrarIncidencia))
		{
			return;
		}

		// Un borrador se guarda tal cual esté: no se valida, porque su razón de ser es
		// permitir dejar la captura a medias sin perderla.
		MensajeError = null;
		await _incidencias.GuardarBorradorAsync(TipoSeleccionado, Kilometro, GravedadSeleccionada, Nota.Trim());
		LimpiarFormulario();
		await RecargarBorradoresAsync();
	}

	private async Task RecargarBorradoresAsync()
	{
		Borradores.Clear();
		foreach (var borrador in await _incidencias.ObtenerBorradoresAsync())
		{
			Borradores.Add(new RegistroColaVista(borrador));
		}

		OnPropertyChanged(nameof(HayBorradores));
	}

	private void LimpiarFormulario()
	{
		Nota = string.Empty;
		GravedadSeleccionada = Gravedad.Media;
	}

	partial void OnMensajeErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

	partial void OnAvisoGpsChanged(string? value) => OnPropertyChanged(nameof(HayAvisoGps));

	partial void OnNotaChanged(string value) => OnPropertyChanged(nameof(ContadorNota));

	// Escribir el kilómetro a mano cambia su origen: deja de ser una lectura del GPS.
	partial void OnKilometroChanged(string value)
	{
		if (FuenteKilometro == KilometerSource.GPS && !string.IsNullOrEmpty(AvisoGps))
		{
			FuenteKilometro = KilometerSource.Manual;
		}
	}
}
