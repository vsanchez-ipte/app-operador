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
#if COPIAR_TOKEN
using Microsoft.Maui.ApplicationModel.DataTransfer;
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
#if COPIAR_TOKEN
	private readonly ITokenProvider _tokens;
	private readonly ITokenClaims _claims;
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
#if COPIAR_TOKEN
		,
		ITokenProvider tokens,
		ITokenClaims claims
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
#if COPIAR_TOKEN
		_tokens = tokens;
		_claims = claims;
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

	/// <summary>
	/// Indica si este paquete puede copiar el token de la sesión.
	/// </summary>
	/// <remarks>
	/// Vale lo mismo que en <see cref="PuedeExportarBaseDatos"/>: la propiedad existe en los
	/// dos casos porque el XAML no se preprocesa y la vista compila sus enlaces contra este
	/// tipo.
	/// </remarks>
#if COPIAR_TOKEN
	public bool PuedeCopiarToken => true;
#else
	public bool PuedeCopiarToken => false;
#endif

#if COPIAR_TOKEN
	/// <summary>
	/// Deja el token de la sesión donde QA pueda recogerlo, para autorizarse en Swagger.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Por qué hace falta.</b> <c>POST /ITS/AppLogin/Preauth</c> no acepta la contraseña en
	/// claro: la espera cifrada con RSA-OAEP-SHA256 y en Base64. Desde Swagger no hay forma de
	/// producir eso a mano, así que sin esto los endpoints del canal móvil solo se pueden
	/// probar desde la app. La app ya cifra y ya tiene un token de una sesión válida; lo único
	/// que faltaba era poder sacarlo del teléfono.
	/// </para>
	/// <para>
	/// <b>Se avisa antes de copiar.</b> Lo que se entrega es la credencial de la sesión: quien
	/// la tenga puede actuar como este operador contra Jacob hasta que la sesión termine. Por
	/// eso también queda anotado en la bitácora, con nivel de advertencia.
	/// </para>
	/// <para>
	/// Después se dice qué módulos trae firmados el token. No es adorno: si falta
	/// <c>APP_OPERADOR_CAPTURA</c>, todos los <c>POST</c> del recorrido responden
	/// <c>appincidencias.permiso.revocado</c>, y sin este aviso eso se descubre a la mitad de
	/// la prueba y parece un fallo del servidor.
	/// </para>
	/// <para>
	/// <b>El token no se registra en ningún lado</b>, ni en la bitácora ni en un log: por eso
	/// el mensaje habla de los módulos y no del token (JTT-1378 §7).
	/// </para>
	/// </remarks>
	[RelayCommand]
	private async Task CopiarTokenAsync()
	{
		var token = await _tokens.ObtenerAsync();

		if (string.IsNullOrWhiteSpace(token))
		{
			// Pasa con la sesión cerrada y en los paquetes simulados, que no hablan con Jacob
			// y por lo tanto no tienen ningún token que dar.
			await Shell.Current.DisplayAlertAsync(
				"Sin token",
				"Esta sesión no tiene token guardado. Ingrese contra el servidor y vuelva a intentarlo.",
				"Entendido");
			return;
		}

		var confirmado = await Shell.Current.DisplayAlertAsync(
			"Copiar token de sesión",
			"El token es la credencial de esta sesión: quien lo tenga puede actuar como este " +
			"operador hasta que la sesión termine. Se copia para pegarlo en el botón Authorize " +
			"de Swagger.",
			"Copiar",
			"Cancelar");

		if (!confirmado)
		{
			return;
		}

		try
		{
			await Clipboard.Default.SetTextAsync(token);

			await _bitacora.RegistrarAsync(
				NivelAuditoria.Advertencia,
				"Se copió el token de la sesión al portapapeles.");

			var modulos = _claims.ModulosDe(token);
			var hayCaptura = modulos.Contains(
				ReglaCapacidades.PermisoCapturaIncidencias, StringComparer.OrdinalIgnoreCase);

			var alcance = hayCaptura
				? "Alcanza para el recorrido completo, incluidos los POST de incidencias."
				: $"Falta {ReglaCapacidades.PermisoCapturaIncidencias}: los POST van a responder " +
				  "appincidencias.permiso.revocado.";

			// Compartir es la única salida desde un teléfono físico, donde el portapapeles no
			// llega a la computadora en la que corre Swagger. En el emulador basta con copiar.
			var compartir = await Shell.Current.DisplayAlertAsync(
				"Token copiado",
				$"Módulos que firma el token: {(modulos.Count == 0 ? "ninguno" : string.Join(", ", modulos))}\n\n" +
				$"{alcance}\n\n" +
				"Si Swagger corre en otra computadora, compártalo por un medio de la empresa.",
				"Compartir",
				"Listo");

			if (compartir)
			{
				await Share.Default.RequestAsync(new ShareTextRequest(token, "Token de sesión"));
			}

			await ActualizarAsync();
		}
		catch (Exception error)
		{
			// Se atrapa todo por lo mismo que en la exportación: esto cuelga de un botón, y lo
			// que falle —el portapapeles del sistema, la hoja de compartir— no debe tumbar la
			// app. El mensaje sale tal cual para poder reportarlo; el token no aparece en él.
			await Shell.Current.DisplayAlertAsync(
				"No se pudo copiar",
				$"El token no salió de la app: {error.Message}",
				"Entendido");
		}
	}
#else
	/// <summary>
	/// Existe para que la vista compile en los paquetes sin copia de token, donde el botón que
	/// la invoca nunca se muestra.
	/// </summary>
	[RelayCommand]
	private Task CopiarTokenAsync() => Task.CompletedTask;
#endif
}
