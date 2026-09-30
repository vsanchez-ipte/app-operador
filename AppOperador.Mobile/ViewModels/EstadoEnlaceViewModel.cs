using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AppOperador.Mobile.ViewModels;

// Singleton: el aviso tiene que verse igual en todas las pantallas.
public sealed partial class EstadoEnlaceViewModel : ObservableObject
{
	public const string TextoModoOffline = "Sin conexión / Modo offline";

	public const string TextoEnLinea = "En línea";

	public const string TextoRevalidando = "Revalidando";

	public const string TextoErrorDeServicio = "Error de servicio";

	private const string MensajeSinComunicacion = "Sigue sin haber comunicación con Jacob CCO.";
	private const string MensajeRevalidada = "Enlace recuperado. Sesión revalidada.";
	private const string MensajeSesionNoValida = "La sesión ya no es válida. Vuelva a iniciar sesión.";

	// Hubo respuesta, así que no es «sin conexión». Lleva el código para soporte.
	private const string MensajeServidorConError =
		"Jacob CCO respondió con un error ({0}). No es su conexión: repórtelo a soporte.";

	// Texto fijo: el mensaje de una excepción no le sirve al operador.
	private const string MensajeFalloInesperado =
		"No se pudo comprobar el enlace. Intente de nuevo.";

	private readonly IConnectivityService _conectividad;
	private readonly ISessionStore _sesiones;
	private readonly ComprobarVigenciaOffline _vigencia;
	private readonly ILogger<EstadoEnlaceViewModel> _registro;
	private readonly RevalidarSesionMovil? _revalidar;

	// Sin enlace, que el servidor haya contestado o no cambia lo que el operador debe hacer.
	private CausaSinEnlace _ultimaCausa = CausaSinEnlace.Ninguna;

	public EstadoEnlaceViewModel(
		IConnectivityService conectividad,
		ISessionStore sesiones,
		ComprobarVigenciaOffline vigencia,
		ILogger<EstadoEnlaceViewModel> registro,
		RevalidarSesionMovil? revalidar = null)
	{
		_conectividad = conectividad;
		_sesiones = sesiones;
		_vigencia = vigencia;
		_registro = registro;
		_revalidar = revalidar;

		_conectividad.EnlaceCambio += AlCambiarElEnlace;
	}

	public bool HayEnlace => _conectividad.HayEnlace;

	public EstadoComunicacion Estado =>
		ReglaEstadoComunicacion.Determinar(_conectividad.HayEnlace, Ocupado, _ultimaCausa);

	public string TextoEstado => Estado switch
	{
		EstadoComunicacion.EnLinea => TextoEnLinea,
		EstadoComunicacion.Revalidando => TextoRevalidando,
		EstadoComunicacion.ErrorDeServicio => TextoErrorDeServicio,
		_ => TextoModoOffline,
	};

	// Banderas y no estilos: la apariencia la decide la vista.
	public bool EstaEnLinea => Estado == EstadoComunicacion.EnLinea;

	public bool EstaSinConexion => Estado == EstadoComunicacion.SinConexion;

	public bool EstaRevalidando => Estado == EstadoComunicacion.Revalidando;

	public bool EstaEnErrorDeServicio => Estado == EstadoComunicacion.ErrorDeServicio;

	// Con fecha, porque la ventana puede vencer al día siguiente.
	public string? ExpiraEl =>
		_sesiones.Actual?.Vigencia.OfflineUntilUtc.ToLocalTime().ToString("dd/MM/yyyy, hh:mm tt");

	public bool HayExpiracion => ExpiraEl is not null;

	// Solo con sesión: sin ella, la pantalla de acceso ya dice lo suyo.
	public bool EnModoOffline => !_conectividad.HayEnlace && _sesiones.Actual is not null;

	public string Texto => TextoModoOffline;

	public bool PuedeReintentar => !Ocupado;

	[ObservableProperty]
	public partial bool Ocupado { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HayDetalle))]
	public partial string? Detalle { get; set; }

	public bool HayDetalle => !string.IsNullOrEmpty(Detalle);

	// Primero la sonda liviana y solo con enlace la revalidación.
	[RelayCommand]
	private async Task ReintentarAsync()
	{
		if (Ocupado)
		{
			return;
		}

		Ocupado = true;
		OnPropertyChanged(nameof(PuedeReintentar));
		try
		{
			// Esto puede levantar la recuperación; con Ocupado encendido, esa vía no revalida.
			var sondeo = await _conectividad.ComprobarAsync();
			_ultimaCausa = sondeo.Causa;

			if (!sondeo.HayEnlace)
			{
				Detalle = ExplicarSondeo(sondeo);
				return;
			}

			if (_revalidar is null || _sesiones.Actual is null)
			{
				Detalle = "Enlace recuperado.";
				return;
			}

			var resultado = await _revalidar.RevalidarAsync();

			Detalle = resultado switch
			{
				{ Exitoso: true } => MensajeRevalidada,
				{ EsRechazoDefinitivo: true } => MensajeSesionNoValida,
				_ => MensajeSinComunicacion,
			};
		}
		catch (Exception excepcion)
		{
			// Un reintento fallido no debe cerrar la app.
			_registro.LogError(excepcion, "Falló el reintento manual del enlace con Jacob CCO.");
			Detalle = MensajeFalloInesperado;
		}
		finally
		{
			Ocupado = false;
			Refrescar();
		}
	}

	private string ExplicarSondeo(ResultadoSondeo sondeo)
	{
		_registro.LogWarning(
			"Sondeo del enlace sin éxito. Causa: {Causa}. Código: {Codigo}. Detalle: {Detalle}",
			sondeo.Causa,
			sondeo.CodigoHttp,
			sondeo.Detalle);

		return sondeo.Causa switch
		{
			CausaSinEnlace.RespuestaDeError =>
				string.Format(MensajeServidorConError, sondeo.CodigoHttp),
			CausaSinEnlace.SesionRechazada => MensajeSesionNoValida,
			_ => MensajeSinComunicacion,
		};
	}

	// La llaman las pantallas al aparecer; no hay temporizador que gaste batería.
	public async Task<bool> ComprobarSesionAsync()
	{
		var estado = await _vigencia.ComprobarAsync();

		Refrescar();

		if (estado == EstadoVigenciaSesion.Expirada)
		{
			await Shell.Current.GoToAsync("//acceso");
			return false;
		}

		// Sin esperar: el estado guardado puede ser viejo si el servidor cayó sin cambiar la red.
		_ = ComprobarElEnlaceAsync();
		return true;
	}

	// No enciende Ocupado: impediría revalidar si este sondeo detecta la recuperación.
	public async Task<bool> ComprobarElEnlaceAsync()
	{
		if (_sesiones.Actual is null)
		{
			return false;
		}

		try
		{
			var sondeo = await _conectividad.ComprobarAsync();
			_ultimaCausa = sondeo.Causa;
			return sondeo.HayEnlace;
		}
		catch (Exception excepcion)
		{
			// Nadie espera el resultado: una excepción aquí cerraría la app.
			_registro.LogError(excepcion, "Falló el sondeo del enlace con Jacob CCO.");
			return false;
		}
		finally
		{
			Refrescar();
		}
	}

	public void Refrescar()
	{
		OnPropertyChanged(nameof(HayEnlace));
		OnPropertyChanged(nameof(EnModoOffline));
		OnPropertyChanged(nameof(PuedeReintentar));
		OnPropertyChanged(nameof(ExpiraEl));
		OnPropertyChanged(nameof(HayExpiracion));
		NotificarEstado();
	}

	private void NotificarEstado()
	{
		OnPropertyChanged(nameof(Estado));
		OnPropertyChanged(nameof(TextoEstado));
		OnPropertyChanged(nameof(EstaEnLinea));
		OnPropertyChanged(nameof(EstaSinConexion));
		OnPropertyChanged(nameof(EstaRevalidando));
		OnPropertyChanged(nameof(EstaEnErrorDeServicio));
	}

	partial void OnOcupadoChanged(bool value) => NotificarEstado();

	private void AlCambiarElEnlace(object? origen, bool hayEnlace)
	{
		// La causa anterior deja de valer; si no, un «Error de servicio» viejo seguiría pintado.
		_ultimaCausa = CausaSinEnlace.Ninguna;

		// El detalle era de la última vez con enlace y contradiría el aviso.
		if (!hayEnlace)
		{
			Detalle = null;
		}

		Refrescar();

		if (!hayEnlace || _revalidar is null || _sesiones.Actual is null)
		{
			return;
		}

		// Con un reintento manual en curso, revalida él; si no, correrían dos revalidaciones.
		if (Ocupado)
		{
			return;
		}

		_ = RevalidarTrasRecuperarAsync();
	}

	// Nadie espera el resultado: aquí no puede escapar ninguna excepción.
	private async Task RevalidarTrasRecuperarAsync()
	{
		try
		{
			var resultado = await _revalidar!.RevalidarAsync();

			Detalle = resultado.EsRechazoDefinitivo ? MensajeSesionNoValida : MensajeRevalidada;
		}
		catch (Exception excepcion)
		{
			_registro.LogError(excepcion, "Falló la revalidación automática tras recuperar el enlace.");
			Detalle = MensajeFalloInesperado;
		}
		finally
		{
			Refrescar();
		}
	}
}
