using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppOperador.Mobile.ViewModels;

/// <summary>
/// Perfil del operador: sesión, permisos y bitácora local (JTT-292).
/// </summary>
public sealed partial class PerfilViewModel : ObservableObject
{
	private readonly ISessionStore _sesiones;
	private readonly IConnectivityService _conectividad;
	private readonly IAuditLog _bitacora;
	private readonly IClock _reloj;
	private readonly CerrarSesionMovil _cierre;

	public PerfilViewModel(
		ISessionStore sesiones,
		IConnectivityService conectividad,
		IAuditLog bitacora,
		IClock reloj,
		CerrarSesionMovil cierre,
		EstadoEnlaceViewModel enlace)
	{
		Enlace = enlace;
		_sesiones = sesiones;
		_conectividad = conectividad;
		_bitacora = bitacora;
		_reloj = reloj;
		_cierre = cierre;
	}

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>Eventos de la bitácora local, del más reciente al más antiguo.</summary>
	public ObservableCollection<EventoAuditoriaVista> Eventos { get; } = [];

	public string Operador => _sesiones.Actual?.Operador ?? "-";

	public string UnidadVehicular => _sesiones.Actual?.UnidadVehicular ?? "-";

	public string VersionAplicacion => _sesiones.Actual?.VersionAplicacion ?? "-";

	public string VersionCatalogos => _sesiones.Actual?.VersionCatalogos.ToString("yyyy-MM-dd") ?? "-";

	/// <summary>Estado de la ventana offline: VIGENTE mientras quede tiempo.</summary>
	public string EstadoVigencia
	{
		get
		{
			var vigencia = _sesiones.Actual?.Vigencia;
			if (vigencia is null)
			{
				return "SIN SESIÓN";
			}

			return vigencia.EstaVigenteEn(_reloj.UtcAhora) ? "VIGENTE" : "EXPIRADA";
		}
	}

	public bool VigenciaActiva => EstadoVigencia == "VIGENTE";

	/// <summary>
	/// Hora local de expiración de la ventana offline.
	/// </summary>
	/// <remarks>
	/// JTT-279 exige mostrarla en hora local aunque la comparación se haga en UTC (DA-10).
	/// </remarks>
	public string HoraExpiracion =>
		_sesiones.Actual?.Vigencia.OfflineUntilUtc.ToLocalTime().ToString("hh:mm tt") ?? "-";

	/// <summary>Modo de operación visible en el perfil.</summary>
	public string Modo => _conectividad.HayEnlace ? "ONLINE" : "OFFLINE";

	/// <summary>
	/// Permisos efectivos, mostrados como etiquetas.
	/// </summary>
	/// <remarks>
	/// El perfil los muestra, no los administra: la lista es de solo lectura por construcción
	/// (JTT-1379 CA 7).
	/// </remarks>
	public IReadOnlyCollection<string> Permisos =>
		_sesiones.Actual?.Permisos ?? PermisosOperador.Ninguno;

	/// <summary>Refresca los datos de la pantalla.</summary>
	public async Task ActualizarAsync()
	{
		Eventos.Clear();
		foreach (var evento in await _bitacora.ObtenerEventosAsync())
		{
			Eventos.Add(new EventoAuditoriaVista(evento));
		}

		foreach (var propiedad in new[]
		{
			nameof(Operador), nameof(UnidadVehicular), nameof(VersionAplicacion),
			nameof(VersionCatalogos), nameof(EstadoVigencia), nameof(VigenciaActiva),
			nameof(HoraExpiracion), nameof(Modo), nameof(Permisos),
		})
		{
			OnPropertyChanged(propiedad);
		}
	}

	/// <summary>
	/// Termina la sesión del operador (JTT-1390).
	/// </summary>
	/// <remarks>
	/// Lo pendiente de enviar no se toca: cerrar sesión no es desinstalar la app. La
	/// navegación ocurre siempre, incluso si no se pudo avisar a Jacob, porque el cierre
	/// local ya se aplicó y dejar al operador en el perfil daría a entender lo contrario.
	/// </remarks>
	[RelayCommand]
	private async Task CerrarSesionAsync()
	{
		await _cierre.CerrarAsync();
		await Shell.Current.GoToAsync("//acceso");
	}
}
