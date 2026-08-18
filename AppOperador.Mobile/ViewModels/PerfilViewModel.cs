using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.ValueObjects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
#if EXPORTAR_BASE_DATOS
using AppOperador.Mobile.Configuracion;
using CommunityToolkit.Maui.Storage;
#endif

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
#if EXPORTAR_BASE_DATOS
	private readonly IExportadorBaseDatos _exportador;
#endif

	public PerfilViewModel(
		ISessionStore sesiones,
		IConnectivityService conectividad,
		IAuditLog bitacora,
		IClock reloj,
		CerrarSesionMovil cierre,
		EstadoEnlaceViewModel enlace
#if EXPORTAR_BASE_DATOS
		,
		IExportadorBaseDatos exportador
#endif
		)
	{
		Enlace = enlace;
		_sesiones = sesiones;
		_conectividad = conectividad;
		_bitacora = bitacora;
		_reloj = reloj;
		_cierre = cierre;
#if EXPORTAR_BASE_DATOS
		_exportador = exportador;
#endif
	}

	/// <summary>Aviso de modo offline, común a todas las pantallas (JTT-1383 CA 8).</summary>
	public EstadoEnlaceViewModel Enlace { get; }

	/// <summary>Eventos de la bitácora local, del más reciente al más antiguo.</summary>
	public ObservableCollection<EventoAuditoriaVista> Eventos { get; } = [];

	public string Operador => _sesiones.Actual?.Operador ?? "-";

	public string UnidadVehicular => _sesiones.Actual?.UnidadVehicular ?? "-";

	public string VersionAplicacion => _sesiones.Actual?.VersionAplicacion ?? "-";

	public string VersionCatalogos => _sesiones.Actual?.VersionCatalogos.ToString("yyyy-MM-dd") ?? "-";

	/// <summary>
	/// Estado de la ventana offline.
	/// </summary>
	/// <remarks>
	/// Con sesión abierta siempre es VIGENTE, y no es un atajo: la pantalla comprueba la
	/// vigencia antes de mostrarse y una sesión vencida ya no existe cuando esto se pinta
	/// (JTT-1384). Antes se recalculaba aquí contra el reloj del dispositivo, que es
	/// justamente la medición que el resto de la app dejó de usar por manipulable.
	/// </remarks>
	public string EstadoVigencia => _sesiones.Actual is null ? "SIN SESIÓN" : "VIGENTE";

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

	/// <summary>
	/// Indica si este paquete trae la exportación de diagnóstico.
	/// </summary>
	/// <remarks>
	/// La propiedad existe en los dos casos porque el XAML no se preprocesa: la vista compila
	/// sus enlaces contra este tipo y una propiedad que apareciera y desapareciera rompería la
	/// compilación del paquete normal, que es justamente el que no debe verse afectado.
	/// </remarks>
#if EXPORTAR_BASE_DATOS
	public bool PuedeExportarBaseDatos => true;
#else
	public bool PuedeExportarBaseDatos => false;
#endif

#if EXPORTAR_BASE_DATOS
	/// <summary>
	/// Guarda una copia legible de la base local, para revisarla en el escritorio.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Se avisa antes y no después: lo que se produce es la base entera sin cifrar, con
	/// incidencias, operador, unidad y bitácora dentro. Quien lo pide tiene que poder
	/// arrepentirse antes de que el archivo exista.
	/// </para>
	/// <para>
	/// El destino lo elige la persona en el selector de documentos del sistema. Es la vía que
	/// no exige permisos de almacenamiento: el sistema entrega un destino concreto en vez de
	/// abrirle a la app el almacenamiento completo del teléfono.
	/// </para>
	/// <para>
	/// El <c>await using</c> es lo que borra la copia temporal, y por eso envuelve todos los
	/// caminos de salida: se guardó, se canceló o falló a media escritura.
	/// </para>
	/// </remarks>
	[RelayCommand]
	private async Task ExportarBaseDatosAsync()
	{
		var confirmado = await Shell.Current.DisplayAlertAsync(
			"Exportar base de datos",
			"Se va a crear una copia SIN CIFRAR de la base local. Incluye incidencias, " +
			"operador, unidad y bitácora. Guárdela solo donde corresponda y bórrela al terminar.",
			"Exportar",
			"Cancelar");

		if (!confirmado)
		{
			return;
		}

		try
		{
			await using var exportacion = await _exportador.ExportarAsync(AmbienteDeCompilacion.Nombre);
			await using var contenido = File.OpenRead(exportacion.RutaTemporal);

			var guardado = await FileSaver.Default.SaveAsync(exportacion.NombreSugerido, contenido);

			if (!guardado.IsSuccessful)
			{
				// Cancelar en el selector entra por aquí, y es lo más común: no es un fallo
				// que valga la pena presentar como error.
				await Shell.Current.DisplayAlertAsync(
					"Exportar base de datos",
					"No se guardó la copia. No queda ningún archivo sin cifrar en el teléfono.",
					"Entendido");
				return;
			}

			// La ruta elegida no se registra: la bitácora se ve en pantalla y se exporta con la
			// propia base, así que apuntar dónde quedó la copia sin cifrar sería señalarla.
			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				$"Se exportó una copia sin cifrar de la base local (esquema {exportacion.VersionEsquema}).");

			await Shell.Current.DisplayAlertAsync(
				"Base exportada",
				$"{exportacion.NombreSugerido}\n\n" +
				$"Esquema {exportacion.VersionEsquema} · {exportacion.Tablas.Count} tablas · " +
				$"{Math.Max(1, exportacion.Bytes / 1024)} KB\n\n" +
				"La copia NO está cifrada.",
				"Entendido");

			await ActualizarAsync();
		}
		catch (ExportacionBaseDatosException error)
		{
			await Shell.Current.DisplayAlertAsync("No se pudo exportar", error.Message, "Entendido");
		}
		catch (Exception error)
		{
			// Se atrapa todo a propósito: esto cuelga de un botón, y lo que falle aquí —el
			// selector del sistema, el destino elegido, el espacio en disco— no debe tumbar la
			// app. El mensaje se muestra tal cual en vez de tragárselo, para que se pueda
			// reportar.
			await Shell.Current.DisplayAlertAsync(
				"No se pudo exportar",
				$"La exportación no terminó: {error.Message}",
				"Entendido");
		}
	}
#else
	/// <summary>
	/// Existe para que la vista compile en los paquetes sin exportación, donde el botón que la
	/// invoca nunca se muestra.
	/// </summary>
	[RelayCommand]
	private Task ExportarBaseDatosAsync() => Task.CompletedTask;
#endif
}
