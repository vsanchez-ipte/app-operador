using AppOperador.Domain.Enums;

namespace AppOperador.Aplicacion.Modelos;

/// <summary>
/// Elemento de la cola de sincronización tal como se lista en la pantalla de Cola.
/// </summary>
/// <remarks>
/// La maqueta muestra dos identificadores distintos y conviene no confundirlos:
/// <see cref="ClaveLocal"/> (<c>LOC-######</c>) lo genera la app al guardar, existe sin
/// conexión y nunca cambia; <see cref="FolioCentral"/> (<c>INC-APK-2026-0034</c>) lo asigna
/// Jacob y solo aparece después de sincronizar. El folio es opcional y no bloquea (DA-15).
///
/// <b>Cuál de los dos manda lo decide <see cref="ReferenciaPrincipal"/></b> (JTT-1403 CA 1);
/// el otro no desaparece nunca (CA 2).
/// </remarks>
/// <param name="ClaveLocal">Identificador local, presente desde la captura.</param>
/// <param name="Clase">Qué es el registro: incidencia o evidencia.</param>
/// <param name="Prioridad">Prioridad con la que la cola lo atiende.</param>
/// <param name="Descripcion">Resumen legible del contenido.</param>
/// <param name="Kilometro">Punto kilométrico asociado, en forma canónica.</param>
/// <param name="Estado">Estado dentro de la cola.</param>
/// <param name="FolioCentral">Folio asignado por Jacob, si ya se sincronizó.</param>
/// <param name="Severidad">
/// Severidad con la que se capturó, tal como la nombra el catálogo.
/// <para>
/// <b>No es lo mismo que <see cref="Prioridad"/>, y confundirlas fue un defecto real.</b> La
/// pantalla mostraba la prioridad rotulada como severidad, y la prioridad solo tiene dos
/// valores: toda severidad que no fuera crítica —Advertencia, Información, Normal— se leía
/// «Normal». El operador que capturó una Advertencia veía otra cosa en la Cola.
/// </para>
/// </param>
public sealed record RegistroCola(
	string ClaveLocal,
	ClaseRegistro Clase,
	SyncPriority Prioridad,
	string Descripcion,
	string Kilometro,
	EstadoSincronizacion Estado,
	string? FolioCentral = null,
	string? Severidad = null)
{
	/// <summary>Severidad como se muestra, o un aviso explícito si el registro no la tiene.</summary>
	/// <remarks>
	/// <b>Vive aquí y no en la vista</b>, por lo mismo que <see cref="ReferenciaPrincipal"/>: qué
	/// se le muestra al operador no es una decisión de estilo, y en la vista no llega ninguna
	/// prueba. La Cola ya se contradijo dos veces en un día —26 de agosto— por decidir en el
	/// ViewModel, y este defecto es de la misma familia.
	/// <para>
	/// Un borrador puede no tener severidad todavía. Se dice, en vez de dejar el hueco vacío o
	/// —peor— rellenarlo con algo que parezca una severidad de verdad.
	/// </para>
	/// </remarks>
	public string SeveridadLegible =>
		string.IsNullOrWhiteSpace(Severidad) ? "Sin severidad" : Severidad!.Trim();

	/// <summary>
	/// Referencia con la que se identifica el registro: el folio si ya llegó, la clave local
	/// mientras no (JTT-1403 CA 1).
	/// </summary>
	/// <remarks>
	/// <b>No es una decisión de estilo, y por eso no vive en la vista.</b> El folio es la única
	/// referencia que el CCO puede buscar y la que el operador dicta por radio; la clave local
	/// no existe para nadie fuera del dispositivo que la generó. Presentar la clave como
	/// referencia principal de un registro ya confirmado obliga al operador a leer dos líneas
	/// para saber cuál sirve, y a acertar.
	///
	/// <b>La app no interpreta el folio</b>: lo recibe de Jacob y lo muestra tal cual. Su
	/// formato lo fija el servidor —hoy <c>INC-APK-2026-0034</c>— y puede cambiar sin que la
	/// app se entere, así que aquí no se valida ni se descompone.
	/// </remarks>
	public string ReferenciaPrincipal => TieneFolio ? FolioCentral! : ClaveLocal;

	/// <summary>Si Jacob ya confirmó el registro y le asignó folio.</summary>
	/// <remarks>
	/// Se comprueba con <see cref="string.IsNullOrWhiteSpace(string?)"/> y no contra nulo: una
	/// cadena vacía escrita por un mapeo descuidado se leería como folio y dejaría la lista con
	/// un renglón en blanco por referencia principal.
	/// </remarks>
	public bool TieneFolio => !string.IsNullOrWhiteSpace(FolioCentral);
}

/// <summary>
/// Naturaleza de un elemento de la cola.
/// </summary>
/// <remarks>
/// Incidencias y evidencias viajan por separado y se reintentan por separado: una
/// evidencia fallida no revierte una incidencia ya confirmada.
/// </remarks>
public enum ClaseRegistro
{
	Incidencia = 1,
	Evidencia = 2,
}
