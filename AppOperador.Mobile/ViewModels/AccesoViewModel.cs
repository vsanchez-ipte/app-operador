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

	private readonly IAuthenticationService _autenticacion;
	private readonly IConnectivityService _conectividad;

	[ObservableProperty]
	public partial string Usuario { get; set; }

	[ObservableProperty]
	public partial string Contrasena { get; set; }

	[ObservableProperty]
	public partial UnidadVehicular? UnidadSeleccionada { get; set; }

	[ObservableProperty]
	public partial string? MensajeError { get; set; }

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	public AccesoViewModel(IAuthenticationService autenticacion, IConnectivityService conectividad)
	{
		_autenticacion = autenticacion;
		_conectividad = conectividad;
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

	/// <summary>Estado de comunicación con Jacob CCO, visible en la pantalla.</summary>
	public string TextoEstadoEnlace => _conectividad.HayEnlace ? "Enlace CCO activo" : MensajeSinComunicacion;

	/// <summary>Carga el catálogo de unidades al abrir la pantalla.</summary>
	public async Task InicializarAsync()
	{
		if (Unidades.Count > 0)
		{
			return;
		}

		foreach (var unidad in await _autenticacion.ObtenerUnidadesAsync())
		{
			Unidades.Add(unidad);
		}

		UnidadSeleccionada = Unidades.FirstOrDefault();
	}

	[RelayCommand]
	private async Task IngresarAsync()
	{
		if (UnidadSeleccionada is null)
		{
			return;
		}

		Ocupado = true;
		try
		{
			var resultado = await _autenticacion.IngresarAsync(Usuario, Contrasena, UnidadSeleccionada);
			await ProcesarResultadoAsync(resultado);
		}
		finally
		{
			Ocupado = false;
		}
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
		_ => MensajeCredencialInvalida,
	};

	// El generador de CommunityToolkit.Mvvm llama a este método al cambiar MensajeError;
	// así HayError se recalcula sin que la vista tenga que enterarse de los dos nombres.
	partial void OnMensajeErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));
}
