using SQLite;

namespace AppOperador.Infrastructure.Sqlite.Entidades;

/// <summary>
/// Fila de <c>incidencia_local</c>: una incidencia capturada en el dispositivo.
/// </summary>
/// <remarks>
/// Es el <c>LocalIncident</c> del documento de arquitectura. Sobrevive al cierre de la
/// app y a la falta de red: guardar nunca depende de la conexión.
///
/// Los instantes se guardan como <b>ticks UTC</b> en un entero, no como
/// <see cref="DateTime"/>. sqlite-net devuelve las fechas con
/// <see cref="DateTimeKind.Unspecified"/>, y la regla de vigencia del dominio exige
/// <see cref="DateTimeKind.Utc"/>: guardando ticks el error deja de ser posible en vez de
/// depender de acordarse de normalizar en cada lectura.
/// </remarks>
[Table("incidencia_local")]
internal sealed class IncidenciaLocal
{
	/// <summary>
	/// Identificador de idempotencia generado en el dispositivo.
	/// </summary>
	/// <remarks>
	/// Viaja a Jacob y se conserva entre reintentos: si un envío se repite porque la
	/// respuesta se perdió, el servidor reconoce el UUID y no duplica la incidencia.
	/// </remarks>
	[PrimaryKey]
	[Column("uuid")]
	public string Uuid { get; set; } = string.Empty;

	/// <summary>Clave visible para el operador, con la forma <c>LOC-######</c>.</summary>
	[Indexed(Name = "ix_incidencia_clave", Unique = true)]
	[Column("clave_local")]
	public string ClaveLocal { get; set; } = string.Empty;

	/// <summary>
	/// Identificador del tipo en el catálogo, como texto. Nulo mientras es borrador.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Sigue siendo texto aunque desde JTT-1394 el catálogo identifique por entero, y es
	/// deliberado: esta columna es un <b>sello histórico de lo que se capturó</b>, no una llave
	/// foránea. Convertirla a entero habría dejado ilegibles las incidencias capturadas antes,
	/// que traen las claves de la maqueta —<c>OBJETO</c>, <c>VEHICULO</c>…—, y JTT-1388 CA 8
	/// exige que una actualización conserve borradores y pendientes.
	/// </para>
	/// <para>
	/// Las capturadas desde JTT-1394 guardan aquí el entero en forma invariante. Las anteriores
	/// conservan su clave de texto y <b>no son enviables</b>: apuntan a tipos que no existen en
	/// ningún servidor, y tampoco lo eran antes, porque el endpoint de creación no existía.
	/// </para>
	/// </remarks>
	[Column("tipo_clave")]
	public string? TipoClave { get; set; }

	/// <summary>Nombre del tipo al momento de capturar, para que el histórico no cambie
	/// si el catálogo se actualiza.</summary>
	[Column("tipo_nombre")]
	public string? TipoNombre { get; set; }

	/// <summary>Punto kilométrico en forma canónica <c>000+000</c>. Nulo en borradores.</summary>
	[Column("kilometro")]
	public string? Kilometro { get; set; }

	/// <summary>Origen del kilómetro: GPS o Manual. Ver <c>KilometerSource</c>.</summary>
	[Column("fuente_kilometro")]
	public int FuenteKilometro { get; set; }

	/// <summary>
	/// Gravedad del enum que la app tenía antes de JTT-1394. <b>Ya no se escribe.</b>
	/// </summary>
	/// <remarks>
	/// Se conserva la columna en vez de borrarla porque quitarla en SQLite obliga a reconstruir
	/// la tabla, y con ella se irían los borradores y pendientes que JTT-1388 CA 8 manda
	/// preservar. En las filas nuevas queda en cero; en las viejas dice con qué nivel inventado
	/// se capturaron, que es lo único que se puede saber de ellas.
	/// </remarks>
	[Column("gravedad")]
	public int Gravedad { get; set; }

	/// <summary>Identificador del nivel de severidad de Jacob. Vacío en las filas anteriores a JTT-1394.</summary>
	[Column("severidad_id")]
	public string SeveridadId { get; set; } = string.Empty;

	/// <summary>Nombre del nivel al momento de capturar, para que el histórico no cambie si lo renombran.</summary>
	[Column("severidad_nombre")]
	public string SeveridadNombre { get; set; } = string.Empty;

	/// <summary>Posición del nivel en la escala del catálogo, donde menor es más grave.</summary>
	[Column("severidad_orden")]
	public int SeveridadOrden { get; set; }

	/// <summary>Prioridad derivada de la severidad por la regla de dominio. Ver <c>SyncPriority</c>.</summary>
	[Column("prioridad")]
	public int Prioridad { get; set; }

	/// <summary>
	/// Versión del catálogo con la que se capturó, en formato <c>yyyy-MM-dd</c> (JTT-1394 CA 5).
	/// </summary>
	/// <remarks>
	/// <b>Se sella aquí y no solo en la sesión.</b> Una incidencia capturada sin conexión puede
	/// sincronizarse días después, cuando la sesión ya bajó un catálogo distinto: si la versión
	/// viviera solo en la sesión, al enviarla se declararía una que no es la que el operador
	/// usó. Vacío en las filas anteriores a JTT-1394, que no la registraron.
	/// </remarks>
	[Column("version_catalogo")]
	public string VersionCatalogo { get; set; } = string.Empty;

	/// <summary>Nota del operador. Obligatoria cuando el tipo exige descripción.</summary>
	[Column("nota")]
	public string Nota { get; set; } = string.Empty;

	/// <summary>Estado dentro de la cola. Ver <c>EstadoSincronizacion</c>.</summary>
	[Indexed(Name = "ix_incidencia_estado")]
	[Column("estado")]
	public int Estado { get; set; }

	/// <summary>Folio asignado por Jacob, con la forma <c>INC-####</c>. Nulo hasta sincronizar.</summary>
	[Column("folio_central")]
	public string? FolioCentral { get; set; }

	/// <summary>Operador que capturó, para trazabilidad.</summary>
	[Column("operador")]
	public string Operador { get; set; } = string.Empty;

	/// <summary>Unidad vehicular activa al capturar.</summary>
	[Column("unidad_vehicular")]
	public string UnidadVehicular { get; set; } = string.Empty;

	/// <summary>Instante de captura, en ticks UTC.</summary>
	[Column("creado_utc_ticks")]
	public long CreadoUtcTicks { get; set; }

	/// <summary>Instante del último cambio de estado, en ticks UTC.</summary>
	[Column("actualizado_utc_ticks")]
	public long ActualizadoUtcTicks { get; set; }

	/// <summary>Número de envíos intentados. Sirve para el reintento con espera creciente.</summary>
	[Column("intentos")]
	public int Intentos { get; set; }

	/// <summary>
	/// Contador monotónico del sistema en el momento de capturar (JTT-1383 CA 12).
	/// </summary>
	/// <remarks>
	/// Acompaña a <see cref="CreadoUtcTicks"/>, que es la fecha del dispositivo y por tanto
	/// se puede mover. Con las dos, quien reciba el registro puede ordenar lo capturado
	/// dentro de una misma sesión aunque el reloj haya cambiado en medio.
	/// </remarks>
	public long MonotonicoTicks { get; set; }

	/// <summary>
	/// Sesión de la que salió el registro (JTT-1383 CA 12).
	/// </summary>
	/// <remarks>
	/// Vacío en los recorridos simulados, que no crean sesión en Jacob.
	/// </remarks>
	public string SesionOrigen { get; set; } = string.Empty;

	/// <summary>
	/// Permiso con el que se autorizó la captura (JTT-1385 CA 7).
	/// </summary>
	/// <remarks>
	/// <para>
	/// Se guarda en el momento de crear el registro, no al sincronizarlo. Una incidencia creada
	/// sin conexión puede tardar horas en salir, y para entonces al operador pueden haberle
	/// revocado el permiso: al enviarla, Jacob necesita saber con qué autorización se capturó
	/// para decidir si sigue valiendo (CA 8), y eso ya no se puede reconstruir después.
	/// </para>
	/// <para>
	/// Hoy Jacob emite un solo permiso, así que aquí quedará <c>APP_OPERADOR_MOVIL</c> en todas.
	/// Vacío en los recorridos simulados, que no abren sesión en el servidor.
	/// </para>
	/// </remarks>
	public string PermisoOrigen { get; set; } = string.Empty;
}
