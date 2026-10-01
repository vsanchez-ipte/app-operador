using System.Collections.ObjectModel;
using System.Diagnostics;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using AppOperador.Domain.Enums;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

public sealed partial class CapturaViewModel : ObservableObject, IQueryAttributable
{
	private const string MensajeKilometroInvalido = "Capture un KM válido";
	// El aviso nombra al tipo seleccionado: qué tipos exigen descripción lo decide el catálogo.
	private const string FormatoDescripcionRequerida = "Describa la incidencia de tipo {0}";

	private const string MensajeSinCatalogo =
		"Aún no se ha descargado el catálogo. Conéctese una vez para poder registrar incidencias.";
	private const string MensajeGpsNoDisponible =
		"No está disponible la ubicación del dispositivo. Capture el KM manualmente.";
	private const string MensajeGpsSinPermiso =
		"La app no tiene permiso para usar la ubicación. Capture el KM manualmente.";
	private const string MensajeGpsSinPrecision =
		"La señal GPS no tiene precisión suficiente. Reintente o capture el KM manualmente.";
	private const string MensajeFueraDelCorredor =
		"La ubicación está fuera del corredor. Capture el KM manualmente.";
	private const string MensajeTramoSinGeometria =
		"Este tramo todavía no tiene geometría disponible. Capture el KM manualmente.";
	private const string MensajeErrorGps =
		"No se pudo calcular el KM con la ubicación. Reintente o captúrelo manualmente.";

	private const string MensajeSinPermisoCaptura =
		"Su cuenta no tiene autorizado registrar incidencias. Solicite el acceso al CCO y vuelva a ingresar.";

	private const string MensajeBorradorSinTipo = "Elija el tipo de incidencia para poder registrarla";
	private const string MensajeBorradorSinSeveridad = "Elija la severidad para poder registrarla";
	private const string MensajeBorradorNoEncontrado = "El borrador ya no está disponible";
	private const string MensajeRechazadaNoEncontrada =
		"El registro ya no está en Fallido: pudo salir en la última sincronización";

	private readonly IIncidentRepository _incidencias;
	private readonly ICatalogoRepository _catalogo;
	private readonly ConvertirBorradorEnIncidencia _convertirBorrador;
	private readonly CorregirIncidenciaRechazada _corregirRechazada;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly ObtenerKilometroPorUbicacion _obtenerKilometro;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly ISelectorEvidencia _selectorEvidencia;
	private readonly IVisorEvidencia _visorEvidencia;
	private readonly AdjuntarEvidencia _adjuntarEvidencia;
	private readonly QuitarEvidencia _quitarEvidencia;
	private readonly ObtenerEvidenciasDeIncidencia _obtenerEvidencias;
	private readonly EliminarBorrador _eliminarBorrador;

	[ObservableProperty]
	public partial TipoIncidencia? TipoSeleccionado { get; set; }

	[ObservableProperty]
	public partial string Kilometro { get; set; }

	[ObservableProperty]
	public partial SeveridadIncidencia? SeveridadSeleccionada { get; set; }

	[ObservableProperty]
	public partial string Nota { get; set; }

	[ObservableProperty]
	public partial string? MensajeError { get; set; }

	// Un error debajo de cada campo: un aviso al pie del formulario no dice dónde mirar.
	[ObservableProperty]
	public partial string? ErrorTipo { get; set; }

	[ObservableProperty]
	public partial string? ErrorKilometro { get; set; }

	[ObservableProperty]
	public partial string? ErrorSeveridad { get; set; }

	[ObservableProperty]
	public partial string? ErrorNota { get; set; }

	// Evento y no llamada a la vista: la página decide cómo desplazarse hasta el campo.
	public event EventHandler<CampoCaptura>? CampoConError;

	// Aparte de Ocupado: es la carga al entrar, no un botón trabajando.
	[ObservableProperty]
	public partial bool Cargando { get; set; }

	public CapturaViewModel(
		IIncidentRepository incidencias,
		ICatalogoRepository catalogo,
		ObtenerKilometroPorUbicacion obtenerKilometro,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace,
		ConvertirBorradorEnIncidencia convertirBorrador,
		CorregirIncidenciaRechazada corregirRechazada,
		ISincronizadorIncidencias sincronizador,
		ISelectorEvidencia selectorEvidencia,
		IVisorEvidencia visorEvidencia,
		AdjuntarEvidencia adjuntarEvidencia,
		QuitarEvidencia quitarEvidencia,
		ObtenerEvidenciasDeIncidencia obtenerEvidencias,
		EliminarBorrador eliminarBorrador)
	{
		_incidencias = incidencias;
		_catalogo = catalogo;
		_convertirBorrador = convertirBorrador;
		_corregirRechazada = corregirRechazada;
		_sincronizador = sincronizador;
		_obtenerKilometro = obtenerKilometro;
		_capacidades = capacidades;
		_selectorEvidencia = selectorEvidencia;
		_visorEvidencia = visorEvidencia;
		_adjuntarEvidencia = adjuntarEvidencia;
		_quitarEvidencia = quitarEvidencia;
		_obtenerEvidencias = obtenerEvidencias;
		_eliminarBorrador = eliminarBorrador;
		Enlace = enlace;

		Kilometro = string.Empty;
		Nota = string.Empty;
		FuenteKilometro = KilometerSource.Manual;
	}

	public EstadoEnlaceViewModel Enlace { get; }

	public ObservableCollection<TipoIncidencia> Tipos { get; } = [];

	public ObservableCollection<SeveridadIncidencia> Severidades { get; } = [];

	public bool HayError => !string.IsNullOrEmpty(MensajeError);

	public bool HayErrorTipo => !string.IsNullOrEmpty(ErrorTipo);

	public bool HayErrorKilometro => !string.IsNullOrEmpty(ErrorKilometro);

	public bool HayErrorSeveridad => !string.IsNullOrEmpty(ErrorSeveridad);

	public bool HayErrorNota => !string.IsNullOrEmpty(ErrorNota);

	public int LongitudMaximaNota => ReglaNotaIncidencia.MaximoCaracteres;

	public string ContadorNota => $"{Nota.Length}/{LongitudMaximaNota} car.";

	public async Task InicializarAsync()
	{
		// Solo lo local va bajo la capa de carga; el GPS se pide después para no tapar la pantalla mientras tarda.
		var claveACorregir = _claveACorregir;
		_claveACorregir = null;

		Cargando = true;
		try
		{
			await InicializarCargandoAsync();

			// Lo que llega de la Cola se repone antes de destapar la pantalla; su kilómetro es manual.
			if (claveACorregir is not null)
			{
				await AbrirRechazadaAsync(claveACorregir);
				return;
			}
		}
		finally
		{
			Cargando = false;
		}

		RecalcularUbicacionSinEsperar();
	}

	private async Task InicializarCargandoAsync()
	{
		await CargarCatalogoAsync();

		// Al entrar: la sesión pudo cerrarse o revocarse mientras la pantalla no estaba a la vista.
		NotificarAutorizacion();

		await RecargarBorradoresAsync();

		// Aunque no haya registro: los límites deciden si el botón de adjuntar va encendido.
		await RecargarEvidenciasAsync();
	}

	// Copia local en cada entrada: permite capturar sin conexión y recoge un catálogo recién descargado.
	private async Task CargarCatalogoAsync()
	{
		var catalogos = await _catalogo.ObtenerAsync();

		var tipoElegido = TipoSeleccionado?.Id;
		var severidadElegida = SeveridadSeleccionada?.Id;

		Tipos.Clear();
		foreach (var tipo in catalogos.Tipos)
		{
			Tipos.Add(tipo);
		}

		Severidades.Clear();
		foreach (var severidad in catalogos.Severidades)
		{
			Severidades.Add(severidad);
		}

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == tipoElegido) ?? Tipos.FirstOrDefault();
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == severidadElegida)
			?? Severidades.FirstOrDefault();

		OnPropertyChanged(nameof(HayCatalogo));
		OnPropertyChanged(nameof(AvisoSinCatalogo));
		OnPropertyChanged(nameof(HayAvisoSinCatalogo));

		// PuedeRegistrar depende también del catálogo, así que los botones se reevalúan aquí.
		NotificarAutorizacion();
	}

	public bool HayCatalogo => Tipos.Count > 0 && Severidades.Count > 0;

	public string? AvisoSinCatalogo => HayCatalogo ? null : MensajeSinCatalogo;

	public bool HayAvisoSinCatalogo => !HayCatalogo;

	public bool PuedeRegistrar => TienePermisoDeCaptura && HayCatalogo;

	// Aparte de PuedeRegistrar: a quien le falta el catálogo no se le dice que su cuenta no está autorizada.
	public bool TienePermisoDeCaptura => _capacidades.Puede(CapacidadOperador.RegistrarIncidencia);

	public string? AvisoSinPermiso => TienePermisoDeCaptura ? null : MensajeSinPermisoCaptura;

	public bool HayAvisoSinPermiso => !TienePermisoDeCaptura;

	// Notifica también el aviso: un permiso revocado no debe apagar los botones sin decir por qué.
	public void NotificarAutorizacion()
	{
		OnPropertyChanged(nameof(TienePermisoDeCaptura));
		OnPropertyChanged(nameof(PuedeRegistrar));
		OnPropertyChanged(nameof(AvisoSinPermiso));
		OnPropertyChanged(nameof(HayAvisoSinPermiso));
		GuardarIncidenciaCommand.NotifyCanExecuteChanged();
		GuardarBorradorCommand.NotifyCanExecuteChanged();
	}

	// Se comprueba otra vez aunque el botón esté apagado: la sesión puede cerrarse antes del toque.
	[RelayCommand(CanExecute = nameof(PuedeRegistrar))]
	private async Task GuardarIncidenciaAsync()
	{
		if (!_capacidades.Puede(CapacidadOperador.RegistrarIncidencia))
		{
			// Revocado entre el pintado y el toque: se refresca para que aparezca el aviso.
			NotificarAutorizacion();
			return;
		}

		// Con un borrador abierto se convierte ese borrador; crear otro duplicaría el registro.
		if (BorradorEnEdicion is { } claveEnEdicion)
		{
			await ConvertirBorradorAbiertoAsync(claveEnEdicion);
			return;
		}

		// Con un rechazado abierto se reenvía ese registro corregido.
		if (RechazadaEnCorreccion is { } claveRechazada)
		{
			await ReenviarRechazadaAbiertaAsync(claveRechazada);
			return;
		}

		if (SeveridadSeleccionada is null)
		{
			// No debería llegar aquí: sin severidades no hay catálogo y el botón está apagado.
			MensajeError = MensajeSinCatalogo;
			return;
		}

		if (TipoSeleccionado is null)
		{
			// Tras limpiar el formulario, un segundo toque llega aquí: se avisa en vez de callar.
			SenalarError(CampoCaptura.Tipo, MensajeBorradorSinTipo);
			return;
		}

		// El value object es la única autoridad sobre el formato del kilómetro.
		if (!Kilometer.IntentarCrear(Kilometro, out var kilometro))
		{
			SenalarError(CampoCaptura.Kilometro, MensajeKilometroInvalido);
			return;
		}

		var nota = Nota.Trim();
		if (!ReglaNotaIncidencia.EsSuficiente(TipoSeleccionado.ExigeDescripcion, nota))
		{
			SenalarError(CampoCaptura.Nota, MensajeDescripcionRequerida());
			return;
		}

		MensajeError = null;
		LimpiarErroresDeCampo();
		var clave = await _incidencias.GuardarAsync(
			TipoSeleccionado,
			kilometro,
			FuenteKilometro,
			SeveridadSeleccionada,
			nota,
			posicionGps: FuenteKilometro == KilometerSource.GPS ? _posicionGps : null);

		LimpiarFormulario();
		await RecargarBorradoresAsync();
		await IntentarEnviarRecienGuardadaAsync(clave);

		// Sin esperar: la lectura tarda hasta diez segundos y el aviso del envío no tiene por qué esperarla.
		RecalcularUbicacionSinEsperar();
	}

	private string MensajeDescripcionRequerida() =>
		string.Format(FormatoDescripcionRequerida, TipoSeleccionado?.Nombre ?? "seleccionado");

	// El botón dice lo que va a hacer: con un borrador abierto no se crea uno nuevo.
	public string TextoBotonPrimario => EstaCorrigiendo
		? "Reenviar corregida"
		: EstaEditandoBorrador ? "Convertir en incidencia" : "Guardar incidencia";

	// También tipo y kilómetro: si se quedaran, otro toque registraría el mismo hecho dos veces.
	private void LimpiarFormulario()
	{
		TipoSeleccionado = null;
		Kilometro = string.Empty;
		Nota = string.Empty;
		SeveridadSeleccionada = Severidades.FirstOrDefault();
		MensajeEnvio = null;
		LimpiarErroresDeCampo();
		SoltarEvidencias();
	}

	partial void OnMensajeErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

	partial void OnNotaChanged(string value)
	{
		OnPropertyChanged(nameof(ContadorNota));
		ErrorNota = null;
	}

	partial void OnTipoSeleccionadoChanged(TipoIncidencia? value) => ErrorTipo = null;

	partial void OnSeveridadSeleccionadaChanged(SeveridadIncidencia? value) => ErrorSeveridad = null;

	partial void OnErrorTipoChanged(string? value) => OnPropertyChanged(nameof(HayErrorTipo));

	partial void OnErrorKilometroChanged(string? value) => OnPropertyChanged(nameof(HayErrorKilometro));

	partial void OnErrorSeveridadChanged(string? value) => OnPropertyChanged(nameof(HayErrorSeveridad));

	partial void OnErrorNotaChanged(string? value) => OnPropertyChanged(nameof(HayErrorNota));

	private void SenalarError(CampoCaptura? campo, string mensaje)
	{
		LimpiarErroresDeCampo();

		switch (campo)
		{
			case CampoCaptura.Tipo:
				ErrorTipo = mensaje;
				break;
			case CampoCaptura.Kilometro:
				ErrorKilometro = mensaje;
				break;
			case CampoCaptura.Severidad:
				ErrorSeveridad = mensaje;
				break;
			case CampoCaptura.Nota:
				ErrorNota = mensaje;
				break;
			default:
				// Sin campo propio —el borrador o el rechazado ya no existen— va al aviso general.
				MensajeError = mensaje;
				return;
		}

		MensajeError = null;
		CampoConError?.Invoke(this, campo.Value);
	}

	private void LimpiarErroresDeCampo()
	{
		ErrorTipo = null;
		ErrorKilometro = null;
		ErrorSeveridad = null;
		ErrorNota = null;
	}
}
