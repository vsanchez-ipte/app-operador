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
public sealed partial class CapturaViewModel : ObservableObject
{
	// Textos fijados por JTT-280.
	private const string MensajeKilometroInvalido = "Capture un KM válido";
	private const string MensajeDescripcionRequerida = "Describa la incidencia de tipo Otro";

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

	private readonly IIncidentRepository _incidencias;
	private readonly ICatalogoRepository _catalogo;
	private readonly ConvertirBorradorEnIncidencia _convertirBorrador;
	private readonly ISincronizadorIncidencias _sincronizador;
	private readonly ObtenerKilometroPorUbicacion _obtenerKilometro;
	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly ISelectorEvidencia _selectorEvidencia;
	private readonly AdjuntarEvidencia _adjuntarEvidencia;
	private readonly QuitarEvidencia _quitarEvidencia;
	private readonly ObtenerEvidenciasDeIncidencia _obtenerEvidencias;

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
	/// Marca que el kilómetro lo está escribiendo la lectura del GPS, no el operador.
	/// </summary>
	/// <remarks>
	/// Sin esto no se distingue quién escribió: <see cref="OnKilometroChanged(string)"/> se dispara
	/// igual cuando <see cref="RecalcularUbicacionAsync"/> asigna la lectura que cuando el operador
	/// teclea, y la fuente acabaría siempre en <see cref="KilometerSource.Manual"/>.
	/// </remarks>
	private bool _asignandoDesdeGps;

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

	[ObservableProperty]
	public partial string? AvisoGps { get; set; }

	/// <summary>
	/// Origen del kilómetro: GPS mientras la lectura sea válida, Manual en cuanto el
	/// operador lo escriba a mano.
	/// </summary>
	[ObservableProperty]
	public partial KilometerSource FuenteKilometro { get; set; }

	/// <summary>
	/// Clave del borrador que se está editando, o <see langword="null"/> si se captura uno nuevo.
	/// </summary>
	/// <remarks>
	/// <b>Es el interruptor de toda la pantalla.</b> Con un borrador abierto, los dos botones
	/// dejan de crear y pasan a actuar sobre él: guardar actualiza en vez de duplicar, y
	/// registrar convierte en vez de crear una segunda incidencia con los mismos datos.
	/// </remarks>
	[ObservableProperty]
	public partial string? BorradorEnEdicion { get; set; }

	/// <summary>
	/// Qué pasó al enviar la incidencia recién guardada, o <see langword="null"/> si no se ha
	/// guardado ninguna en esta pasada.
	/// </summary>
	/// <remarks>
	/// Va aparte de <see cref="MensajeError"/> a propósito: <b>no es un error</b>. Que una
	/// incidencia se quede en la cola por falta de señal es el funcionamiento normal en campo, y
	/// pintarlo en rojo enseñaría al operador a ignorar los mensajes rojos.
	/// </remarks>
	[ObservableProperty]
	public partial string? MensajeEnvio { get; set; }

	public CapturaViewModel(
		IIncidentRepository incidencias,
		ICatalogoRepository catalogo,
		ObtenerKilometroPorUbicacion obtenerKilometro,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace,
		ConvertirBorradorEnIncidencia convertirBorrador,
		ISincronizadorIncidencias sincronizador,
		ISelectorEvidencia selectorEvidencia,
		AdjuntarEvidencia adjuntarEvidencia,
		QuitarEvidencia quitarEvidencia,
		ObtenerEvidenciasDeIncidencia obtenerEvidencias)
	{
		_incidencias = incidencias;
		_catalogo = catalogo;
		_convertirBorrador = convertirBorrador;
		_sincronizador = sincronizador;
		_obtenerKilometro = obtenerKilometro;
		_capacidades = capacidades;
		_selectorEvidencia = selectorEvidencia;
		_adjuntarEvidencia = adjuntarEvidencia;
		_quitarEvidencia = quitarEvidencia;
		_obtenerEvidencias = obtenerEvidencias;
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

	/// <summary>Borradores guardados, listados bajo el formulario.</summary>
	public ObservableCollection<RegistroColaVista> Borradores { get; } = [];

	public bool HayError => !string.IsNullOrEmpty(MensajeError);

	public bool HayAvisoGps => !string.IsNullOrEmpty(AvisoGps);

	public bool HayBorradores => Borradores.Count > 0;

	/// <summary>Indica si hay algo que decir sobre el último envío.</summary>
	public bool HayMensajeEnvio => !string.IsNullOrEmpty(MensajeEnvio);

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

	/// <summary>
	/// Etiqueta del campo de kilómetro, con la fuente de la que salió (JTT-1393 CA 6).
	/// </summary>
	/// <remarks>
	/// Antes era el texto fijo «KM (GPS O MANUAL)», que enuncia las dos posibilidades pero no
	/// dice cuál ocurrió. El criterio pide justamente lo segundo.
	/// </remarks>
	public string EtiquetaKilometro =>
		FuenteKilometro == KilometerSource.GPS ? "KM (GPS)" : "KM (MANUAL)";

	/// <summary>Carga catálogos e intenta situar al operador por GPS.</summary>
	public async Task InicializarAsync()
	{
		await CargarCatalogoAsync();

		// Lo que la sesión autoriza se reevalúa al entrar: pudo cerrarse o revocarse mientras la
		// pantalla no estaba a la vista (JTT-1385 CA 3).
		NotificarAutorizacion();

		await RecalcularUbicacionAsync();
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

	/// <summary>
	/// Intenta completar el kilómetro con la lectura del GPS.
	/// </summary>
	/// <remarks>
	/// Si la posición no pertenece al corredor o no hay lectura válida, se avisa y queda
	/// la captura manual, tal como describe el flujo 5.3 del documento de arquitectura.
	/// </remarks>
	[RelayCommand]
	private async Task RecalcularUbicacionAsync()
	{
		var resultado = await _obtenerKilometro.EjecutarAsync();
		if (!resultado.HayKilometro)
		{
			var teniaKilometroGps = FuenteKilometro == KilometerSource.GPS;
			_posicionGps = null;
			FuenteKilometro = KilometerSource.Manual;

			// Una lectura anterior no puede sobrevivir como si fuera captura manual. Lo que el
			// operador haya escrito a mano sí se conserva cuando un reintento falla.
			if (teniaKilometroGps)
			{
				_asignandoDesdeGps = true;
				Kilometro = string.Empty;
				_asignandoDesdeGps = false;
			}

			AvisoGps = MensajeDe(resultado.Motivo);
			return;
		}

		AvisoGps = null;
		_posicionGps = resultado.Posicion;

		// La bandera evita que el propio GPS marque el kilómetro como capturado a mano.
		_asignandoDesdeGps = true;
		Kilometro = resultado.Kilometro!.Valor;
		_asignandoDesdeGps = false;

		FuenteKilometro = KilometerSource.GPS;
	}

	private static string MensajeDe(MotivoSinKilometro? motivo) => motivo switch
	{
		MotivoSinKilometro.ServicioNoDisponible => MensajeGpsNoDisponible,
		MotivoSinKilometro.PermisoDenegado => MensajeGpsSinPermiso,
		MotivoSinKilometro.PrecisionInsuficiente => MensajeGpsSinPrecision,
		MotivoSinKilometro.FueraDelCorredor => MensajeFueraDelCorredor,
		MotivoSinKilometro.TramoSinGeometria => MensajeTramoSinGeometria,
		_ => MensajeErrorGps,
	};

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

		if (SeveridadSeleccionada is null)
		{
			// No debería llegar aquí: sin severidades no hay catálogo y el botón está apagado.
			MensajeError = MensajeSinCatalogo;
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
		if (!ReglaNotaIncidencia.EsSuficiente(TipoSeleccionado.ExigeDescripcion, nota))
		{
			MensajeError = MensajeDescripcionRequerida;
			return;
		}

		MensajeError = null;
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
	}

	[RelayCommand(CanExecute = nameof(PuedeRegistrar))]
	private async Task GuardarBorradorAsync()
	{
		// Un borrador es captura a medias, así que necesita la misma autorización que registrar.
		if (!_capacidades.Puede(CapacidadOperador.RegistrarIncidencia))
		{
			// Mismo caso que en el guardado: se refresca para que el aviso explique el bloqueo.
			NotificarAutorizacion();
			return;
		}

		// Un borrador se guarda tal cual esté: no se valida, porque su razón de ser es
		// permitir dejar la captura a medias sin perderla.
		MensajeError = null;

		if (BorradorEnEdicion is { } claveEnEdicion)
		{
			// Editar actualiza el que ya existe. Crear uno nuevo dejaría al operador con dos
			// borradores del mismo hecho cada vez que guardara su avance (CA 8, «editarlo»).
			await _incidencias.ActualizarBorradorAsync(
				claveEnEdicion, TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());
			CancelarEdicionBorrador();
		}
		else
		{
			await _incidencias.GuardarBorradorAsync(
				TipoSeleccionado, Kilometro, SeveridadSeleccionada, Nota.Trim());
			LimpiarFormulario();
		}

		await RecargarBorradoresAsync();
	}

	/// <summary>
	/// Convierte el borrador abierto, delegando el CA 9 en el caso de uso.
	/// </summary>
	/// <remarks>
	/// <b>La pantalla no revalida por su cuenta.</b> El caso de uso es el único dueño de las
	/// validaciones de envío; aquí solo se traduce su respuesta a un aviso. Repetir las
	/// comprobaciones daría dos redacciones del mismo criterio, que es como se separan.
	/// </remarks>
	private async Task ConvertirBorradorAbiertoAsync(string clave)
	{
		var resultado = await _convertirBorrador.EjecutarAsync(
			clave,
			TipoSeleccionado,
			Kilometro,
			FuenteKilometro,
			SeveridadSeleccionada,
			Nota,
			posicionGps: FuenteKilometro == KilometerSource.GPS ? _posicionGps : null);

		if (resultado != ResultadoConversionBorrador.Convertido)
		{
			MensajeError = MensajeDe(resultado);

			// Si ya no existe, el formulario tiene que soltarlo: seguir editando un borrador
			// que desapareció deja al operador escribiendo sobre nada.
			if (resultado == ResultadoConversionBorrador.NoEncontrado)
			{
				BorradorEnEdicion = null;
				await RecargarBorradoresAsync();
			}

			return;
		}

		MensajeError = null;
		CancelarEdicionBorrador();
		await RecargarBorradoresAsync();

		// Convertir también crea una incidencia, así que también intenta salir en el momento.
		await IntentarEnviarRecienGuardadaAsync(clave);
	}

	// ── Ciclo de vida del borrador en pantalla (JTT-1399 CA 8) ────────────────────────

	/// <summary>Indica si el formulario está editando un borrador ya guardado.</summary>
	public bool EstaEditandoBorrador => BorradorEnEdicion is not null;

	/// <summary>Texto del botón principal, que cambia de significado al editar.</summary>
	/// <remarks>
	/// <b>El botón tiene que decir lo que va a hacer.</b> Con un borrador abierto, «Guardar
	/// incidencia» haría creer que se crea una nueva y quedarían dos registros del mismo hecho;
	/// lo que ocurre es que ese mismo borrador pasa a la cola.
	/// </remarks>
	public string TextoBotonPrimario =>
		EstaEditandoBorrador ? "Convertir en incidencia" : "Guardar incidencia";

	/// <inheritdoc cref="TextoBotonPrimario" />
	public string TextoBotonSecundario =>
		EstaEditandoBorrador ? "Actualizar borrador" : "Guardar borrador";

	/// <summary>
	/// Carga un borrador en el formulario para seguir capturándolo (JTT-1399 CA 8, «abrirlo»).
	/// </summary>
	/// <remarks>
	/// <b>Se reutiliza el mismo formulario en vez de abrir otra pantalla.</b> Editar un borrador
	/// es exactamente capturar, y una segunda pantalla obligaría a mantener dos veces las mismas
	/// validaciones y el mismo diseño.
	/// </remarks>
	[RelayCommand]
	private async Task AbrirBorradorAsync(RegistroColaVista? vista)
	{
		if (vista is null)
		{
			return;
		}

		var borrador = await _incidencias.ObtenerBorradorAsync(vista.ClaveLocal);
		if (borrador is null)
		{
			// Pudo eliminarse desde otro punto, o pertenecer a otra sesión.
			MensajeError = MensajeBorradorNoEncontrado;
			await RecargarBorradoresAsync();
			return;
		}

		MensajeError = null;
		BorradorEnEdicion = borrador.ClaveLocal;

		// Sus evidencias vienen con él: se adjuntaron a este UUID y siguen siendo suyas.
		// Reponer el UUID no basta —la lista y el contador siguen los del formulario anterior,
		// que SoltarEvidencias dejó vacíos—, así que hay que releerlas del repositorio.
		_uuidParaEvidencias = borrador.Uuid;
		await RecargarEvidenciasAsync();

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == borrador.TipoId);
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == borrador.SeveridadId);
		Nota = borrador.Nota;

		// El kilómetro se repone tal cual se guardó, aunque esté a medio escribir, y como
		// manual: reponerlo no es una lectura del GPS por mucho que lo fuera al capturarlo.
		Kilometro = borrador.Kilometro ?? string.Empty;
		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}

	/// <summary>
	/// Abandona la edición sin tocar el borrador (JTT-1399 CA 8).
	/// </summary>
	/// <remarks>
	/// Sin esta salida, quien abriera un borrador por error quedaría atrapado: los dos botones
	/// actuarían sobre él y no habría forma de volver a capturar uno nuevo.
	/// </remarks>
	[RelayCommand]
	private void CancelarEdicionBorrador()
	{
		BorradorEnEdicion = null;
		MensajeError = null;
		LimpiarFormulario();
		Kilometro = string.Empty;
	}

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

	/// <summary>
	/// Elimina el borrador que se está editando (JTT-1399 CA 8, «eliminarlo»).
	/// </summary>
	[RelayCommand]
	private async Task EliminarBorradorAsync()
	{
		if (BorradorEnEdicion is not { } clave)
		{
			return;
		}

		await _incidencias.EliminarBorradorAsync(clave);
		CancelarEdicionBorrador();
		await RecargarBorradoresAsync();
	}

	/// <summary>
	/// Traduce el motivo de una conversión fallida al aviso que lee el operador.
	/// </summary>
	/// <remarks>
	/// El kilómetro y la nota reutilizan los literales del guardado normal: es el mismo defecto
	/// y el operador no tiene por qué leer dos redacciones distintas del mismo problema.
	/// </remarks>
	private static string MensajeDe(ResultadoConversionBorrador motivo) => motivo switch
	{
		ResultadoConversionBorrador.FaltaTipo => MensajeBorradorSinTipo,
		ResultadoConversionBorrador.FaltaSeveridad => MensajeBorradorSinSeveridad,
		ResultadoConversionBorrador.KilometroInvalido => MensajeKilometroInvalido,
		ResultadoConversionBorrador.NotaInsuficiente => MensajeDescripcionRequerida,
		_ => MensajeBorradorNoEncontrado,
	};

	/// <summary>
	/// Intenta enviar a Jacob la incidencia que se acaba de guardar.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Guardar primero, enviar después, y siempre en ese orden.</b> La incidencia queda escrita
	/// en el dispositivo antes de tocar la red: si el envío falla, se cae, o el operador sale de la
	/// pantalla, lo capturado ya está a salvo y espera en la cola. Enviar antes de guardar
	/// convertiría cualquier fallo en pérdida de lo que el operador acaba de escribir.
	/// </para>
	/// <para>
	/// <b>Un fallo aquí no es un error de la captura.</b> Sin enlace, o con Jacob caído, la
	/// incidencia se queda pendiente y sale sola por el camino normal de reintentos; el operador
	/// solo necesita saber cuál de las dos cosas pasó.
	/// </para>
	/// </remarks>
	private async Task IntentarEnviarRecienGuardadaAsync(string claveLocal)
	{
		try
		{
			var resultado = await _sincronizador.EnviarUnaAsync(claveLocal);
			MensajeEnvio = TextoDelEnvio(claveLocal, resultado);
		}
		catch (Exception) when (!Debugger.IsAttached)
		{
			// Nada de lo que pueda fallar al enviar puede tumbar la captura: la incidencia ya
			// está guardada, que es lo que importa.
			MensajeEnvio = $"{claveLocal} quedó en la cola. Se enviará al recuperar la señal.";
		}
	}

	/// <summary>
	/// Qué se le dice al operador después de guardar (JTT-1401 CA 10).
	/// </summary>
	/// <remarks>
	/// <b>Siempre se nombra la clave local.</b> Es lo único que el operador puede volver a buscar
	/// en la cola, y con folio o sin él es su referencia hasta que Jacob conteste.
	/// <para>
	/// Textos provisionales: Producto no ha fijado los literales de esta pantalla.
	/// </para>
	/// </remarks>
	private static string TextoDelEnvio(string claveLocal, ResultadoSincronizacion resultado)
	{
		if (resultado.Confirmados == 1)
		{
			return $"{claveLocal} enviada al CCO.";
		}

		if (resultado.MotivoBloqueo is { } motivo)
		{
			return motivo switch
			{
				MotivoNoSincroniza.SinEnlaceConJacob =>
					$"{claveLocal} guardada sin conexión. Se enviará al recuperar la señal.",
				MotivoNoSincroniza.SinSesion =>
					$"{claveLocal} guardada. La sesión expiró: vuelva a ingresar para enviarla.",
				_ => $"{claveLocal} guardada. Su cuenta no tiene autorizado sincronizar.",
			};
		}

		// Falló el envío, y las dos familias significan cosas muy distintas para el operador:
		// una la puede corregir y la otra no depende de él.
		return resultado.FamiliaUltimoError switch
		{
			// Técnico: Jacob NO llegó a evaluarla —servidor caído, red que se cortó a medias—.
			// Decir «no la aceptó» sería mentir y mandaría al operador a revisar una captura
			// que está bien.
			FamiliaErrorSincronizacion.Tecnico =>
				$"{claveLocal} guardada. No se pudo contactar al CCO; se reintentará sola.",

			// Funcional: Jacob la evaluó y la rechazó. Se muestra SU mensaje, no uno propio:
			// el servidor sabe qué está mal —«el kilómetro 119.999 está fuera del corredor»— y
			// reescribirlo como «no se aceptó» obliga al operador a adivinar qué corregir.
			FamiliaErrorSincronizacion.Funcional =>
				$"{claveLocal}: {resultado.MensajeUltimoError ?? "el CCO la rechazó. Revise la cola."}",

			_ => $"{claveLocal} quedó en la cola.",
		};
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
		SeveridadSeleccionada = Severidades.FirstOrDefault();
		SoltarEvidencias();
	}

	partial void OnMensajeErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

	partial void OnMensajeEnvioChanged(string? value) => OnPropertyChanged(nameof(HayMensajeEnvio));

	/// <summary>Abrir o soltar un borrador cambia lo que los dos botones significan.</summary>
	partial void OnBorradorEnEdicionChanged(string? value)
	{
		OnPropertyChanged(nameof(EstaEditandoBorrador));
		OnPropertyChanged(nameof(TextoBotonPrimario));
		OnPropertyChanged(nameof(TextoBotonSecundario));
	}

	partial void OnAvisoGpsChanged(string? value) => OnPropertyChanged(nameof(HayAvisoGps));

	partial void OnNotaChanged(string value) => OnPropertyChanged(nameof(ContadorNota));

	partial void OnFuenteKilometroChanged(KilometerSource value) =>
		OnPropertyChanged(nameof(EtiquetaKilometro));

	/// <summary>
	/// Escribir el kilómetro a mano cambia su origen: deja de ser una lectura del GPS.
	/// </summary>
	/// <remarks>
	/// La condición anterior —cambiar a manual solo si además había aviso de GPS— nunca se
	/// cumplía en el caso que importa: con lectura válida no hay aviso, así que corregir a mano
	/// un kilómetro obtenido por GPS lo dejaba marcado como GPS. No se notaba porque la fuente
	/// no se mostraba en ninguna parte; al presentarla (CA 6) queda a la vista.
	/// </remarks>
	partial void OnKilometroChanged(string value)
	{
		if (_asignandoDesdeGps)
		{
			return;
		}

		FuenteKilometro = KilometerSource.Manual;
		_posicionGps = null;
	}

	// ── Evidencia (JTT-1398 CA 1) ─────────────────────────────────────────────────────

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
	public string AvisoVideo => ResumenEvidencias.Limites.EstanDefinidos
		&& !ResumenEvidencias.Limites.AdmiteVideo
			? "El servidor todavía no admite video."
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
			return;
		}

		var seleccion = await _selectorEvidencia.ElegirAsync(origen);

		if (seleccion.Archivo is not { } archivo)
		{
			// Que el dispositivo no pueda abrir la cámara o el selector NO es una respuesta del
			// operador, y callarlo lo deja pulsando un botón mudo: es lo que pasó el 28-ago.
			// Cancelar y negar el permiso sí se atienden en silencio, que es lo que pide el CA 4.
			if (seleccion.NoSePudoAbrir)
			{
				MensajeError = MensajeNoSePudoAbrir(origen);
			}

			return;
		}

		var uuid = await AsegurarRegistroParaEvidenciaAsync();
		if (uuid is null)
		{
			return;
		}

		// BorradorEnEdicion es la clave local del registro: con ella se nombra lo capturado en
		// vez de dejar el GUID que entrega el sistema.
		var resultado = await _adjuntarEvidencia.EjecutarAsync(uuid, archivo, BorradorEnEdicion);

		MensajeError = resultado.Exito ? null : MensajeDe(resultado.Motivo);

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
		MotivoEvidenciaRechazada.FormatoNoAdmitido =>
			$"Ese tipo de archivo no se admite. Se admiten: {FormatosLegibles}.",
		MotivoEvidenciaRechazada.DemasiadoGrande =>
			$"El archivo pasa de {ResumenEvidencias.Limites.TamanoMaximoMb} MB.",
		MotivoEvidenciaRechazada.ArchivoVacio =>
			"El archivo no se pudo leer. Intente tomarlo de nuevo.",
		_ => "No se pudo adjuntar el archivo.",
	};
}
