using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Pantalla de acceso: credenciales, unidad y estado de comunicación.
/// </summary>
/// <remarks>
/// Los mensajes de rechazo son los literales que exige JTT-279 y permanecen visibles
/// hasta que el operador corrija los datos o vuelva a intentar.
/// </remarks>
public sealed partial class AccesoViewModel : ObservableObject
{
	// Textos fijados por JTT-279. No se reformulan ni se traducen.
	private const string MensajeCredencialInvalida = "Usuario o contraseña no válidos";
	private const string MensajeSinPermiso = "No tiene permiso para utilizar la APK";
	private const string MensajeUbicacion = "Active la ubicación y autorice su uso para continuar";
	private const string MensajeSesionExpirada = "Sesión offline expirada";
	private const string MensajeSinComunicacion = "Sin conexión / Modo offline";

	// Textos provisionales: JTT-279 no fijó literal para estos casos y el contrato del API
	// los deja como pregunta abierta. Hay que acordarlos con Producto antes de QA.
	private const string MensajeCuentaInactiva = "La cuenta está inactiva. Contacte al CCO";
	private const string MensajeCuentaBloqueada = "Cuenta bloqueada temporalmente. Intente más tarde";
	private const string MensajeSinUnidades = "No tiene unidades asignadas. Contacte al CCO";
	private const string MensajeErrorServicio = "No se pudo completar la validación. Intente de nuevo";

	// JTT-1378 se detiene tras la preautenticación: no hay sesión que abrir todavía.
	private const string MensajeCredencialValidada = "Credenciales validadas por Jacob CCO";

	// Detalle que acompaña al literal de ubicación (JTT-1380). El literal de JTT-279 es el
	// que revisa QA y no se toca; esto explica *cuál* de los seis estados se encontró, que
	// es lo que le dice al operador qué hacer.
	private const string DetalleNoSolicitado =
		"La app necesita su permiso para usar la ubicación del dispositivo.";
	private const string DetalleRechazado =
		"El permiso de ubicación está rechazado. Concédalo para continuar.";
	private const string DetalleBloqueado =
		"El permiso de ubicación quedó bloqueado. Actívelo desde la configuración de la app.";
	private const string DetalleServicioApagado =
		"El servicio de ubicación del dispositivo está apagado. Enciéndalo para continuar.";
	private const string DetalleSinUbicacion =
		"Este dispositivo no cuenta con servicio de ubicación, así que no es posible completar el acceso.";
	private const string DetalleErrorUbicacion =
		"No se pudo verificar el estado de la ubicación. Intente de nuevo.";
	private const string DetalleAjustesNoAbrieron =
		"No se pudo abrir la configuración del dispositivo. Ábrala manualmente y vuelva a la app.";

	private const string AccionPermitir = "Permitir ubicación";
	private const string AccionAjustesApp = "Abrir configuración de la app";
	private const string AccionAjustesUbicacion = "Abrir configuración de ubicación";

	private readonly IAuthenticationService _autenticacion;
	private readonly IConnectivityService _conectividad;
	private readonly VerificarUbicacionParaAcceso _ubicacion;
	private readonly IPreauthClient? _preauth;

	// Último veredicto de ubicación. Nulo mientras no se haya comprobado nada: sirve para no
	// molestar al operador con el aviso antes de que intente entrar.
	private ResultadoUbicacion? _ultimaUbicacion;

	// El desafío vive solo en memoria y solo mientras dura la pantalla: es la credencial
	// del segundo paso del acceso y no puede registrarse ni persistirse (JTT-1378 §7).
	private string? _desafioVigente;

	[ObservableProperty]
	public partial string Usuario { get; set; }

	[ObservableProperty]
	public partial string Contrasena { get; set; }

	[ObservableProperty]
	public partial UnidadVehicular? UnidadSeleccionada { get; set; }

	[ObservableProperty]
	public partial string? MensajeError { get; set; }

	/// <summary>
	/// Aviso informativo, no de rechazo. Hoy solo lo usa la preautenticación real para
	/// confirmar que las credenciales fueron aceptadas.
	/// </summary>
	[ObservableProperty]
	public partial string? MensajeAviso { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	/// <summary>
	/// Explicación del estado de ubicación encontrado. Acompaña al literal de JTT-279, que
	/// por sí solo no distingue un permiso rechazado de un GPS apagado.
	/// </summary>
	[ObservableProperty]
	public partial string? DetalleUbicacion { get; set; }

	/// <summary>
	/// Texto del botón que resuelve el bloqueo de ubicación. Nulo cuando no hay nada que el
	/// operador pueda hacer desde la app.
	/// </summary>
	[ObservableProperty]
	public partial string? TextoAccionUbicacion { get; set; }

	/// <param name="ubicacion">
	/// Comprobación del prerrequisito de ubicación (JTT-279 PR3, JTT-1380). Se ejecuta antes
	/// de cualquier camino de acceso, en línea o sin conexión.
	/// </param>
	/// <param name="preauth">
	/// Preautenticación real contra Jacob CCO. Es opcional a propósito: solo se registra
	/// cuando <c>ConfiguracionApi.UsarApiReal</c> está encendido. Si es nulo, la pantalla
	/// funciona íntegramente contra el simulador, que es como se demuestran las cinco
	/// pantallas mientras el canal móvil no esté desplegado.
	/// </param>
	public AccesoViewModel(
		IAuthenticationService autenticacion,
		IConnectivityService conectividad,
		VerificarUbicacionParaAcceso ubicacion,
		IPreauthClient? preauth = null)
	{
		_autenticacion = autenticacion;
		_conectividad = conectividad;
		_ubicacion = ubicacion;
		_preauth = preauth;
		_conectividad.EnlaceCambio += (_, _) => OnPropertyChanged(nameof(TextoEstadoEnlace));

		Usuario = string.Empty;
		Contrasena = string.Empty;
	}

	/// <summary>
	/// Unidades del catálogo vehicular. No se admite texto libre (JTT-279).
	/// </summary>
	/// <remarks>
	/// Observable a propósito: el catálogo se carga después de que la vista ya se enlazó,
	/// y con una lista simple el desplegable se quedaría vacío.
	/// </remarks>
	public ObservableCollection<UnidadVehicular> Unidades { get; } = [];

	/// <summary>Indica si hay algún mensaje de rechazo que mostrar.</summary>
	public bool HayError => !string.IsNullOrEmpty(MensajeError);

	/// <summary>Indica si hay un aviso informativo que mostrar.</summary>
	public bool HayAviso => !string.IsNullOrEmpty(MensajeAviso);

	/// <summary>Indica si hay explicación del estado de ubicación que mostrar.</summary>
	public bool HayDetalleUbicacion => !string.IsNullOrEmpty(DetalleUbicacion);

	/// <summary>Indica si hay un botón de ubicación que ofrecer.</summary>
	public bool HayAccionUbicacion => !string.IsNullOrEmpty(TextoAccionUbicacion);

	/// <summary>Estado de comunicación con Jacob CCO, visible en la pantalla.</summary>
	public string TextoEstadoEnlace => _conectividad.HayEnlace ? "Enlace CCO activo" : MensajeSinComunicacion;

	/// <summary>Carga el catálogo de unidades al abrir la pantalla.</summary>
	/// <remarks>
	/// Con el API real no se carga nada: las unidades verdaderas las devuelve la
	/// preautenticación y JTT-1378 no las muestra. Enseñar mientras tanto las del simulador
	/// daría a entender que son las del operador, que es peor que no mostrar ninguna.
	/// </remarks>
	public async Task InicializarAsync()
	{
		if (UsaApiReal || Unidades.Count > 0)
		{
			return;
		}

		foreach (var unidad in await _autenticacion.ObtenerUnidadesAsync())
		{
			Unidades.Add(unidad);
		}

		UnidadSeleccionada = Unidades.FirstOrDefault();
	}

	/// <summary>
	/// Indica si la pantalla habla con Jacob CCO de verdad.
	/// </summary>
	/// <remarks>
	/// La vista lo usa para avisar que el acceso se detiene tras validar credenciales, sin
	/// entrar a la app: el segundo paso llega en una historia posterior.
	/// </remarks>
	public bool UsaApiReal => _preauth is not null;

	/// <summary>Etiqueta del primer campo.</summary>
	/// <remarks>
	/// Jacob CCO autentica por <b>email</b>, no por nombre de usuario: la columna
	/// <c>usuario</c> existe en la base pero el login no la consulta. Contra el API real
	/// hay que pedir el correo; contra el simulador se conserva el texto de la maqueta.
	/// </remarks>
	public string EtiquetaUsuario => UsaApiReal ? "CORREO" : "USUARIO";

	/// <summary>Texto de ejemplo del primer campo, acorde a lo que se pide.</summary>
	public string EjemploUsuario => UsaApiReal ? "operador@ipte.com.mx" : "usuario.campo";

	/// <summary>Teclado del primer campo: el de correo incluye la arroba.</summary>
	public Keyboard TecladoUsuario => UsaApiReal ? Keyboard.Email : Keyboard.Default;

	[RelayCommand]
	private async Task IngresarAsync()
	{
		Ocupado = true;
		try
		{
			// JTT-279 PR3: sin ubicación habilitada y autorizada no se continúa. Se comprueba
			// antes de mandar nada a Jacob CCO — si el acceso no puede completarse, no tiene
			// sentido poner las credenciales del operador en la red.
			if (!await UbicacionAutorizadaAsync())
			{
				return;
			}

			if (_preauth is not null)
			{
				await PreautenticarAsync();
				return;
			}

			if (UnidadSeleccionada is null)
			{
				return;
			}

			var resultado = await _autenticacion.IngresarAsync(Usuario, Contrasena, UnidadSeleccionada);
			await ProcesarResultadoAsync(resultado);
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>
	/// Primer paso del acceso real: valida credenciales y obtiene el desafío.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Aquí termina el alcance de JTT-1378. Con el desafío en mano <b>no</b> se consume
	/// <c>POST ITS/AppLogin</c>, no se muestran las unidades recibidas, no se elige unidad y
	/// no se abre sesión: todo eso pertenece a historias posteriores.
	/// </para>
	/// <para>
	/// Ni el desafío ni las unidades se muestran en pantalla. El operador solo ve que sus
	/// credenciales fueron aceptadas.
	/// </para>
	/// </remarks>
	private async Task PreautenticarAsync()
	{
		var resultado = await _preauth!.PreautenticarAsync(Usuario, Contrasena);

		if (!resultado.Exitoso)
		{
			_desafioVigente = null;
			MensajeError = TextoDe(resultado.Motivo);
			return;
		}

		_desafioVigente = resultado.ChallengeId;
		Contrasena = string.Empty;
		MensajeError = null;
		MensajeAviso = MensajeCredencialValidada;
	}

	[RelayCommand]
	private async Task ContinuarSinConexionAsync()
	{
		Ocupado = true;
		try
		{
			// El prerrequisito es del dispositivo, no de la red: reanudar sin conexión también
			// abre sesión, así que también exige ubicación.
			if (!await UbicacionAutorizadaAsync())
			{
				return;
			}

			var resultado = await _autenticacion.ContinuarSinConexionAsync();
			await ProcesarResultadoAsync(resultado);
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>
	/// Ejecuta lo que corresponda al estado de ubicación: pedir el permiso o llevar al
	/// operador a la configuración del sistema.
	/// </summary>
	[RelayCommand]
	private async Task EjecutarAccionUbicacionAsync()
	{
		if (_ultimaUbicacion is null)
		{
			return;
		}

		Ocupado = true;
		try
		{
			if (_ultimaUbicacion.Accion == AccionUbicacion.SolicitarPermiso)
			{
				AplicarUbicacion(await _ubicacion.SolicitarPermisoAsync());
				return;
			}

			// Al volver de la configuración no se puede reevaluar aquí: la app queda en
			// segundo plano. De eso se encarga RevisarUbicacionAsync al reaparecer la pantalla.
			if (!await _ubicacion.AbrirAjustesAsync(_ultimaUbicacion.Accion))
			{
				DetalleUbicacion = DetalleAjustesNoAbrieron;
			}
		}
		finally
		{
			Ocupado = false;
		}
	}

	/// <summary>
	/// Vuelve a evaluar la ubicación al reaparecer la pantalla.
	/// </summary>
	/// <remarks>
	/// Es lo que cierra el caso de "cambié el permiso en la configuración y volví": el
	/// bloqueo desaparece solo, sin reiniciar la app. No se comprueba nada si la pantalla no
	/// estaba mostrando un bloqueo, para no advertir de un permiso que todavía no se ha
	/// pedido: eso ocurre cuando el operador pulsa Iniciar sesión, no al abrir la pantalla.
	/// </remarks>
	public async Task RevisarUbicacionAsync()
	{
		if (_ultimaUbicacion is null || _ultimaUbicacion.PermiteAcceder)
		{
			return;
		}

		AplicarUbicacion(await _ubicacion.RevisarAsync());
	}

	/// <summary>Comprueba el prerrequisito y deja la pantalla contando lo que encontró.</summary>
	private async Task<bool> UbicacionAutorizadaAsync()
	{
		var resultado = await _ubicacion.ExigirAsync();
		AplicarUbicacion(resultado);

		return resultado.PermiteAcceder;
	}

	/// <summary>Vuelca un veredicto de ubicación en la pantalla.</summary>
	private void AplicarUbicacion(ResultadoUbicacion resultado)
	{
		_ultimaUbicacion = resultado;

		if (resultado.PermiteAcceder)
		{
			// Solo se retira el rechazo por ubicación: si en pantalla hay otro error, sigue
			// siendo cierto y no lo borra haber concedido el permiso.
			if (string.Equals(MensajeError, MensajeUbicacion, StringComparison.Ordinal))
			{
				MensajeError = null;
			}

			DetalleUbicacion = null;
			TextoAccionUbicacion = null;
			return;
		}

		// El literal lo fija JTT-279 y es el que revisa QA; el detalle y el botón son los que
		// distinguen los estados que pide JTT-1380.
		MensajeError = MensajeUbicacion;
		DetalleUbicacion = DetalleDe(resultado.Estado);
		TextoAccionUbicacion = TextoAccionDe(resultado.Accion);
	}

	private static string DetalleDe(EstadoUbicacion estado) => estado switch
	{
		EstadoUbicacion.NoSolicitado => DetalleNoSolicitado,
		EstadoUbicacion.Rechazado => DetalleRechazado,
		EstadoUbicacion.BloqueadoPermanentemente => DetalleBloqueado,
		EstadoUbicacion.ServicioDesactivado => DetalleServicioApagado,
		EstadoUbicacion.NoDisponibleEnElDispositivo => DetalleSinUbicacion,
		_ => DetalleErrorUbicacion,
	};

	private static string? TextoAccionDe(AccionUbicacion accion) => accion switch
	{
		AccionUbicacion.SolicitarPermiso => AccionPermitir,
		AccionUbicacion.AbrirAjustesDeLaApp => AccionAjustesApp,
		AccionUbicacion.AbrirAjustesDeUbicacion => AccionAjustesUbicacion,
		_ => null,
	};

	private async Task ProcesarResultadoAsync(ResultadoAcceso resultado)
	{
		if (resultado.Autorizado)
		{
			MensajeError = null;
			Contrasena = string.Empty;
			await Shell.Current.GoToAsync("//principal/inicio");
			return;
		}

		MensajeError = TextoDe(resultado.Motivo);
	}

	private static string TextoDe(MotivoRechazoAcceso? motivo) => motivo switch
	{
		MotivoRechazoAcceso.CredencialInvalida => MensajeCredencialInvalida,
		MotivoRechazoAcceso.SinPermiso => MensajeSinPermiso,
		MotivoRechazoAcceso.UbicacionNoDisponible => MensajeUbicacion,
		MotivoRechazoAcceso.SesionOfflineExpirada => MensajeSesionExpirada,
		MotivoRechazoAcceso.SinComunicacion => MensajeSinComunicacion,
		MotivoRechazoAcceso.CuentaInactiva => MensajeCuentaInactiva,
		MotivoRechazoAcceso.CuentaBloqueada => MensajeCuentaBloqueada,
		MotivoRechazoAcceso.SinUnidades => MensajeSinUnidades,
		MotivoRechazoAcceso.ErrorDelServicio => MensajeErrorServicio,
		// Un motivo que no esté en la lista es un descuido de programación, no una
		// credencial mala: decirle al operador que se equivocó sería mentirle.
		_ => MensajeErrorServicio,
	};

	// El generador de CommunityToolkit.Mvvm llama a estos métodos al cambiar cada mensaje;
	// así las banderas se recalculan sin que la vista tenga que enterarse de los dos nombres.
	// Los dos mensajes son excluyentes: mostrar a la vez un rechazo y una confirmación
	// dejaría al operador sin saber qué pasó.
	partial void OnMensajeErrorChanged(string? value)
	{
		OnPropertyChanged(nameof(HayError));
		if (!string.IsNullOrEmpty(value))
		{
			MensajeAviso = null;
		}

		// El detalle y el botón de ubicación solo tienen sentido junto a su literal. Si el
		// mensaje pasa a ser otro —o ninguno—, se retiran: un botón "Permitir ubicación"
		// debajo de "Usuario o contraseña no válidos" señalaría al problema equivocado.
		if (!string.Equals(value, MensajeUbicacion, StringComparison.Ordinal))
		{
			DetalleUbicacion = null;
			TextoAccionUbicacion = null;
		}
	}

	partial void OnMensajeAvisoChanged(string? value)
	{
		OnPropertyChanged(nameof(HayAviso));
		if (!string.IsNullOrEmpty(value))
		{
			MensajeError = null;
		}
	}

	partial void OnDetalleUbicacionChanged(string? value) => OnPropertyChanged(nameof(HayDetalleUbicacion));

	partial void OnTextoAccionUbicacionChanged(string? value) => OnPropertyChanged(nameof(HayAccionUbicacion));
}
