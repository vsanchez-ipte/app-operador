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

public sealed partial class CapturaViewModel
{
	// Adjuntar antes de guardar crea un borrador: así la evidencia no se pierde si la app se cierra.
	private string? _uuidParaEvidencias;
	private PosicionDispositivo? _posicionGps;

	// No borra nada: las evidencias siguen atadas a su registro y vuelven al abrirlo.
	private void SoltarEvidencias()
	{
		_uuidParaEvidencias = null;
		Evidencias.Clear();
		ResumenEvidencias = ResumenEvidencias with { Adjuntas = [] };

		OnPropertyChanged(nameof(PuedeAdjuntar));
		OnPropertyChanged(nameof(PuedeAdjuntarVideo));
		OnPropertyChanged(nameof(AvisoVideo));
		OnPropertyChanged(nameof(HayAvisoVideo));
		OnPropertyChanged(nameof(ContadorEvidencias));
		OnPropertyChanged(nameof(HayEvidencias));
	}

	public ObservableCollection<EvidenciaVista> Evidencias { get; } = [];

	[ObservableProperty]
	public partial ResumenEvidencias ResumenEvidencias { get; set; } = ResumenEvidencias.Vacio;

	// Adjuntar es capturar: exige el mismo permiso que registrar.
	public bool PuedeAdjuntar => TienePermisoDeCaptura && ResumenEvidencias.PuedeAdjuntar;

	// Se enciende solo cuando el catálogo publique un formato de video.
	public bool PuedeAdjuntarVideo => TienePermisoDeCaptura && ResumenEvidencias.PuedeAdjuntarVideo;

	public bool HayCamara => _selectorEvidencia.Disponible(OrigenEvidencia.Camara);

	// Junto al botón: un botón visible y muerto se lee como que la app está rota.
	public string AvisoVideo => !ResumenEvidencias.Limites.EstanDefinidos
		? string.Empty
		: !ResumenEvidencias.Limites.AdmiteVideo
			? "El servidor todavía no admite video."
			: !ResumenEvidencias.HayEspacioParaVideo
				// Se bloquea el video y se dice con qué se puede seguir.
				? "No hay espacio en el dispositivo para un video. Puede continuar con texto o fotografía."
				// Dice el tope sin prometer que la grabación se detendrá: hay cámaras que ignoran la petición.
				: $"El video no debe pasar de {ResumenEvidencias.Limites.TamanoMaximoMb} MB.";

	public bool HayAvisoVideo => AvisoVideo.Length > 0;

	// Combinado, como lo pidió el PO: sin separar fotos de videos.
	public string ContadorEvidencias =>
		ResumenEvidencias.Limites.EstanDefinidos
			? $"{ResumenEvidencias.Cuantas}/{ResumenEvidencias.Limites.MaximoArchivosPorIncidencia} archivos"
			: "Sin conexión previa: no se puede adjuntar todavía";

	public bool HayEvidencias => Evidencias.Count > 0;

	[RelayCommand]
	private Task AdjuntarDeCamaraAsync() => AdjuntarAsync(OrigenEvidencia.Camara);

	[RelayCommand]
	private Task AdjuntarDeGaleriaAsync() => AdjuntarAsync(OrigenEvidencia.Galeria);

	[RelayCommand]
	private Task AdjuntarDeVideoAsync() => AdjuntarAsync(OrigenEvidencia.Video);

	// Es por donde entra un PDF.
	[RelayCommand]
	private Task AdjuntarDeArchivoAsync() => AdjuntarAsync(OrigenEvidencia.Archivo);

	private async Task AdjuntarAsync(OrigenEvidencia origen)
	{
		if (!_capacidades.Puede(CapacidadOperador.AdjuntarEvidencia))
		{
			NotificarAutorizacion();
			return;
		}

		// El video exige además que el servidor lo admita.
		var puede = origen is OrigenEvidencia.Video
			? ResumenEvidencias.PuedeAdjuntarVideo
			: ResumenEvidencias.PuedeAdjuntar;

		if (!puede)
		{
			MensajeError = MensajeDe(origen is OrigenEvidencia.Video
				? ResumenEvidencias.MotivoParaNoAdjuntarVideo
				: ResumenEvidencias.MotivoParaNoAdjuntar);
			CampoConError?.Invoke(this, CampoCaptura.Evidencia);
			return;
		}

		// El tope solo viaja al grabar: los demás orígenes entregan algo que ya existe.
		var topeBytes = origen is OrigenEvidencia.Video
			? ResumenEvidencias.TopeParaGrabarVideo
			: 0;

		var seleccion = await _selectorEvidencia.ElegirAsync(origen, topeBytes);

		if (seleccion.Archivos.Count == 0)
		{
			// Cancelar y la primera negativa van en silencio; lo demás se avisa para no dejar un botón mudo.
			MensajeError = seleccion.Desenlace switch
			{
				DesenlaceSeleccion.NoSePudoAbrir => MensajeNoSePudoAbrir(origen),
				DesenlaceSeleccion.PermisoBloqueado => MensajePermisoBloqueado,
				// El aviso de permiso bloqueado se retira: hablaba de un botón que ya no se muestra.
				_ => OfreceAjustesDeCamara ? null : MensajeError,
			};

			// La salida solo se ofrece si el sistema ya no volverá a preguntar.
			OfreceAjustesDeCamara = seleccion.Desenlace == DesenlaceSeleccion.PermisoBloqueado;

			return;
		}

		OfreceAjustesDeCamara = false;

		var uuid = await AsegurarRegistroParaEvidenciaAsync();
		if (uuid is null)
		{
			return;
		}

		// En el orden elegido; el primero que se rechace detiene la tanda y dice cuál no entró.
		MensajeError = null;
		foreach (var archivo in seleccion.Archivos)
		{
			var resultado = await _adjuntarEvidencia.EjecutarAsync(
				uuid, archivo, RechazadaEnCorreccion ?? BorradorEnEdicion);

			if (!resultado.Exito)
			{
				MensajeError = seleccion.Archivos.Count > 1
					? $"{archivo.NombreOriginal}: {MensajeDe(resultado.Motivo)}"
					: MensajeDe(resultado.Motivo);
				CampoConError?.Invoke(this, CampoCaptura.Evidencia);
				break;
			}
		}

		if (MensajeError is null && seleccion.Ilegibles > 0)
		{
			MensajeError = seleccion.Ilegibles == 1
				? "Uno de los archivos marcados no se pudo leer y se quedó fuera."
				: $"{seleccion.Ilegibles} de los archivos marcados no se pudieron leer y se quedaron fuera.";
			CampoConError?.Invoke(this, CampoCaptura.Evidencia);
		}

		await RecargarEvidenciasAsync();
	}

	// Sirve para lo enviado y lo no enviado: el archivo local no se borra al sincronizar.
	[RelayCommand]
	private async Task AbrirEvidenciaAsync(EvidenciaVista? evidencia)
	{
		if (evidencia is null)
		{
			return;
		}

		var resultado = await _visorEvidencia.AbrirAsync(
			evidencia.Ruta, evidencia.Nombre, evidencia.TipoMime);

		// Abrir bien no dice nada; cada falla se corrige distinto.
		MensajeError = resultado switch
		{
			ResultadoApertura.ArchivoNoEncontrado =>
				"El archivo de esa evidencia ya no está en el dispositivo.",
			ResultadoApertura.SinAplicacion =>
				$"No hay una aplicación en el dispositivo para abrir un archivo {evidencia.Tipo}.",
			ResultadoApertura.NoSePudo =>
				"No se pudo abrir la evidencia.",
			_ => MensajeError,
		};
	}

	[RelayCommand]
	private async Task QuitarEvidenciaAsync(EvidenciaVista? evidencia)
	{
		if (evidencia is null)
		{
			return;
		}

		await _quitarEvidencia.EjecutarAsync(evidencia.Uuid);
		await RecargarEvidenciasAsync();
	}

	// El borrador queda visible en la lista: es el precio de no perder la foto.
	private async Task<string?> AsegurarRegistroParaEvidenciaAsync()
	{
		if (_uuidParaEvidencias is { } yaHay)
		{
			return yaHay;
		}

		if (BorradorEnEdicion is not { } clave)
		{
			clave = await _incidencias.GuardarBorradorAsync(
				TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());

			BorradorEnEdicion = clave;
			await RecargarBorradoresAsync();
		}

		var borrador = await _incidencias.ObtenerBorradorAsync(clave);
		_uuidParaEvidencias = borrador?.Uuid;

		return _uuidParaEvidencias;
	}

	private async Task RecargarEvidenciasAsync()
	{
		ResumenEvidencias = await _obtenerEvidencias.EjecutarAsync(_uuidParaEvidencias);

		Evidencias.Clear();
		foreach (var adjunta in ResumenEvidencias.Adjuntas)
		{
			Evidencias.Add(EvidenciaVista.Desde(adjunta));
		}

		OnPropertyChanged(nameof(PuedeAdjuntar));
		OnPropertyChanged(nameof(PuedeAdjuntarVideo));
		OnPropertyChanged(nameof(AvisoVideo));
		OnPropertyChanged(nameof(HayAvisoVideo));
		OnPropertyChanged(nameof(ContadorEvidencias));
		OnPropertyChanged(nameof(HayEvidencias));
		AdjuntarDeCamaraCommand.NotifyCanExecuteChanged();
		AdjuntarDeGaleriaCommand.NotifyCanExecuteChanged();
	}

	// Con lo que publica el catálogo: una lista escrita aquí se desalinearía con el servidor.
	private string FormatosLegibles => string.Join(
		", ",
		ResumenEvidencias.Limites.FormatosPermitidos
			.Select(formato => formato[(formato.LastIndexOf('/') + 1)..].ToUpperInvariant())
			.Distinct());

	// Dice dónde se arregla: desde la app ya no se puede volver a pedir.
	private const string MensajePermisoBloqueado =
		"La cámara no tiene permiso y el sistema ya no volverá a preguntar. "
		+ "Actívelo desde la configuración de la aplicación.";

	// Solo con el permiso bloqueado: ofrecerlo siempre convertiría una negativa en un trámite.
	[ObservableProperty]
	public partial bool OfreceAjustesDeCamara { get; set; }

	[RelayCommand]
	private Task AbrirAjustesDeCamaraAsync() => _selectorEvidencia.AbrirConfiguracionAsync();

	private static string MensajeNoSePudoAbrir(OrigenEvidencia origen) => origen switch
	{
		OrigenEvidencia.Camara => "No se pudo abrir la cámara. Adjunte el archivo desde el dispositivo.",
		OrigenEvidencia.Video => "No se pudo abrir la cámara para grabar. Adjunte el archivo desde el dispositivo.",
		OrigenEvidencia.Galeria => "No se pudo abrir la galería. Use «Elegir archivo».",
		_ => "No se pudo abrir el selector de archivos.",
	};

	private string MensajeDe(MotivoEvidenciaRechazada motivo) => motivo switch
	{
		MotivoEvidenciaRechazada.LimitesDesconocidos =>
			"Conéctese una vez para poder adjuntar archivos.",
		MotivoEvidenciaRechazada.CupoLleno =>
			$"Ya adjuntó el máximo de {ResumenEvidencias.Limites.MaximoArchivosPorIncidencia} archivos. Quite uno para agregar otro.",
		// Literal de la historia para formato no admitido, con el porqué para elegir otro archivo.
		MotivoEvidenciaRechazada.FormatoNoAdmitido =>
			$"No se pudo procesar la evidencia: ese tipo de archivo no se admite. Se admiten: {FormatosLegibles}.",
		MotivoEvidenciaRechazada.DemasiadoGrande =>
			$"El archivo pasa de {ResumenEvidencias.Limites.TamanoMaximoMb} MB.",
		MotivoEvidenciaRechazada.ArchivoVacio =>
			"No se pudo procesar la evidencia: el archivo no se pudo leer. Intente tomarlo de nuevo.",
		MotivoEvidenciaRechazada.SinEspacio =>
			"No hay espacio suficiente en el dispositivo para guardar esa evidencia. "
			+ "Puede continuar con la nota, o liberar espacio y volver a intentarlo.",
		_ => "No se pudo adjuntar el archivo.",
	};
}
