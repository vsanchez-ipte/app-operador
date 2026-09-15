using AppOperador.Aplicacion.Modelos;

namespace AppOperador.Aplicacion.Interfaces;

/// <summary>
/// Bitácora local de eventos operativos (JTT-1392).
/// </summary>
/// <remarks>
/// <para>
/// La maqueta la muestra en el perfil. El documento de arquitectura pide conservar
/// trazabilidad, pero no llegó a nombrar esta interfaz; el nombre sigue el estilo en
/// inglés del resto de contratos de esta carpeta.
/// </para>
/// <para>
/// <b>Quién y desde dónde lo pone la bitácora, no quien registra.</b> Cada línea lleva el
/// operador, su rol, permiso, unidad, sesión y si había enlace (CA 1); son datos de la sesión
/// abierta en ese instante, y es la implementación la que los toma de ahí. Así ningún caso de
/// uso puede olvidarlos ni ponerlos mal.
/// </para>
/// </remarks>
public interface IAuditLog
{
	/// <summary>
	/// Eventos del operador con sesión abierta, del más reciente al más antiguo.
	/// </summary>
	/// <remarks>
	/// Filtrados por operador porque la pantalla que los muestra es el perfil, que el operador
	/// lee como suyo (JTT-1392, decisión D3). Las líneas anteriores al esquema 10, que no
	/// tienen operador, se muestran a todos como historial previo.
	/// </remarks>
	Task<IReadOnlyList<EventoAuditoria>> ObtenerEventosAsync(CancellationToken cancelacion = default);

	/// <summary>Agrega un aviso general, sin operación concreta.</summary>
	Task RegistrarAsync(NivelAuditoria nivel, string mensaje, CancellationToken cancelacion = default);

	/// <summary>
	/// Agrega el resultado de una operación auditable (CA 2), con el vocabulario del CCO.
	/// </summary>
	/// <param name="motivoCodigo">Código del rechazo, cuando lo hay: el mismo que devolvió Jacob.</param>
	/// <param name="operador">
	/// A quién atribuir la línea cuando todavía no hay sesión: el acceso. Con sesión abierta se
	/// ignora y manda la sesión.
	/// </param>
	Task RegistrarAsync(
		OperacionAuditada operacion,
		ResultadoAuditoria resultado,
		string mensaje,
		string? motivoCodigo = null,
		string? operador = null,
		CancellationToken cancelacion = default);
}
