using AppOperador.Domain.ValueObjects;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Qué capacidades concede la sesión del operador (JTT-1385 CA 1, 3 y 4).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hoy Jacob CCO emite un solo permiso.</b> La respuesta del acceso trae siempre
/// <c>["APP_OPERADOR_MOVIL"]</c>, que es el módulo 1300000 y significa «puede usar la App
/// Operador». No existe un catálogo de capacidades finas —capturar, adjuntar, sincronizar— con
/// el que distinguir a un operador de otro.
/// </para>
/// <para>
/// Por eso el permiso general las concede todas: es lo único que el servidor sabe decir. La
/// alternativa —inventar códigos que Jacob nunca emite— haría que la app bloqueara funciones
/// por permisos que jamás van a llegar.
/// </para>
/// <para>
/// <b>La regla ya mira primero el permiso específico</b>, así que el día que Jacob emita
/// capacidades finas la app las respeta sin tocar ninguna pantalla. Mientras tanto ese camino
/// no se ejercita en producción, pero sí en las pruebas.
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

		return permisos.Contiene(CodigoDe(capacidad))
			|| permisos.Contiene(PermisoAppOperadorMovil);
	}

	/// <summary>
	/// Código con el que Jacob CCO concedería esta capacidad por separado.
	/// </summary>
	/// <remarks>
	/// Ninguno de estos códigos existe todavía en el servidor. Están declarados para que la
	/// correspondencia viva en un solo sitio el día que se definan, y para poder probar el
	/// camino del permiso específico.
	/// </remarks>
	private static string CodigoDe(CapacidadOperador capacidad) => capacidad switch
	{
		CapacidadOperador.RegistrarIncidencia => "APP_OPERADOR_CAPTURA",
		CapacidadOperador.AdjuntarEvidencia => "APP_OPERADOR_EVIDENCIA",
		CapacidadOperador.ConsultarCola => "APP_OPERADOR_COLA",
		CapacidadOperador.Sincronizar => "APP_OPERADOR_SINCRONIZACION",
		_ => string.Empty,
	};
}
