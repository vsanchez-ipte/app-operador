using System.Collections.ObjectModel;
using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Aplicacion.Servicios;
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
		EstadoEnlaceViewModel enlace,
		CapacidadesDeLaSesion capacidades,
		ConsultarAlmacenamientoLocal almacenamiento
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
		_capacidades = capacidades;
		_almacenamiento = almacenamiento;
#if EXPORTAR_BASE_DATOS
		_exportador = exportador;
#endif
#if COPIAR_TOKEN
		_tokens = tokens;
		_claims = claims;
#endif
	}

	private readonly CapacidadesDeLaSesion _capacidades;
	private readonly ConsultarAlmacenamientoLocal _almacenamiento;

	// En lugar de VIGENTE cuando la ventana venció o la sesión no respalda ninguna capacidad.
	private const string TextoPermisosVencidos = "Permisos vencidos o no validados";

	// Aparte de Ocupado: es la carga al entrar, no un botón trabajando.
	[ObservableProperty]
	public partial bool Cargando { get; set; }

	public EstadoEnlaceViewModel Enlace { get; }

	public AlmacenamientoLocal Almacenamiento { get; private set; } = AlmacenamientoLocal.Vacio;

	public ObservableCollection<EvidenciaPendienteVista> EvidenciasPendientes { get; } = [];

	public ObservableCollection<CapacidadVista> Capacidades { get; } = [];

	// En el teléfono nadie lee doscientas líneas, y pintarlas todas hacía lenta la pantalla.
	public const int EventosPorPagina = 15;

	public ObservableCollection<EventoAuditoriaVista> Eventos { get; } = [];

	[ObservableProperty]
	public partial bool HayMasEventos { get; set; }

	public string Operador => _sesiones.Actual?.Operador ?? "-";

	public string UnidadVehicular => _sesiones.Actual?.UnidadVehicular ?? "-";

	public string VersionAplicacion => _sesiones.Actual?.VersionAplicacion ?? "-";

	public string VersionCatalogos => _sesiones.Actual?.VersionCatalogos.ToString("yyyy-MM-dd") ?? "-";

	// Con sesión abierta ya es vigente: la pantalla comprueba la vigencia antes de mostrarse.
	public string EstadoVigencia => _sesiones.Actual is null
		? "SIN SESIÓN"
		: SesionRespaldaAlgo ? "VIGENTE" : TextoPermisosVencidos;

	public bool VigenciaActiva => EstadoVigencia == "VIGENTE";

	private bool SesionRespaldaAlgo =>
		_sesiones.Actual is { } sesion
		&& sesion.Vigencia.EstaVigenteEn(_reloj.UtcAhora)
		&& (_capacidades.Puede(CapacidadOperador.RegistrarIncidencia)
			|| _capacidades.Puede(CapacidadOperador.AdjuntarEvidencia)
			|| _capacidades.Puede(CapacidadOperador.Sincronizar)
			|| _capacidades.Puede(CapacidadOperador.ConsultarCola));

	public string EspacioLibre => Almacenamiento.Espacio.PorcentajeLibre is { } porcentaje
		? $"{porcentaje} % libre"
		: "-";

	public string TextoEvidenciasPendientes => Almacenamiento.CuantasPendientes switch
	{
		0 => "Ninguna pendiente de enviar",
		1 => $"1 pendiente de enviar ({TamanoLegible(Almacenamiento.BytesPendientes)})",
		var n => $"{n} pendientes de enviar ({TamanoLegible(Almacenamiento.BytesPendientes)})",
	};

	public bool HayEvidenciasPendientes => Almacenamiento.CuantasPendientes > 0;

	// En hora local aunque la comparación se haga en UTC.
	public string HoraExpiracion =>
		_sesiones.Actual?.Vigencia.OfflineUntilUtc.ToLocalTime().ToString("hh:mm tt") ?? "-";

	public string Modo => _conectividad.HayEnlace ? "ONLINE" : "OFFLINE";

	public IReadOnlyCollection<string> Permisos =>
		_sesiones.Actual?.Permisos ?? PermisosOperador.Ninguno;

	public async Task ActualizarAsync()
	{
		Cargando = true;
		try
		{
			await ActualizarCargandoAsync();
		}
		finally
		{
			Cargando = false;
		}
	}

	private async Task ActualizarCargandoAsync()
	{
		Eventos.Clear();
		HayMasEventos = false;
		await VerMasEventosAsync();

		Almacenamiento = await _almacenamiento.EjecutarAsync();
		EvidenciasPendientes.Clear();
		foreach (var pendiente in Almacenamiento.EvidenciasPendientes)
		{
			EvidenciasPendientes.Add(new EvidenciaPendienteVista(pendiente));
		}

		// «Operación offline» no es un permiso del servidor: es que la ventana siga abierta.
		Capacidades.Clear();
		Capacidades.Add(new CapacidadVista("Captura", _capacidades.Puede(CapacidadOperador.RegistrarIncidencia)));
		Capacidades.Add(new CapacidadVista("Evidencia", _capacidades.Puede(CapacidadOperador.AdjuntarEvidencia)));
		Capacidades.Add(new CapacidadVista("Sincronización", _capacidades.Puede(CapacidadOperador.Sincronizar)));
		Capacidades.Add(new CapacidadVista(
			"Operación offline",
			_sesiones.Actual is { } sesion && sesion.Vigencia.EstaVigenteEn(_reloj.UtcAhora)));

		foreach (var propiedad in new[]
		{
			nameof(Operador), nameof(UnidadVehicular), nameof(VersionAplicacion),
			nameof(VersionCatalogos), nameof(EstadoVigencia), nameof(VigenciaActiva),
			nameof(HoraExpiracion), nameof(Modo), nameof(Permisos),
			nameof(Almacenamiento), nameof(EspacioLibre), nameof(TextoEvidenciasPendientes),
			nameof(HayEvidenciasPendientes),
		})
		{
			OnPropertyChanged(propiedad);
		}
	}

	// Una línea de más dice si hay otra página sin una consulta aparte.
	[RelayCommand]
	private async Task VerMasEventosAsync()
	{
		var pagina = await _bitacora.ObtenerEventosAsync(Eventos.Count, EventosPorPagina + 1);

		foreach (var evento in pagina.Take(EventosPorPagina))
		{
			Eventos.Add(new EventoAuditoriaVista(evento));
		}

		HayMasEventos = pagina.Count > EventosPorPagina;
	}

	// Se navega aunque no se haya avisado a Jacob: el cierre local ya se aplicó.
	[RelayCommand]
	private async Task CerrarSesionAsync()
	{
		await _cierre.CerrarAsync();
		await Shell.Current.GoToAsync("//acceso");
	}

	// Existe en los dos paquetes: el XAML no se preprocesa y compila sus enlaces contra este tipo.
#if EXPORTAR_BASE_DATOS
	public bool PuedeExportarBaseDatos => true;
#else
	public bool PuedeExportarBaseDatos => false;
#endif

#if EXPORTAR_BASE_DATOS
	// Se avisa antes porque la copia sale sin cifrar; el await using borra la temporal en cualquier salida.
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
				// Cancelar en el selector entra aquí, y no es un error.
				await Shell.Current.DisplayAlertAsync(
					"Exportar base de datos",
					"No se guardó la copia. No queda ningún archivo sin cifrar en el teléfono.",
					"Entendido");
				return;
			}

			// La ruta no se registra: la bitácora se exporta con la base y señalaría la copia sin cifrar.
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
			// Cuelga de un botón: nada de aquí debe tumbar la app, y el mensaje se muestra para reportarlo.
			await Shell.Current.DisplayAlertAsync(
				"No se pudo exportar",
				$"La exportación no terminó: {error.Message}",
				"Entendido");
		}
	}
#else
	// Para que la vista compile en los paquetes sin exportación.
	[RelayCommand]
	private Task ExportarBaseDatosAsync() => Task.CompletedTask;
#endif

	// Existe en los dos paquetes, por lo mismo que PuedeExportarBaseDatos.
#if COPIAR_TOKEN
	public bool PuedeCopiarToken => true;
#else
	public bool PuedeCopiarToken => false;
#endif

#if COPIAR_TOKEN
	// Preauth exige la contraseña cifrada con RSA: desde Swagger solo se prueba con un token sacado de la app.
	[RelayCommand]
	private async Task CopiarTokenAsync()
	{
		var token = await _tokens.ObtenerAsync();

		if (string.IsNullOrWhiteSpace(token))
		{
			// Sesión cerrada o paquete simulado: no hay token que dar.
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

			// Compartir es la única salida desde un teléfono físico; en el emulador basta con copiar.
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
			// Por lo mismo que en la exportación; el mensaje no incluye el token.
			await Shell.Current.DisplayAlertAsync(
				"No se pudo copiar",
				$"El token no salió de la app: {error.Message}",
				"Entendido");
		}
	}
#else
	// Para que la vista compile en los paquetes sin copia de token.
	[RelayCommand]
	private Task CopiarTokenAsync() => Task.CompletedTask;
#endif

	private static string TamanoLegible(long bytes) => bytes switch
	{
		< 1024 => $"{bytes} B",
		< 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
		_ => $"{bytes / (1024.0 * 1024.0):0.#} MB",
	};
}

public sealed record CapacidadVista(string Nombre, bool Permitida)
{
	public string Estado => Permitida ? "SÍ" : "NO";
}

public sealed class EvidenciaPendienteVista
{
	public EvidenciaPendienteVista(EvidenciaPendiente pendiente)
	{
		Nombre = pendiente.NombreOriginal;
		Incidencia = pendiente.ClaveLocalIncidencia;
		Estado = pendiente.EstadoLegible;
	}

	public string Nombre { get; }

	public string Incidencia { get; }

	public string Estado { get; }

	public string Texto => $"{Nombre} · {Incidencia} · {Estado}";
}
