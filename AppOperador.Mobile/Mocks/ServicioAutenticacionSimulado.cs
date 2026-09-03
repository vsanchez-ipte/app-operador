using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

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
/// La ubicación <b>no</b> se comprueba aquí. Es un prerrequisito del dispositivo, no de
/// Jacob CCO, y lo verifica <c>VerificarUbicacionParaAcceso</c> antes de llamar a este
/// servicio, para que la regla valga igual contra el simulador y contra el API real.
/// </para>
/// <para>
/// Nada de esto es una regla de negocio: al llegar el API real esta clase desaparece completa.
/// </para>
/// </remarks>
public sealed class ServicioAutenticacionSimulado : IAuthenticationService
{
	private const string ContrasenaValida = "demo";
	private const string UsuarioSinPermiso = "sinpermiso";

	private readonly IClock _reloj;
	private readonly IConnectivityService _conectividad;
	private readonly ISessionStore _sesiones;
	private readonly IAuditLog _bitacora;
	private readonly ActualizarCatalogoLocal? _catalogos;

	// Última validación en línea. Es lo que permite reanudar sin conexión más adelante.
	private VigenciaOffline? _ultimaVigencia;
	private UnidadVehicular? _ultimaUnidad;
	private string? _ultimoUsuario;

	/// <param name="catalogos">
	/// Descarga del catálogo simulado (JTT-1394). El simulador hace de Jacob también para esto:
	/// desde que la base dejó de sembrar tipos, sin esta llamada el recorrido simulado se queda
	/// sin nada que ofrecer en el formulario. Opcional para no obligar a las pruebas.
	/// </param>
	public ServicioAutenticacionSimulado(
		IClock reloj,
		IConnectivityService conectividad,
		ISessionStore sesiones,
		IAuditLog bitacora,
		ActualizarCatalogoLocal? catalogos = null)
	{
		_reloj = reloj;
		_conectividad = conectividad;
		_sesiones = sesiones;
		_bitacora = bitacora;
		_catalogos = catalogos;
	}

	public Task<IReadOnlyList<UnidadVehicular>> ObtenerUnidadesAsync(CancellationToken cancelacion = default)
	{
		// En el diseño final este catálogo lo devuelve la preautenticación. Los identificadores
		// imitan los uuid que emite Jacob, para que el simulador ejercite el mismo camino que
		// el catálogo real y no una clave disfrazada de id.
		IReadOnlyList<UnidadVehicular> unidades =
		[
			new("11111111-1111-1111-1111-111111111111", "VEH-01", "Camioneta de campo 01"),
			new("22222222-2222-2222-2222-222222222222", "VEH-02", "Camioneta de campo 02"),
			new("33333333-3333-3333-3333-333333333333", "VEH-03", "Grúa ligera 03"),
		];

		return Task.FromResult(unidades);
	}

	public async Task<ResultadoAcceso> IngresarAsync(
		string usuario,
		string contrasena,
		UnidadVehicular unidad,
		CancellationToken cancelacion = default)
	{
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

		// Mismo momento que en el acceso real: el catálogo se refresca al validar en línea
		// (JTT-1394 CA 4). El token da igual aquí, pero no puede ir vacío o no se llamaría.
		if (_catalogos is not null)
		{
			await _catalogos.EjecutarAsync("simulado", cancelacion);
		}
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


	private static SesionOperador ConstruirSesion(string usuario, UnidadVehicular unidad, VigenciaOffline vigencia) =>
		new(
			operador: usuario,
			rol: "Operador de campo",
			unidadVehicular: unidad.Clave,
			vigencia: vigencia,
			// El simulador hace de Jacob: por eso puede entregar permisos. Ninguna otra parte
			// de la app puede construirlos (JTT-1379 CA 7).
			//
			// Entrega lo mismo que el servidor real, que desde el 20-ago son dos permisos: el
			// general y el de captura. Antes daba CAPTURA, EVIDENCIA, SYNC y OFFLINE, codigos
			// que nunca existieron en Jacob: con ellos el recorrido simulado quedaba sin
			// ninguna capacidad concedida (JTT-1385) y ademas hacia creer que el catalogo de
			// capacidades finas ya estaba resuelto.
			//
			// El de captura hace falta desde que registrar dejo de aceptar el respaldo del
			// permiso general: sin el, el recorrido simulado se queda sin poder capturar.
			permisos: PermisosOperador.DelServidor(
			[
				ReglaCapacidades.PermisoAppOperadorMovil,
				ReglaCapacidades.PermisoCapturaIncidencias,
			]),
			versionAplicacion: "1.2.0",
			versionCatalogos: new DateOnly(2026, 7, 23));
}
