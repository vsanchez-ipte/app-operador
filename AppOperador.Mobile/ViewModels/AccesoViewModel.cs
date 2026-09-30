using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

public sealed partial class AccesoViewModel : ObservableObject
{
	// Literales fijados por Producto: no se reformulan.
	private const string MensajeCredencialInvalida = "Usuario o contraseña no válidos";
	private const string MensajeSinPermiso = "No tiene permiso para utilizar la APK";
	private const string MensajeUbicacion = "Active la ubicación y autorice su uso para continuar";
	private const string MensajeSesionExpirada = "Sesión offline expirada";
	private const string MensajeSinComunicacion = "Sin conexión / Modo offline";

	// Provisionales: Producto aún no fija estos textos.
	private const string MensajeCuentaInactiva = "La cuenta está inactiva. Contacte al CCO";
	private const string MensajeCuentaBloqueada = "Cuenta bloqueada temporalmente. Intente más tarde";
	private const string MensajeSinUnidades = "No tiene unidades asignadas. Contacte al CCO";
	private const string MensajeErrorServicio = "No se pudo completar la validación. Intente de nuevo";

	private const string MensajeCredencialValidada = "Credenciales validadas por Jacob CCO";

	private const string MensajeElijaUnidad = "Elija la unidad con la que va a operar";

	private const string MensajeDesafioNoValido = "La sesión de acceso venció. Vuelva a iniciar sesión";
	private const string MensajeUnidadNoAutorizada = "La unidad ya no está disponible. Elija otra";

	// Acompañan al literal de ubicación, que no se toca, para decir cuál de los estados ocurrió.
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

	// Un texto por paso: el acceso son hasta tres esperas y cada una puede tardar.
	private const string PasoComprobandoEnlace = "Comprobando el enlace con Jacob CCO…";
	private const string PasoComprobandoUbicacion = "Comprobando la ubicación del dispositivo…";
	private const string PasoValidandoCredenciales = "Validando sus credenciales con Jacob CCO…";
	private const string PasoAbriendoSesion = "Abriendo la sesión con la unidad elegida…";
	private const string PasoReanudandoOffline = "Reanudando la última sesión guardada…";

	// Si Android mató el proceso, la sesión sigue guardada y «Continuar offline» no se lee como «reanudar».
	private const string FormatoSesionGuardada =
		"Hay una sesión guardada de {0}, vigente hasta el {1}. «Continuar offline» la reanuda "
		+ "sin volver a autenticarse.";

	private const string AccionPermitir = "Permitir ubicación";
	private const string AccionAjustesApp = "Abrir configuración de la app";
	private const string AccionAjustesUbicacion = "Abrir configuración de ubicación";
	private const string AccionRevalidar = "Volver a validar";

	private readonly ReanudarSesionOffline? _reanudarOffline;
	private readonly AvisoDeSesionTerminada _aviso;
	private readonly IAuthenticationService _autenticacion;
	private readonly IConnectivityService _conectividad;
	private readonly VerificarUbicacionParaAcceso _ubicacion;
	private readonly AbrirSesionMovil? _accesoJacob;

	// Nulo hasta la primera comprobación, para no avisar antes de que intente entrar.
	private ResultadoUbicacion? _ultimaUbicacion;

	[ObservableProperty]
	public partial string Usuario { get; set; }

	[ObservableProperty]
	public partial string Contrasena { get; set; }

	[ObservableProperty]
	public partial UnidadVehicular? UnidadSeleccionada { get; set; }

	[ObservableProperty]
	public partial string? MensajeError { get; set; }

	[ObservableProperty]
	public partial string? MensajeAviso { get; set; }

	[ObservableProperty]
	public partial string? AvisoSesionGuardada { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	// Sin esto la pantalla parece colgada. Va y se limpia junto con Ocupado.
	[ObservableProperty]
	public partial string? PasoEnCurso { get; set; }

	[ObservableProperty]
	public partial string? DetalleUbicacion { get; set; }

	[ObservableProperty]
	public partial string? TextoAccionUbicacion { get; set; }

	// Siempre que la ubicación bloquee: dos estados no ofrecen ninguna otra acción.
	[ObservableProperty]
	public partial bool PuedeRevalidarUbicacion { get; set; }

	public static string TextoRevalidarUbicacion => AccionRevalidar;

	public AccesoViewModel(
		IAuthenticationService autenticacion,
		IConnectivityService conectividad,
		VerificarUbicacionParaAcceso ubicacion,
		AvisoDeSesionTerminada aviso,
		AbrirSesionMovil? accesoJacob = null,
		ReanudarSesionOffline? reanudarOffline = null)
	{
		_aviso = aviso;
		_autenticacion = autenticacion;
		_conectividad = conectividad;
		_ubicacion = ubicacion;
		_accesoJacob = accesoJacob;
		_reanudarOffline = reanudarOffline;
		_conectividad.EnlaceCambio += (_, _) => OnPropertyChanged(nameof(TextoEstadoEnlace));

		Usuario = string.Empty;
		Contrasena = string.Empty;
	}

	// Observable: el catálogo llega después de enlazar la vista.
	public ObservableCollection<UnidadVehicular> Unidades { get; } = [];

	public bool HayError => !string.IsNullOrEmpty(MensajeError);

	public bool HayAviso => !string.IsNullOrEmpty(MensajeAviso);

	public bool HaySesionGuardada => !string.IsNullOrEmpty(AvisoSesionGuardada);

	public bool HayDetalleUbicacion => !string.IsNullOrEmpty(DetalleUbicacion);

	public bool HayAccionUbicacion => !string.IsNullOrEmpty(TextoAccionUbicacion);

	public bool HayPasoEnCurso => !string.IsNullOrEmpty(PasoEnCurso);

	public string TextoEstadoEnlace => _conectividad.HayEnlace ? "Enlace CCO activo" : MensajeSinComunicacion;

	// Al navegar y no al reaparecer (volver de Ajustes no debe borrar lo tecleado). Descarta el desafío.
	public async Task ReiniciarAsync()
	{
		_accesoJacob?.Descartar();

		Usuario = string.Empty;
		Contrasena = string.Empty;
		EnSeleccionDeUnidad = false;
		UnidadSeleccionada = null;
		Unidades.Clear();
		MensajeError = null;
		MensajeAviso = null;

		await InicializarAsync();

		// Cada vez: la sesión guardada pudo aparecer o cerrarse.
		AvisoSesionGuardada = await TextoSesionGuardadaAsync();

		// Si la sesión se cerró sola, hay que decir por qué.
		var motivo = _aviso.Consumir();
		if (motivo is not null)
		{
			MensajeError = TextoDe(motivo);
		}

		Ocupado = true;
		// Sin sesión, lo que se comprueba es la red. Se anuncia: es la primera espera y ocurre sola.
		PasoEnCurso = PasoComprobandoEnlace;
		try
		{
			await _conectividad.ComprobarAsync();
		}
		finally
		{
			PasoEnCurso = null;
			Ocupado = false;
		}

		OnPropertyChanged(nameof(TextoEstadoEnlace));
	}

	// Con el API real no se muestran unidades del simulador: parecerían las del operador.
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

	public bool UsaApiReal => _accesoJacob is not null;

	// Una sola pantalla con estados: el desafío no viaja entre vistas.
	[ObservableProperty]
	public partial bool EnSeleccionDeUnidad { get; set; }

	public bool MostrarCredenciales => !EnSeleccionDeUnidad;

	// Con el API real, solo en el segundo paso: antes no se conocen las unidades.
	public bool MostrarSelectorUnidad => !UsaApiReal || EnSeleccionDeUnidad;

	public string TextoBotonAcceso => EnSeleccionDeUnidad ? "Ingresar" : "Iniciar sesion";

	// Jacob autentica por correo, no por nombre de usuario.
	public string EtiquetaUsuario => UsaApiReal ? "CORREO" : "USUARIO";

	public string EjemploUsuario => UsaApiReal ? "operador@ipte.com.mx" : "usuario.campo";

	public Keyboard TecladoUsuario => UsaApiReal ? Keyboard.Email : Keyboard.Default;

	[RelayCommand]
	private async Task IngresarAsync()
	{
		Ocupado = true;
		try
		{
			// Sin ubicación no se continúa, y no se mandan credenciales a la red en vano.
			if (!await UbicacionAutorizadaAsync())
			{
				return;
			}

			// Segundo paso: el desafío sigue vigente, no se vuelve a preautenticar.
			if (EnSeleccionDeUnidad)
			{
				PasoEnCurso = PasoAbriendoSesion;
				await AbrirSesionAsync();
				return;
			}

			if (_accesoJacob is not null)
			{
				PasoEnCurso = PasoValidandoCredenciales;
				await IdentificarAsync();
				return;
			}

			if (UnidadSeleccionada is null)
			{
				return;
			}

			PasoEnCurso = PasoValidandoCredenciales;
			var resultado = await _autenticacion.IngresarAsync(Usuario, Contrasena, UnidadSeleccionada);
			await ProcesarResultadoAsync(resultado);
		}
		finally
		{
			PasoEnCurso = null;
			Ocupado = false;
		}
	}

	// El desafío lo retiene el caso de uso, lejos de cualquier binding.
	private async Task IdentificarAsync()
	{
		var resultado = await _accesoJacob!.IdentificarAsync(Usuario, Contrasena);

		if (!resultado.Exitoso)
		{
			MensajeError = TextoDe(resultado.Motivo);
			return;
		}

		// El API debió responder sin.vehiculos; si no, el operador quedaría ante un desplegable vacío.
		if (resultado.Unidades.Count == 0)
		{
			_accesoJacob.Descartar();
			MensajeError = MensajeSinUnidades;
			return;
		}

		Contrasena = string.Empty;
		MensajeError = null;

		MostrarUnidades(resultado.Unidades);
		MensajeAviso = $"{MensajeCredencialValidada}. {MensajeElijaUnidad}";
	}

	private void MostrarUnidades(IReadOnlyList<UnidadVehicular> unidades)
	{
		Unidades.Clear();
		foreach (var unidad in unidades)
		{
			Unidades.Add(unidad);
		}

		// Preseleccionar evita que «Ingresar» no haga nada; sigue siendo del catálogo.
		UnidadSeleccionada = Unidades.FirstOrDefault();
		EnSeleccionDeUnidad = true;
	}

	// Desafío vencido vuelve a credenciales; unidad no autorizada solo pide elegir otra.
	private async Task AbrirSesionAsync()
	{
		if (UnidadSeleccionada is null)
		{
			MensajeError = MensajeSinUnidades;
			return;
		}

		var resultado = await _accesoJacob!.AbrirAsync(UnidadSeleccionada);

		if (!resultado.Exitoso)
		{
			MensajeError = TextoDe(resultado.Motivo);

			if (resultado.Motivo == MotivoRechazoAcceso.DesafioNoValido)
			{
				VolverACredenciales();
				MensajeError = MensajeDesafioNoValido;
			}

			return;
		}

		Contrasena = string.Empty;
		MensajeError = null;
		MensajeAviso = null;

		await Shell.Current.GoToAsync("//principal/inicio");
	}

	[RelayCommand]
	// Sin esta salida, el segundo paso sería un callejón: el desafío vence a los cinco minutos.
	private void VolverACredenciales()
	{
		_accesoJacob?.Descartar();
		EnSeleccionDeUnidad = false;
		UnidadSeleccionada = null;
		Unidades.Clear();
		Contrasena = string.Empty;
		MensajeError = null;
		MensajeAviso = null;
	}

	[RelayCommand]
	private async Task ContinuarSinConexionAsync()
	{
		Ocupado = true;
		try
		{
			// Reanudar también abre sesión, así que también exige ubicación.
			if (!await UbicacionAutorizadaAsync())
			{
				return;
			}

			// Con canal real se mide la sesión guardada sin fiarse del reloj; sin él, el simulador.
			PasoEnCurso = PasoReanudandoOffline;
			var resultado = _reanudarOffline is not null
				? await _reanudarOffline.ReanudarAsync()
				: await _autenticacion.ContinuarSinConexionAsync();

			await ProcesarResultadoAsync(resultado);

			// Si no se pudo reanudar, el aviso ya no es cierto.
			if (!resultado.Autorizado)
			{
				AvisoSesionGuardada = null;
			}
		}
		finally
		{
			PasoEnCurso = null;
			Ocupado = false;
		}
	}

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

			// No se reevalúa aquí: la app queda en segundo plano; lo hace RevisarUbicacionAsync al volver.
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

	// Solo si había un bloqueo: no se avisa de un permiso que aún no se ha pedido.
	public async Task RevisarUbicacionAsync()
	{
		if (_ultimaUbicacion is null || _ultimaUbicacion.PermiteAcceder)
		{
			return;
		}

		AplicarUbicacion(await _ubicacion.RevisarAsync());
	}

	[RelayCommand]
	private async Task RevalidarUbicacionAsync()
	{
		Ocupado = true;
		try
		{
			await RevisarUbicacionAsync();
		}
		finally
		{
			Ocupado = false;
		}
	}

	// Anuncia el paso: puede tardar mientras el diálogo del sistema espera respuesta.
	private async Task<bool> UbicacionAutorizadaAsync()
	{
		PasoEnCurso = PasoComprobandoUbicacion;

		var resultado = await _ubicacion.ExigirAsync();
		AplicarUbicacion(resultado);

		return resultado.PermiteAcceder;
	}

	private void AplicarUbicacion(ResultadoUbicacion resultado)
	{
		_ultimaUbicacion = resultado;

		if (resultado.PermiteAcceder)
		{
			// Solo se retira el rechazo por ubicación; otro error sigue siendo cierto.
			if (string.Equals(MensajeError, MensajeUbicacion, StringComparison.Ordinal))
			{
				MensajeError = null;
			}

			DetalleUbicacion = null;
			TextoAccionUbicacion = null;
			PuedeRevalidarUbicacion = false;
			return;
		}

		// El literal no se toca; el detalle y el botón distinguen el estado.
		MensajeError = MensajeUbicacion;
		DetalleUbicacion = DetalleDe(resultado.Estado);
		TextoAccionUbicacion = TextoAccionDe(resultado.Accion);

		// Siempre hay cómo volver a comprobar, aunque el estado no ofrezca otra acción.
		PuedeRevalidarUbicacion = true;
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

	// Con fecha: una ventana de ocho horas cruza la medianoche con frecuencia.
	private async Task<string?> TextoSesionGuardadaAsync()
	{
		if (_reanudarOffline is null)
		{
			return null;
		}

		var guardada = await _reanudarOffline.ConsultarGuardadaAsync();

		return guardada is null
			? null
			: string.Format(
				FormatoSesionGuardada,
				guardada.Operador,
				guardada.VenceUtc.ToLocalTime().ToString("dd/MM/yyyy, hh:mm tt"));
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
		MotivoRechazoAcceso.DesafioNoValido => MensajeDesafioNoValido,
		MotivoRechazoAcceso.UnidadNoAutorizada => MensajeUnidadNoAutorizada,
		MotivoRechazoAcceso.ErrorDelServicio => MensajeErrorServicio,
		// Un motivo fuera de la lista es un descuido del código, no una credencial mala.
		_ => MensajeErrorServicio,
	};

	// Error y aviso son excluyentes: los dos a la vez dejarían al operador sin saber qué pasó.
	partial void OnMensajeErrorChanged(string? value)
	{
		OnPropertyChanged(nameof(HayError));
		if (!string.IsNullOrEmpty(value))
		{
			MensajeAviso = null;
		}

		// El detalle y el botón de ubicación solo valen junto a su literal.
		if (!string.Equals(value, MensajeUbicacion, StringComparison.Ordinal))
		{
			DetalleUbicacion = null;
			TextoAccionUbicacion = null;
			PuedeRevalidarUbicacion = false;
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

	partial void OnAvisoSesionGuardadaChanged(string? value) => OnPropertyChanged(nameof(HaySesionGuardada));

	partial void OnPasoEnCursoChanged(string? value) => OnPropertyChanged(nameof(HayPasoEnCurso));

	partial void OnDetalleUbicacionChanged(string? value) => OnPropertyChanged(nameof(HayDetalleUbicacion));

	partial void OnTextoAccionUbicacionChanged(string? value) => OnPropertyChanged(nameof(HayAccionUbicacion));

	partial void OnEnSeleccionDeUnidadChanged(bool value)
	{
		OnPropertyChanged(nameof(MostrarCredenciales));
		OnPropertyChanged(nameof(MostrarSelectorUnidad));
		OnPropertyChanged(nameof(TextoBotonAcceso));
	}
}
