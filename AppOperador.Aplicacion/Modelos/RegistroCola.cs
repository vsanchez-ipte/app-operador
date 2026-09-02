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
/// <param name="UltimoErrorCodigo">
/// Código con el que se rechazó el último intento, o <see langword="null"/> si no ha fallado.
/// Es lo que decide si el registro va a salir solo o necesita que alguien lo corrija.
/// </param>
/// <param name="UltimoErrorMensaje">
/// Lo que dijo Jacob al rechazarlo, o el motivo del fallo local.
/// <para>
/// <b>Se propaga en vez de reescribirse</b>, igual que en el aviso del formulario: quien
/// rechazó sabe por qué mejor que la pantalla. Sin él, la tarjeta solo puede decir que falló,
/// que es exactamente lo que el operador ya ve en la insignia.
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
	string? Severidad = null,
	string? UltimoErrorCodigo = null,
	string? UltimoErrorMensaje = null)
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

	/// <summary>Si la tarjeta debe explicar por qué este registro no ha salido.</summary>
	/// <remarks>
	/// Solo los fallidos. Un pendiente puede arrastrar el código de un intento anterior —al
	/// recuperar un envío interrumpido vuelve a Pendiente sin borrarlo—, y enseñar ahí un
	/// rechazo ya superado diría que algo va mal cuando el registro está en camino.
	/// </remarks>
	public bool HayMotivoFallo => Estado == EstadoSincronizacion.Fallido;

	/// <summary>
	/// Por qué no salió este registro, y si va a salir solo (JTT-1401 CA 8 y 9).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>El resumen de arriba cuenta cuántas hay de cada clase; esto dice cuál es cuál.</b> Con
	/// varias fallidas a la vez, «el CCO las rechazó» no le dice al operador <i>qué</i> corregir
	/// ni <i>en cuál</i>, y las que solo esperan reintento se confunden con las que no van a
	/// salir nunca. Es el mismo defecto que ya se corrigió en el aviso del formulario, una
	/// pantalla más adentro.
	/// </para>
	/// <para>
	/// <b>Vive aquí y no en la vista</b>, como <see cref="SeveridadLegible"/> y
	/// <see cref="ReferenciaPrincipal"/>: qué se le dice al operador no es una decisión de
	/// estilo, y en la vista no llega ninguna prueba.
	/// </para>
	/// </remarks>
	public string MotivoFallo => CodigosErrorJacob.EsFuncional(UltimoErrorCodigo)
		// Solo aquí se añade qué hacer: es el único caso en que el operador tiene que actuar,
		// y el único en que esperar no sirve de nada.
		? $"El CCO la rechazó: {Detalle} Corríjala: no saldrá sola."
		: $"No llegó al CCO: {Detalle}";

	/// <summary>
	/// Lo que se sabe del rechazo: el mensaje si lo hay, y si no, el código.
	/// </summary>
	/// <remarks>
	/// <b>El código es el último recurso, pero se muestra.</b> Es feo —
	/// <c>appincidencias.km.fueradecorredor</c> no está escrito para un operador— y aun así es
	/// infinitamente más útil que «falló»: es lo que se dicta por radio al CCO y lo que permite
	/// diagnosticar un dispositivo que vuelve de campo.
	/// </remarks>
	private string Detalle
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(UltimoErrorMensaje))
			{
				var mensaje = UltimoErrorMensaje!.Trim();
				return mensaje.EndsWith('.') ? mensaje : $"{mensaje}.";
			}

			return string.IsNullOrWhiteSpace(UltimoErrorCodigo)
				// Puede pasar con lo guardado antes de que se registraran los intentos. Se dice
				// que no se sabe, en vez de callar y dejar la tarjeta igual que antes.
				? "no se registró el motivo."
				: $"código {UltimoErrorCodigo!.Trim()}.";
		}
	}
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
