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

/// <summary>Evidencia: adjuntar desde cámara, galería o archivos, y quitar (JTT-1398, JTT-289).</summary>
public sealed partial class CapturaViewModel
{
	/// <summary>
	/// UUID del registro al que se están adjuntando evidencias, o <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// <b>Adjuntar antes de guardar obliga a que exista algo a lo que adjuntar.</b> El CA 1
	/// permite lo primero y el UUID nace al guardar, así que al adjuntar sin borrador abierto se
	/// guarda uno: la evidencia queda atada desde el principio y, si la app se cierra, no se
	/// pierde. Es el mismo criterio que JTT-1401 aplicó al envío —guardar antes de arriesgar— y
	/// usa lo que JTT-1399 ya construyó, incluido que convertir conserva el UUID.
	/// </remarks>
	private string? _uuidParaEvidencias;
	private PosicionDispositivo? _posicionGps;

	/// <summary>
	/// Suelta el registro al que se estaban adjuntando evidencias y vacía la lista.
	/// </summary>
	/// <remarks>
	/// <b>No borra nada.</b> Las evidencias siguen guardadas y atadas a su registro; lo que se
	/// suelta es la pantalla. Al abrir ese borrador otra vez vuelven a aparecer.
	/// </remarks>
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

	/// <summary>Los archivos adjuntos, tal como se listan antes de guardar.</summary>
	public ObservableCollection<EvidenciaVista> Evidencias { get; } = [];

	/// <summary>Lo que se puede hacer con las evidencias. Lo decide Aplicación, no la vista.</summary>
	[ObservableProperty]
	public partial ResumenEvidencias ResumenEvidencias { get; set; } = ResumenEvidencias.Vacio;

	/// <summary>Indica si el botón de adjuntar va encendido.</summary>
	/// <remarks>
	/// Exige además el permiso de captura: adjuntar es capturar, y el mismo módulo que autoriza
	/// registrar autoriza adjuntar (JTT-1385, JTT-1404).
	/// </remarks>
	public bool PuedeAdjuntar => TienePermisoDeCaptura && ResumenEvidencias.PuedeAdjuntar;

	/// <summary>Indica si el botón de grabar video va encendido.</summary>
	/// <remarks>
	/// <b>Hoy sale apagado</b>: el catálogo no publica ningún <c>video/*</c>. Se enciende solo
	/// el día que el servidor lo declare, sin tocar la app ni publicar una versión nueva — el
	/// mismo trato que a los otros tres límites.
	/// </remarks>
	public bool PuedeAdjuntarVideo => TienePermisoDeCaptura && ResumenEvidencias.PuedeAdjuntarVideo;

	/// <summary>Indica si el dispositivo tiene cámara. Sin ella el botón no se ofrece.</summary>
	public bool HayCamara => _selectorEvidencia.Disponible(OrigenEvidencia.Camara);

	/// <summary>Por qué el botón de video está apagado, cuando lo está.</summary>
	/// <remarks>
	/// Va junto al botón por lo mismo que el aviso de permiso: el operador mira el botón que no
	/// responde, y ahí es donde tiene que encontrar la explicación. Sin esto, un botón visible y
	/// muerto se lee como que la app está rota.
	/// </remarks>
	public string AvisoVideo => !ResumenEvidencias.Limites.EstanDefinidos
		? string.Empty
		: !ResumenEvidencias.Limites.AdmiteVideo
			? "El servidor todavía no admite video."
			: !ResumenEvidencias.HayEspacioParaVideo
				// JTT-289 CA 8: se bloquea el video y se dice con qué se puede seguir.
				? "No hay espacio en el dispositivo para un video. Puede continuar con texto o fotografía."
				: string.Empty;

	public bool HayAvisoVideo => AvisoVideo.Length > 0;

	/// <summary>Contador combinado, como lo pidió el PO: sin separar fotos de videos.</summary>
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

	/// <summary>Adjunta desde el selector de archivos, que es por donde entra un PDF.</summary>
	[RelayCommand]
	private Task AdjuntarDeArchivoAsync() => AdjuntarAsync(OrigenEvidencia.Archivo);

	/// <summary>
	/// Pide un archivo y lo adjunta, creando el borrador si hacía falta.
	/// </summary>
	private async Task AdjuntarAsync(OrigenEvidencia origen)
	{
		if (!_capacidades.Puede(CapacidadOperador.AdjuntarEvidencia))
		{
			NotificarAutorizacion();
			return;
		}

		// El video tiene una condición más que los demás orígenes —que el servidor lo admita—,
		// así que se pregunta por la suya y no por la general.
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

		var seleccion = await _selectorEvidencia.ElegirAsync(origen);

		if (seleccion.Archivos.Count == 0)
		{
			// Cancelar y negar el permiso por primera vez se atienden en silencio, que es lo que
			// pide el CA 4. Lo demás no es una decisión del operador y callarlo lo deja pulsando
			// un botón mudo: es lo que pasó el 28-ago con el manifiesto, y otra vez con el
			// permiso bloqueado.
			MensajeError = seleccion.Desenlace switch
			{
				DesenlaceSeleccion.NoSePudoAbrir => MensajeNoSePudoAbrir(origen),
				DesenlaceSeleccion.PermisoBloqueado => MensajePermisoBloqueado,
				// Si el aviso anterior era el de permiso bloqueado, se retira: hablaba de un
				// botón de configuración que ya no se va a mostrar.
				_ => OfreceAjustesDeCamara ? null : MensajeError,
			};

			// La salida solo se ofrece cuando de verdad hace falta: mientras el sistema siga
			// dispuesto a preguntar, el operador vuelve a tocar el botón y ya está.
			OfreceAjustesDeCamara = seleccion.Desenlace == DesenlaceSeleccion.PermisoBloqueado;

			return;
		}

		OfreceAjustesDeCamara = false;

		var uuid = await AsegurarRegistroParaEvidenciaAsync();
		if (uuid is null)
		{
			return;
		}

		// Uno por uno y en el orden en que se eligieron (JTT-289 CA 1). El primero que el
		// catálogo rechace detiene la tanda con su motivo: los anteriores ya quedaron adjuntos y
		// se ven en la lista, así que el operador sabe exactamente cuál no entró y por qué.
		//
		// La clave local es con la que se nombra lo capturado, en vez de dejar el GUID que
		// entrega el sistema.
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

	/// <summary>
	/// Devuelve el UUID al que atar la evidencia, guardando un borrador si todavía no hay.
	/// </summary>
	/// <remarks>
	/// <b>El borrador aparece en la lista, y eso es visible para el operador.</b> Es el precio
	/// de no perder la foto: sin registro previo, la evidencia viviría en memoria y se iría con
	/// la app. Convertir el borrador conserva el UUID (JTT-1399), así que la evidencia sigue
	/// siendo de la misma incidencia cuando se registre.
	/// </remarks>
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

	/// <summary>Vuelve a leer las evidencias y refresca lo que la pantalla muestra de ellas.</summary>
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

	/// <summary>Traduce el motivo del rechazo al aviso que lee el operador.</summary>
	/// <remarks>
	/// Cada motivo dice algo distinto porque cada uno se corrige distinto. Los literales son
	/// provisionales, como los demás de esta pantalla.
	/// </remarks>
	/// <summary>Los formatos que el servidor admite, como se le dicen al operador.</summary>
	/// <remarks>
	/// <b>Se arman con lo que publica el catálogo, no con una lista escrita aquí.</b> El texto
	/// anterior decía «elija una fotografía o un PDF», que era cierto hoy y dejaría de serlo en
	/// cuanto el API admitiera video: el operador leería que no se admite justo lo que sí. El
	/// CA 8 pide que el rechazo diga qué formato sí se admite, y eso solo lo sabe el servidor.
	/// </remarks>
	private string FormatosLegibles => string.Join(
		", ",
		ResumenEvidencias.Limites.FormatosPermitidos
			.Select(formato => formato[(formato.LastIndexOf('/') + 1)..].ToUpperInvariant())
			.Distinct());

	/// <summary>Qué se le dice al operador cuando el dispositivo no pudo abrir el selector.</summary>
	/// <remarks>
	/// Nombra <b>qué</b> no se pudo abrir y ofrece la salida que queda, porque la evidencia es
	/// opcional y las cuatro vías son intercambiables: si la cámara falla, el archivo sirve.
	/// <para>
	/// Textos provisionales: Producto no ha fijado los literales de esta pantalla.
	/// </para>
	/// </remarks>
	/// <summary>
	/// Aviso de que el permiso de cámara quedó bloqueado.
	/// </summary>
	/// <remarks>
	/// Dice <b>dónde</b> se arregla, no solo que falta: desde la aplicación ya no se puede volver
	/// a pedir, así que un «no tiene permiso» a secas dejaría al operador sin nada que hacer.
	/// </remarks>
	private const string MensajePermisoBloqueado =
		"La cámara no tiene permiso y el sistema ya no volverá a preguntar. "
		+ "Actívelo desde la configuración de la aplicación.";

	/// <summary>
	/// Indica si hay que ofrecer el botón que lleva a la configuración del sistema.
	/// </summary>
	/// <remarks>
	/// Solo cuando el permiso quedó bloqueado. Ofrecerlo siempre convertiría una negativa normal
	/// —que se resuelve volviendo a tocar el botón— en un trámite.
	/// </remarks>
	[ObservableProperty]
	public partial bool OfreceAjustesDeCamara { get; set; }

	/// <summary>Lleva al operador a la configuración del sistema para conceder la cámara.</summary>
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
		// «No se pudo procesar la evidencia» es el literal de JTT-289 CA 7 para formato no
		// admitido y archivo ilegible; se conserva y se le añade el porqué, que es lo que el
		// operador necesita para elegir otro archivo.
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
