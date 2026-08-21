using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Qué capacidades concede la sesión del operador (JTT-1385 CA 1, 3 y 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Jacob CCO emite dos permisos.</b> <c>APP_OPERADOR_MOVIL</c> —el módulo 1300000— significa
/// «puede usar la App Operador», y <c>APP_OPERADOR_CAPTURA</c> autoriza <b>capturar la incidencia
/// y adjuntarle evidencia</b>, que van juntas porque la evidencia no existe sin la incidencia a
/// la que se adjunta. Es la decisión del 20 de agosto de 2026: un solo permiso fino, no cuatro.
/// </para>
/// <para>
/// <b>Por eso hay dos comportamientos y no uno.</b> Registrar y adjuntar exigen su propio
/// permiso: si el general bastara, un operador con solo <c>APP_OPERADOR_MOVIL</c> capturaría
/// igual y el permiso nuevo no serviría de nada. Las otras dos —consultar la cola y sincronizar—
/// siguen bajo el permiso general, porque Jacob no las emite por separado y bloquearlas por
/// códigos que nunca van a llegar dejaría la app inservible.
/// </para>
/// <para>
/// El día que Jacob emita alguna de esas tres por separado, basta agregarla a
/// <see cref="ExigePermisoPropio"/>: ninguna pantalla se toca.
/// </para>
/// <para>
/// Ojo con el simulador de la app, que concede <c>CAPTURA</c>, <c>EVIDENCIA</c>, <c>SYNC</c> y
/// <c>OFFLINE</c>: esos códigos son suyos y no existen en el servidor.
/// </para>
/// </remarks>
public static class ReglaCapacidades
{
	/// <summary>Permiso funcional de la App Operador, el módulo 1300000 de Jacob CCO.</summary>
	public const string PermisoAppOperadorMovil = "APP_OPERADOR_MOVIL";

	/// <summary>
	/// Permiso de captura, el módulo 1300100 de Jacob CCO.
	/// </summary>
	/// <remarks>
	/// <b>Cubre dos capacidades: registrar la incidencia y adjuntarle evidencia.</b> Van juntas
	/// porque la evidencia no existe sin la incidencia a la que se adjunta; separarlas daría un
	/// permiso que no autoriza nada por su cuenta.
	/// </remarks>
	/// <remarks>
	/// <b>Este texto tiene que coincidir carácter por carácter con el que emite el API</b> —la
	/// comparación recorta espacios e ignora la caja, y nada más—. Si dejan de coincidir, la
	/// captura se apaga <b>sin ningún mensaje de error</b>: el operador ve los botones
	/// deshabilitados y no hay nada en la bitácora que lo explique. No cambiar de un solo lado.
	/// </remarks>
	public const string PermisoCapturaIncidencias = "APP_OPERADOR_CAPTURA";

	/// <summary>
	/// Indica si la sesión autoriza la capacidad indicada.
	/// </summary>
	/// <param name="permisos">
	/// Permisos de la sesión abierta, o <see langword="null"/> si no hay sesión.
	/// </param>
	/// <param name="capacidad">Acción que se quiere autorizar.</param>
	/// <remarks>
	/// <b>Sin sesión no se concede nada</b>, que es el CA 4: sin sesión válida no se puede
	/// registrar, adjuntar, consultar la cola ni sincronizar. Se resuelve aquí y no en cada
	/// pantalla para que las cuatro respondan igual.
	/// </remarks>
	public static bool Concede(PermisosOperador? permisos, CapacidadOperador capacidad)
	{
		if (permisos is null || permisos.Count == 0)
		{
			return false;
		}

		if (permisos.Contiene(CodigoDe(capacidad)))
		{
			return true;
		}

		// El permiso general no suple a los que Jacob sí emite por separado.
		return !ExigePermisoPropio(capacidad)
			&& permisos.Contiene(PermisoAppOperadorMovil);
	}

	/// <summary>
	/// Indica si la capacidad exige su propio permiso, sin respaldo del general.
	/// </summary>
	/// <remarks>
	/// Es la lista de capacidades que Jacob CCO emite por separado. Hoy las dos que cubre
	/// <see cref="PermisoCapturaIncidencias"/>. Agregar aquí las demás conforme el servidor las
	/// publique: es el único punto que hay que tocar.
	/// </remarks>
	private static bool ExigePermisoPropio(CapacidadOperador capacidad) =>
		capacidad is CapacidadOperador.RegistrarIncidencia
			or CapacidadOperador.AdjuntarEvidencia;

	/// <summary>
	/// Código con el que Jacob CCO concedería esta capacidad por separado.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Solo el de captura existe en el servidor</b>, desde la decisión del 20-ago, y cubre
	/// <b>dos</b> capacidades: registrar y adjuntar evidencia. Los otros dos códigos están
	/// declarados para que la correspondencia viva en un solo sitio el día que se definan, y
	/// para poder probar el camino del permiso específico.
	/// </para>
	/// <para>
	/// <b>No inventar un código propio para adjuntar evidencia.</b> Si adjuntar exigiera
	/// <c>APP_OPERADOR_EVIDENCIA</c> —un código que nadie va a emitir— y a la vez se le quitara
	/// el respaldo del permiso general, quedaría bloqueada para siempre.
	/// </para>
	/// </remarks>
	private static string CodigoDe(CapacidadOperador capacidad) => capacidad switch
	{
		CapacidadOperador.RegistrarIncidencia => PermisoCapturaIncidencias,
		CapacidadOperador.AdjuntarEvidencia => PermisoCapturaIncidencias,
		CapacidadOperador.ConsultarCola => "APP_OPERADOR_COLA",
		CapacidadOperador.Sincronizar => "APP_OPERADOR_SINCRONIZACION",
		_ => string.Empty,
	};
}
