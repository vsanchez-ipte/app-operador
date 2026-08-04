using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;

namespace AppOperador.Mobile.Mocks;

/// <summary>
/// Acceso simulado, para recorrer el flujo sin el canal móvil de Jacob.
/// </summary>
/// <remarks>
/// <para>
/// Reglas del simulador, elegidas para poder ejercitar los cuatro mensajes de rechazo
/// que exige JTT-279 sin necesidad de servidor:
/// </para>
/// <list type="bullet">
///   <item>Contraseña <c>demo</c>: acceso autorizado.</item>
///   <item>Usuario <c>sinpermiso</c>: la cuenta existe pero no tiene el permiso de la APK.</item>
///   <item>Cualquier otra contraseña: credencial inválida.</item>
///   <item>Sin enlace: no se puede validar por primera vez.</item>
/// </list>
/// <para>
/// La ubicación se consulta antes que nada, porque JTT-279 la exige para completar el
/// acceso. Nada de esto es una regla de negocio: al llegar el API real esta clase
/// desaparece completa.
/// </para>
/// </remarks>
public sealed class ServicioAutenticacionSimulado : IAuthenticationService
{
	private const string ContrasenaValida = "demo";
	private const string UsuarioSinPermiso = "sinpermiso";

	private readonly IClock _reloj;
	private readonly IConnectivityService _conectividad;
	private readonly ILocationService _ubicacion;
	private readonly ISessionStore _sesiones;
	private readonly IAuditLog _bitacora;

	// Última validación en línea. Es lo que permite reanudar sin conexión más adelante.
	private VigenciaOffline? _ultimaVigencia;
	private UnidadVehicular? _ultimaUnidad;
	private string? _ultimoUsuario;

	public ServicioAutenticacionSimulado(
		IClock reloj,
		IConnectivityService conectividad,
		ILocationService ubicacion,
		ISessionStore sesiones,
		IAuditLog bitacora)
	{
		_reloj = reloj;
		_conectividad = conectividad;
		_ubicacion = ubicacion;
		_sesiones = sesiones;
		_bitacora = bitacora;
	}

	public Task<IReadOnlyList<UnidadVehicular>> ObtenerUnidadesAsync(CancellationToken cancelacion = default)
	{
		// En el diseño final este catálogo lo devuelve la preautenticación.
		IReadOnlyList<UnidadVehicular> unidades =
		[
			new("VEH-01", "Camioneta de campo 01"),
			new("VEH-02", "Camioneta de campo 02"),
			new("VEH-03", "Grúa ligera 03"),
		];

		return Task.FromResult(unidades);
	}

	public async Task<ResultadoAcceso> IngresarAsync(
		string usuario,
		string contrasena,
		UnidadVehicular unidad,
		CancellationToken cancelacion = default)
	{
		// La ubicación es obligatoria para completar el acceso (JTT-279).
		if (!await _ubicacion.HayPermisoAsync(cancelacion))
		{
			return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.UbicacionNoDisponible);
		}

		// El primer ingreso siempre exige enlace: no existe login sin validar contra Jacob.
		if (!_conectividad.HayEnlace)
		{
			return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.SinComunicacion);
		}

		if (string.Equals(usuario, UsuarioSinPermiso, StringComparison.OrdinalIgnoreCase))
		{
			await _bitacora.RegistrarAsync(NivelAuditoria.Advertencia, "Acceso denegado: sin permiso de APK.", cancelacion);
			return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.SinPermiso);
		}

		if (!string.Equals(contrasena, ContrasenaValida, StringComparison.Ordinal))
		{
			await _bitacora.RegistrarAsync(NivelAuditoria.Advertencia, "Acceso denegado: credenciales no válidas.", cancelacion);
			return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.CredencialInvalida);
		}

		_ultimaVigencia = VigenciaOffline.Validada(_reloj.UtcAhora);
		_ultimaUnidad = unidad;
		_ultimoUsuario = usuario;

		var sesion = ConstruirSesion(usuario, unidad, _ultimaVigencia);
		_sesiones.Guardar(sesion);
		await _bitacora.RegistrarAsync(NivelAuditoria.Info, "Enlace CCO activo.", cancelacion);

		return ResultadoAcceso.Autorizar(sesion);
	}

	public async Task<ResultadoAcceso> ContinuarSinConexionAsync(CancellationToken cancelacion = default)
	{
		// Reanudar exige una validación en línea previa que todavía esté vigente.
		if (_ultimaVigencia is null || _ultimaUnidad is null || _ultimoUsuario is null)
		{
			return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.SesionOfflineExpirada);
		}

		if (!_ultimaVigencia.EstaVigenteEn(_reloj.UtcAhora))
		{
			return ResultadoAcceso.Rechazar(MotivoRechazoAcceso.SesionOfflineExpirada);
		}

		var sesion = ConstruirSesion(_ultimoUsuario, _ultimaUnidad, _ultimaVigencia);
		_sesiones.Guardar(sesion);
		await _bitacora.RegistrarAsync(NivelAuditoria.Advertencia, "Modo offline activado.", cancelacion);

		return ResultadoAcceso.Autorizar(sesion);
	}

	public async Task CerrarSesionAsync(CancellationToken cancelacion = default)
	{
		// Se borra la sesión, no los pendientes: JTT-328 exige conservarlos.
		_sesiones.Limpiar();
		await _bitacora.RegistrarAsync(NivelAuditoria.Info, "Sesión cerrada por el operador.", cancelacion);
	}

	private static SesionOperador ConstruirSesion(string usuario, UnidadVehicular unidad, VigenciaOffline vigencia) =>
		new(
			operador: usuario,
			rol: "Operador de campo",
			unidadVehicular: unidad.Clave,
			vigencia: vigencia,
			permisos: ["CAPTURA", "EVIDENCIA", "SYNC", "OFFLINE"],
			versionAplicacion: "1.2.0",
			versionCatalogos: new DateOnly(2026, 7, 23));
}
