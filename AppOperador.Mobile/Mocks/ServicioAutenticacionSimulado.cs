using AppOperador.Aplicacion.CasosDeUso;
using AppOperador.Aplicacion.Interfaces;
using AppOperador.Aplicacion.Modelos;
using AppOperador.Domain.Reglas;
using AppOperador.Domain.ValueObjects;

namespace AppOperador.Mobile.Mocks;

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
			permisos: PermisosOperador.DelServidor(
			[
				ReglaCapacidades.PermisoAppOperadorMovil,
				ReglaCapacidades.PermisoCapturaIncidencias,
			]),
			versionAplicacion: "1.2.0",
			versionCatalogos: new DateOnly(2026, 7, 23));
}
