using System.Collections.ObjectModel;
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

	private readonly IAuthenticationService _autenticacion;
	private readonly IConnectivityService _conectividad;
	private readonly IPreauthClient? _preauth;

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

	/// <param name="preauth">
	/// Preautenticación real contra Jacob CCO. Es opcional a propósito: solo se registra
	/// cuando <c>ConfiguracionApi.UsarApiReal</c> está encendido. Si es nulo, la pantalla
	/// funciona íntegramente contra el simulador, que es como se demuestran las cinco
	/// pantallas mientras el canal móvil no esté desplegado.
	/// </param>
	public AccesoViewModel(
		IAuthenticationService autenticacion,
		IConnectivityService conectividad,
		IPreauthClient? preauth = null)
	{
		_autenticacion = autenticacion;
		_conectividad = conectividad;
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
			var resultado = await _autenticacion.ContinuarSinConexionAsync();
			await ProcesarResultadoAsync(resultado);
		}
		finally
		{
			Ocupado = false;
		}
	}

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
	}

	partial void OnMensajeAvisoChanged(string? value)
	{
		OnPropertyChanged(nameof(HayAviso));
		if (!string.IsNullOrEmpty(value))
		{
			MensajeError = null;
		}
	}
}
