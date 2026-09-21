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

/// <summary>
/// Captura de una incidencia de campo (JTT-280).
/// </summary>
/// <remarks>
/// El kilómetro se intenta obtener del GPS al abrir la pantalla; si no hay lectura
/// válida, queda habilitada la captura manual. La validación del formato la hace el
/// value object <see cref="Kilometer"/>, no este ViewModel.
/// </remarks>
public sealed partial class CapturaViewModel : ObservableObject, IQueryAttributable
{
	// Textos fijados por JTT-280.
	private const string MensajeKilometroInvalido = "Capture un KM válido";
	// Con el nombre del tipo y no con «Otro» fijo: quién exige descripción lo dice el catálogo y
	// puede cambiar sin publicar versión; el aviso nombra al tipo que esté seleccionado.
	private const string FormatoDescripcionRequerida = "Describa la incidencia de tipo {0}";

	/// <summary>
	/// Se muestra cuando todavía no se ha descargado el catálogo (JTT-1394 CA 2).
	/// </summary>
	/// <remarks>
	/// Hasta JTT-1394 la app sembraba seis tipos de la maqueta y esta situación no existía. Al
	/// dejar de inventarlos, un dispositivo recién actualizado que no se haya conectado nunca
	/// no tiene con qué llenar el formulario, y hay que decirlo: en blanco parecería que la app
	/// está descompuesta.
	/// <para>
	/// Texto provisional: Producto no ha fijado el literal de este caso.
	/// </para>
	/// </remarks>
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

	/// <summary>
	/// Se muestra cuando la sesión no autoriza capturar (JTT-1404 CA 5).
	/// </summary>
	/// <remarks>
	/// Antes los botones simplemente salían deshabilitados y <b>nadie explicaba por qué</b>:
	/// desde la carretera eso es indistinguible de que la app esté descompuesta. El literal
	/// dice las tres cosas que el operador necesita —que no es una falla, qué le falta y qué
	/// hacer— sin nombrar el código del permiso, que a él no le dice nada.
	/// <para>
	/// Texto provisional: Producto no ha fijado el literal de este caso. Va con los demás
	/// pendientes de literales de esta pantalla.
	/// </para>
	/// </remarks>
	private const string MensajeSinPermisoCaptura =
		"Su cuenta no tiene autorizado registrar incidencias. Solicite el acceso al CCO y vuelva a ingresar.";

	/// <summary>
	/// Se muestra cuando la conversión de un borrador no pasa las validaciones (JTT-1399 CA 9).
	/// </summary>
	/// <remarks>
	/// Cada motivo tiene su propio texto porque el operador tiene que saber <b>qué campo</b> le
	/// falta: un «no se pudo» frente a un formulario de seis campos obliga a adivinar, y se
	/// captura en carretera.
	/// <para>
	/// Textos provisionales: Producto no ha fijado los literales de esta pantalla.
	/// </para>
	/// </remarks>
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

	// Un error por campo, debajo del campo que lo causó: un aviso al pie del formulario no dice
	// dónde mirar y, con el formulario largo, ni siquiera se ve. Cada uno se apaga solo en
	// cuanto el operador toca ese campo.
	[ObservableProperty]
	public partial string? ErrorTipo { get; set; }

	[ObservableProperty]
	public partial string? ErrorKilometro { get; set; }

	[ObservableProperty]
	public partial string? ErrorSeveridad { get; set; }

	[ObservableProperty]
	public partial string? ErrorNota { get; set; }

	/// <summary>
	/// Avisa a la página qué campo acaba de fallar, para que se desplace hasta él.
	/// </summary>
	/// <remarks>
	/// Es un evento y no una llamada a la vista: el ViewModel no sabe de <c>ScrollView</c> ni
	/// de elementos con nombre, y así sigue sin saberlo. La página decide cómo llegar ahí.
	/// </remarks>
	public event EventHandler<CampoCaptura>? CampoConError;

	/// <summary>
	/// Clave del borrador que se está editando, o <see langword="null"/> si se captura uno nuevo.
	/// </summary>
	/// <remarks>
	/// <b>Es el interruptor de toda la pantalla.</b> Con un borrador abierto, los dos botones
	/// dejan de crear y pasan a actuar sobre él: guardar actualiza en vez de duplicar, y
	/// registrar convierte en vez de crear una segunda incidencia con los mismos datos.
	/// </remarks>

	/// <summary>
	/// Indica si la pantalla está cargando lo que muestra. Enciende el indicador de arriba.
	/// </summary>
	/// <remarks>
	/// Va aparte de <c>Ocupado</c> —que apaga botones mientras el operador espera una acción
	/// suya— porque esto ocurre solo, al entrar, y lo que hay que decir es que la pantalla
	/// todavía no está lista, no que un botón está trabajando.
	/// </remarks>
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
		_adjuntarEvidencia = adjuntarEvidencia;
		_quitarEvidencia = quitarEvidencia;
		_obtenerEvidencias = obtenerEvidencias;
		_eliminarBorrador = eliminarBorrador;
		Enlace = enlace;

		Kilometro = string.Empty;
		Nota = string.Empty;
		FuenteKilometro = KilometerSource.Manual;
	}

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>
	/// Catálogo de tipos de incidencia.
	/// </summary>
	/// <remarks>Observable por la misma razón que las unidades: se carga tras el enlace.</remarks>
	public ObservableCollection<TipoIncidencia> Tipos { get; } = [];

	/// <summary>
	/// Niveles de severidad del catálogo de Jacob (JTT-1394).
	/// </summary>
	/// <remarks>
	/// Observable y no fija: hasta JTT-1394 era una lista de cuatro valores inventados en la
	/// app. Ahora la llena el catálogo descargado, que trae tres.
	/// </remarks>
	public ObservableCollection<SeveridadIncidencia> Severidades { get; } = [];

	public bool HayError => !string.IsNullOrEmpty(MensajeError);

	public bool HayErrorTipo => !string.IsNullOrEmpty(ErrorTipo);

	public bool HayErrorKilometro => !string.IsNullOrEmpty(ErrorKilometro);

	public bool HayErrorSeveridad => !string.IsNullOrEmpty(ErrorSeveridad);

	public bool HayErrorNota => !string.IsNullOrEmpty(ErrorNota);

	/// <summary>
	/// Tope de caracteres de la nota (JTT-1393 CA 7).
	/// </summary>
	/// <remarks>
	/// Se expone como propiedad, y no como literal en la vista, para que el tope y el contador
	/// que lo anuncia salgan del mismo sitio y no puedan discrepar. El número lo pone el
	/// dominio: la pantalla lo muestra, no lo decide.
	/// </remarks>
	public int LongitudMaximaNota => ReglaNotaIncidencia.MaximoCaracteres;

	/// <summary>
	/// Contador de caracteres de la nota, como en la maqueta.
	/// </summary>
	/// <remarks>
	/// Muestra también el tope: un contador que solo sube no le dice al operador —ni a quien
	/// verifica— cuál es el límite (JTT-1393 CA 7).
	/// </remarks>
	public string ContadorNota => $"{Nota.Length}/{LongitudMaximaNota} car.";

	/// <summary>Carga catálogos e intenta situar al operador por GPS.</summary>
	public async Task InicializarAsync()
	{
		// La capa de carga cubre solo lo local —catálogo, borradores, evidencias: milisegundos—.
		// El GPS se pide después, con la pantalla ya usable y su propio aviso junto al KM: es
		// lo único que tarda, y esperarlo con toda la pantalla tapada desesperaba al operador.
		var claveACorregir = _claveACorregir;
		_claveACorregir = null;

		Cargando = true;
		try
		{
			await InicializarCargandoAsync();

			// Lo que llegó desde la Cola se repone antes de destapar la pantalla: un formulario
			// vacío y usable durante unos segundos, que después se rellena solo, es peor que
			// esperar. Su kilómetro es manual, así que no se pide el GPS.
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

		// Lo que la sesión autoriza se reevalúa al entrar: pudo cerrarse o revocarse mientras la
		// pantalla no estaba a la vista (JTT-1385 CA 3).
		NotificarAutorizacion();

		await RecargarBorradoresAsync();

		// Aunque todavía no haya registro: hacen falta los límites para saber si el botón de
		// adjuntar va encendido antes de que exista nada que adjuntar.
		await RecargarEvidenciasAsync();
	}

	/// <summary>
	/// Vuelca en la pantalla el catálogo guardado (JTT-1394 CA 2).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Lee la <b>copia local</b>, no la red: es lo que permite capturar sin conexión. Quien
	/// refresca esa copia es la validación en línea o la sincronización, que es el CA 4.
	/// </para>
	/// <para>
	/// Se recarga en cada entrada a la pantalla, y no solo la primera vez, porque entre dos
	/// aperturas pudo haberse descargado un catálogo nuevo. Se conserva lo que el operador
	/// tenía elegido si sigue existiendo; si lo retiraron del catálogo, se cae al primero.
	/// </para>
	/// </remarks>
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

	/// <summary>Indica si hay catálogo descargado con el que capturar.</summary>
	public bool HayCatalogo => Tipos.Count > 0 && Severidades.Count > 0;

	/// <summary>Explicación de que falta descargar el catálogo, o <see langword="null"/>.</summary>
	public string? AvisoSinCatalogo => HayCatalogo ? null : MensajeSinCatalogo;

	/// <inheritdoc cref="AvisoSinCatalogo" />
	public bool HayAvisoSinCatalogo => !HayCatalogo;

	/// <summary>Indica si la sesión autoriza registrar incidencias (JTT-1385 CA 3 y 4).</summary>
	public bool PuedeRegistrar => TienePermisoDeCaptura && HayCatalogo;

	/// <summary>
	/// Si la sesión autoriza capturar, al margen de que haya catálogo con qué hacerlo.
	/// </summary>
	/// <remarks>
	/// <b>Se separa de <see cref="PuedeRegistrar"/> a propósito.</b> Los botones se apagan por
	/// dos motivos distintos —falta el permiso o falta el catálogo— y cada uno tiene su aviso.
	/// Si el aviso de permiso colgara de <see cref="PuedeRegistrar"/>, un operador que sí tiene
	/// permiso pero todavía no ha bajado el catálogo leería que su cuenta no está autorizada, y
	/// acabaría pidiendo al CCO algo que ya tiene.
	/// </remarks>
	public bool TienePermisoDeCaptura => _capacidades.Puede(CapacidadOperador.RegistrarIncidencia);

	/// <summary>
	/// Explicación visible de por qué la captura está bloqueada, o <see langword="null"/> si
	/// no lo está (JTT-1404 CA 5).
	/// </summary>
	/// <remarks>
	/// Se deriva de <see cref="PuedeRegistrar"/> en vez de fijarse a mano: así el aviso
	/// aparece y desaparece con la autorización, sin que nadie tenga que acordarse de
	/// limpiarlo. Es la misma capacidad que gobierna los botones, preguntada una vez.
	/// </remarks>
	public string? AvisoSinPermiso => TienePermisoDeCaptura ? null : MensajeSinPermisoCaptura;

	/// <summary>Indica si hay que mostrar el aviso de falta de permiso.</summary>
	public bool HayAvisoSinPermiso => !TienePermisoDeCaptura;

	/// <summary>Reevalúa lo que la sesión autoriza. La llaman la pantalla y el guardado.</summary>
	/// <remarks>
	/// Notifica también el aviso: si solo se refrescaran los botones, un permiso revocado a
	/// media sesión los apagaría <b>sin decir por qué</b>, que es justo lo que este aviso
	/// existe para evitar.
	/// </remarks>
	public void NotificarAutorizacion()
	{
		OnPropertyChanged(nameof(TienePermisoDeCaptura));
		OnPropertyChanged(nameof(PuedeRegistrar));
		OnPropertyChanged(nameof(AvisoSinPermiso));
		OnPropertyChanged(nameof(HayAvisoSinPermiso));
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
			// El permiso se revocó entre que se pintó la pantalla y este toque. Se refresca
			// la vista para que el botón se apague Y aparezca el aviso: salir en silencio
			// dejaría al operador tocando un botón que no responde (JTT-1404 CA 5).
			NotificarAutorizacion();
			return;
		}

		// Con un borrador abierto, registrar es convertir ESE borrador y no crear otra
		// incidencia: si no, quedarían dos registros del mismo hecho, uno en la cola y el
		// borrador original intacto (JTT-1399 CA 8 y 9).
		if (BorradorEnEdicion is { } claveEnEdicion)
		{
			await ConvertirBorradorAbiertoAsync(claveEnEdicion);
			return;
		}

		// Con un rechazado abierto, registrar es reenviar ESE registro corregido (JTT-291 CA 8).
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
			// Con el formulario recién limpiado tras guardar, un segundo toque llega aquí: se
			// dice, en vez de callar y dejar al operador creyendo que no pasó nada.
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

		// El kilómetro de la siguiente captura se pide al GPS después de enviar y sin esperar:
		// la lectura tarda hasta diez segundos y el aviso de «guardada/enviada» no tiene por qué
		// esperarla. Mientras llega, el campo dice que se está buscando.
		RecalcularUbicacionSinEsperar();
	}

	// ── Ciclo de vida del borrador en pantalla (JTT-1399 CA 8) ────────────────────────

	// ── Corregir un registro rechazado (JTT-291 CA 8) ─────────────────────────────────

	/// <summary>
	/// Traduce el motivo de una conversión fallida al aviso que lee el operador.
	/// </summary>
	/// <remarks>
	/// El kilómetro y la nota reutilizan los literales del guardado normal: es el mismo defecto
	/// y el operador no tiene por qué leer dos redacciones distintas del mismo problema.
	/// </remarks>
	private string MensajeDescripcionRequerida() =>
		string.Format(FormatoDescripcionRequerida, TipoSeleccionado?.Nombre ?? "seleccionado");

	/// <summary>Texto del botón principal, que cambia de significado al editar.</summary>
	/// <remarks>
	/// <b>El botón tiene que decir lo que va a hacer.</b> Con un borrador abierto, «Guardar
	/// incidencia» haría creer que se crea una nueva y quedarían dos registros del mismo hecho;
	/// lo que ocurre es que ese mismo borrador pasa a la cola.
	/// </remarks>
	public string TextoBotonPrimario => EstaCorrigiendo
		? "Reenviar corregida"
		: EstaEditandoBorrador ? "Convertir en incidencia" : "Guardar incidencia";

	/// <summary>
	/// Deja el formulario como en una captura nueva.
	/// </summary>
	/// <remarks>
	/// El tipo y el kilómetro también: si se quedaran, un segundo toque de «Guardar» crearía
	/// otra incidencia del mismo hecho. El aviso del envío anterior se retira por lo mismo:
	/// hablaba de otro registro.
	/// </remarks>
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

	/// <summary>Pone el error debajo de su campo, quita el general y pide desplazarse hasta él.</summary>
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

	// ── Evidencia (JTT-1398 CA 1) ─────────────────────────────────────────────────────
}
