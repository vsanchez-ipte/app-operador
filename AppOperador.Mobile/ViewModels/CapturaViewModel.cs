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
	private const string MensajeGpsNoDisponible = "No se pudo obtener el GPS. Capture el KM manualmente.";

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
	private readonly ILocationService _ubicacion;
	private readonly CapacidadesDeLaSesion _capacidades;

	/// <summary>
	/// Marca que el kilómetro lo está escribiendo la lectura del GPS, no el operador.
	/// </summary>
	/// <remarks>
	/// Sin esto no se distingue quién escribió: <see cref="OnKilometroChanged(string)"/> se dispara
	/// igual cuando <see cref="IntentarUbicarAsync"/> asigna la lectura que cuando el operador
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
		ILocationService ubicacion,
		CapacidadesDeLaSesion capacidades,
		EstadoEnlaceViewModel enlace,
		ConvertirBorradorEnIncidencia convertirBorrador,
		ISincronizadorIncidencias sincronizador)
	{
		_incidencias = incidencias;
		_catalogo = catalogo;
		_convertirBorrador = convertirBorrador;
		_sincronizador = sincronizador;
		_ubicacion = ubicacion;
		_capacidades = capacidades;
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

		await IntentarUbicarAsync();
		await RecargarBorradoresAsync();
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

		// La bandera evita que el propio GPS marque el kilómetro como capturado a mano.
		_asignandoDesdeGps = true;
		Kilometro = lectura.Valor;
		_asignandoDesdeGps = false;

		FuenteKilometro = KilometerSource.GPS;
	}

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
			TipoSeleccionado, kilometro, FuenteKilometro, SeveridadSeleccionada, nota);

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
			clave, TipoSeleccionado, Kilometro, FuenteKilometro, SeveridadSeleccionada, Nota);

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

		TipoSeleccionado = Tipos.FirstOrDefault(t => t.Id == borrador.TipoId);
		SeveridadSeleccionada = Severidades.FirstOrDefault(s => s.Id == borrador.SeveridadId);
		Nota = borrador.Nota;

		// El kilómetro se repone tal cual se guardó, aunque esté a medio escribir, y como
		// manual: reponerlo no es una lectura del GPS por mucho que lo fuera al capturarlo.
		Kilometro = borrador.Kilometro ?? string.Empty;
		FuenteKilometro = KilometerSource.Manual;
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

		return resultado.MotivoBloqueo switch
		{
			MotivoNoSincroniza.SinEnlaceConJacob =>
				$"{claveLocal} guardada sin conexión. Se enviará al recuperar la señal.",
			MotivoNoSincroniza.SinSesion =>
				$"{claveLocal} guardada. La sesión expiró: vuelva a ingresar para enviarla.",
			MotivoNoSincroniza.SinPermiso =>
				$"{claveLocal} guardada. Su cuenta no tiene autorizado sincronizar.",
			// Rechazo de Jacob: la incidencia está guardada y esperando en la cola, que es donde
			// el operador puede ver el motivo con detalle.
			_ => $"{claveLocal} guardada. El CCO no la aceptó todavía; revise la cola.",
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
	}
}
