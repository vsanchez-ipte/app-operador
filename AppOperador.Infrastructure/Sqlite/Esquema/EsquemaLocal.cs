namespace AppOperador.Infrastructure.Sqlite.Esquema;

/// <summary>
/// El esquema de la base local, versión 11: trece tablas y nueve llaves foráneas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Referencia donde hay historial; instantánea donde el dato se reemplaza.</b> Operador,
/// unidad y sesión son historial y se referencian. El catálogo se descarga entero y se
/// reemplaza, así que la incidencia guarda el nombre del tipo y de la severidad tal como
/// estaban al capturar, sin llave: una llave al catálogo impediría actualizarlo mientras
/// quedara una incidencia pendiente de un tipo retirado (JTT-1394).
/// </para>
/// <para>
/// <b>Las tablas van en orden de padres a hijos.</b> La migración las recorre en ese orden
/// para que cada llave encuentre a quién apuntar, y la prueba de mapeo las recorre todas.
/// </para>
/// <para>
/// <b>Todas las llaves son <c>ON DELETE RESTRICT</c>.</b> Borrar un operador, una unidad o una
/// sesión no puede llevarse incidencias ni bitácora; y borrar un borrador exige quitar antes su
/// evidencia, que es lo que hace <c>EliminarBorrador</c>.
/// </para>
/// </remarks>
internal static class EsquemaLocal
{
	/// <summary>
	/// Versión que este código espera en <c>PRAGMA user_version</c>.
	/// </summary>
	/// <remarks>
	/// De 0 a 10 el esquema creció por columnas, y lo aplicaba sqlite-net al crear las tablas.
	/// La 11 lo reconstruye entero con llaves foráneas (<see cref="MigradorEsquema"/>). Un
	/// cambio futuro sube este número y agrega su paso allí; las tablas se describen aquí.
	/// </remarks>
	public const int Version = 11;

	public static readonly DefinicionTabla OperadorLocal = new(
		"operador_local",
		ClavePrimaria: "cuenta",
		Columnas:
		[
			Columna.Texto("cuenta", nulable: false),
			Columna.Texto("rol", nulable: false),
		],
		Indices: []);

	public static readonly DefinicionTabla UnidadLocal = new(
		"unidad_local",
		ClavePrimaria: "clave",
		Columnas:
		[
			Columna.Texto("clave", nulable: false),
			Columna.Texto("id", nulable: false),
			Columna.Texto("descripcion", nulable: false),
		],
		Indices: []);

	/// <summary>
	/// Pasa de una sola fila que se sobrescribía a un historial: una fila por sesión validada,
	/// con <c>vigente = 1</c> solo en la que la app usa ahora. El índice parcial único es lo que
	/// garantiza que nunca haya dos vigentes.
	/// </summary>
	public static readonly DefinicionTabla SesionLocal = new(
		"sesion_local",
		ClavePrimaria: "session_id",
		Columnas:
		[
			Columna.Texto("session_id", nulable: false, nombreAnterior: "SessionId"),
			Columna.Llave("operador", "operador_local(cuenta)", nulable: false, nombreAnterior: "Operador"),
			Columna.Llave("unidad_clave", "unidad_local(clave)", nulable: true, nombreAnterior: "UnidadClave"),
			Columna.Texto("rol", nulable: false, nombreAnterior: "Rol"),
			Columna.Texto("permisos", nulable: false, nombreAnterior: "Permisos"),
			Columna.Entero("validado_utc_ticks", nombreAnterior: "ValidadoUtcTicks"),
			Columna.Entero("offline_hasta_utc_ticks", nombreAnterior: "OfflineHastaUtcTicks"),
			Columna.Entero("monotonico_al_validar_ticks", nombreAnterior: "MonotonicoAlValidarTicks"),
			Columna.Texto("version_aplicacion", nulable: false, nombreAnterior: "VersionAplicacion"),
			Columna.Texto("version_catalogos", nulable: false, nombreAnterior: "VersionCatalogos"),
			Columna.Entero("vigente"),
		],
		Indices:
		[
			"CREATE INDEX \"ix_sesion_operador\" ON \"sesion_local\"(\"operador\");",
			"CREATE INDEX \"ix_sesion_unidad\" ON \"sesion_local\"(\"unidad_clave\");",
			"CREATE UNIQUE INDEX \"ix_sesion_vigente\" ON \"sesion_local\"(\"vigente\") WHERE \"vigente\" = 1;",
		],
		NombreAnterior: "SesionLocal");

	/// <summary>
	/// <c>operador</c>, <c>unidad_vehicular</c> y <c>sesion_origen</c> son las mismas columnas de
	/// siempre, ahora como llaves. No se duplicaron en columnas <c>_id</c> a propósito: el mismo
	/// dato dos veces es justo lo que este esquema vino a quitar. Van nulables porque las filas
	/// capturadas sin sesión —simuladas o anteriores a JTT-1383— no tienen a quién apuntar.
	/// </summary>
	public static readonly DefinicionTabla IncidenciaLocal = new(
		"incidencia_local",
		ClavePrimaria: "uuid",
		Columnas:
		[
			Columna.Texto("uuid", nulable: false),
			Columna.Texto("clave_local", nulable: false),
			Columna.Texto("tipo_clave"),
			Columna.Texto("tipo_nombre"),
			Columna.Texto("kilometro"),
			Columna.Entero("fuente_kilometro"),
			Columna.Entero("kilometro_metros", nulable: true),
			Columna.Real("gps_latitud"),
			Columna.Real("gps_longitud"),
			Columna.Real("gps_precision_metros"),
			Columna.Entero("gps_instante_utc_ticks", nulable: true),
			Columna.Entero("gravedad"),
			Columna.Texto("severidad_id", nulable: false),
			Columna.Texto("severidad_nombre", nulable: false),
			Columna.Entero("severidad_orden"),
			Columna.Entero("prioridad"),
			Columna.Texto("version_catalogo", nulable: false),
			Columna.Texto("nota", nulable: false),
			Columna.Entero("estado"),
			Columna.Texto("folio_central"),
			Columna.Llave("operador", "operador_local(cuenta)", nulable: true),
			Columna.Llave("unidad_vehicular", "unidad_local(clave)", nulable: true),
			Columna.Entero("creado_utc_ticks"),
			Columna.Entero("actualizado_utc_ticks"),
			Columna.Entero("intentos"),
			Columna.Texto("ultimo_error_codigo"),
			Columna.Entero("monotonico_ticks", nombreAnterior: "MonotonicoTicks"),
			Columna.Llave("sesion_origen", "sesion_local(session_id)", nulable: true, nombreAnterior: "SesionOrigen"),
			Columna.Texto("permiso_origen", nulable: false, nombreAnterior: "PermisoOrigen"),
		],
		Indices:
		[
			"CREATE UNIQUE INDEX \"ix_incidencia_clave\" ON \"incidencia_local\"(\"clave_local\");",
			"CREATE INDEX \"ix_incidencia_estado\" ON \"incidencia_local\"(\"estado\");",
			"CREATE INDEX \"ix_incidencia_operador\" ON \"incidencia_local\"(\"operador\");",
			"CREATE INDEX \"ix_incidencia_unidad\" ON \"incidencia_local\"(\"unidad_vehicular\");",
			"CREATE INDEX \"ix_incidencia_sesion\" ON \"incidencia_local\"(\"sesion_origen\");",
		]);

	public static readonly DefinicionTabla EvidenciaLocal = new(
		"evidencia_local",
		ClavePrimaria: "uuid",
		Columnas:
		[
			Columna.Texto("uuid", nulable: false),
			Columna.Llave("incidencia_uuid", "incidencia_local(uuid)", nulable: false),
			Columna.Texto("ruta_archivo", nulable: false),
			Columna.Texto("nombre_original", nulable: false),
			Columna.Texto("tipo_medio", nulable: false),
			Columna.Entero("bytes"),
			Columna.Entero("estado"),
			Columna.Entero("creado_utc_ticks"),
			Columna.Entero("intentos"),
			Columna.Texto("ultimo_error_codigo"),
		],
		Indices:
		[
			"CREATE INDEX \"ix_evidencia_incidencia\" ON \"evidencia_local\"(\"incidencia_uuid\");",
			"CREATE INDEX \"ix_evidencia_estado\" ON \"evidencia_local\"(\"estado\");",
		]);

	/// <summary>
	/// Mitad de la antigua <c>intento_sincronizacion</c>, cuya columna <c>registro_uuid</c>
	/// apuntaba a una incidencia o a una evidencia según <c>clase</c>: una llave foránea no
	/// puede apuntar a dos tablas, así que cada clase tiene ahora la suya.
	/// </summary>
	public static readonly DefinicionTabla IntentoIncidencia = new(
		"intento_incidencia",
		ClavePrimaria: "id",
		Columnas:
		[
			Columna.Entero("id"),
			Columna.Llave("incidencia_uuid", "incidencia_local(uuid)", nulable: false, nombreAnterior: "registro_uuid"),
			Columna.Entero("instante_utc_ticks"),
			Columna.Entero("exito"),
			Columna.Entero("codigo", nulable: true),
			Columna.Texto("codigo_texto"),
			Columna.Texto("mensaje"),
		],
		Indices: ["CREATE INDEX \"ix_intento_incidencia\" ON \"intento_incidencia\"(\"incidencia_uuid\");"],
		Autoincremento: true,
		NombreAnterior: "intento_sincronizacion");

	public static readonly DefinicionTabla IntentoEvidencia = new(
		"intento_evidencia",
		ClavePrimaria: "id",
		Columnas:
		[
			Columna.Entero("id"),
			Columna.Llave("evidencia_uuid", "evidencia_local(uuid)", nulable: false, nombreAnterior: "registro_uuid"),
			Columna.Entero("instante_utc_ticks"),
			Columna.Entero("exito"),
			Columna.Entero("codigo", nulable: true),
			Columna.Texto("codigo_texto"),
			Columna.Texto("mensaje"),
		],
		Indices: ["CREATE INDEX \"ix_intento_evidencia\" ON \"intento_evidencia\"(\"evidencia_uuid\");"],
		Autoincremento: true,
		NombreAnterior: "intento_sincronizacion");

	/// <summary>
	/// Solo <c>sesion_id</c> es llave. Operador, rol, permiso y unidad se quedan como
	/// instantánea: la línea del acceso se escribe antes de que exista la sesión, y el operador
	/// de una línea puede reescribirse al atribuir un alias (JTT-1392).
	/// </summary>
	public static readonly DefinicionTabla EventoAuditoria = new(
		"evento_auditoria",
		ClavePrimaria: "id",
		Columnas:
		[
			Columna.Entero("id"),
			Columna.Entero("instante_utc_ticks"),
			Columna.Entero("monotonico_ticks"),
			Columna.Entero("nivel"),
			Columna.Texto("mensaje", nulable: false),
			Columna.Entero("operacion"),
			Columna.Entero("resultado", nulable: true),
			Columna.Texto("motivo_codigo"),
			Columna.Texto("operador"),
			Columna.Texto("rol"),
			Columna.Texto("permiso"),
			Columna.Texto("unidad_clave"),
			Columna.Llave("sesion_id", "sesion_local(session_id)", nulable: true),
			Columna.Entero("origen", nulable: true),
		],
		Indices:
		[
			"CREATE INDEX \"ix_auditoria_instante\" ON \"evento_auditoria\"(\"instante_utc_ticks\");",
			"CREATE INDEX \"ix_auditoria_operador\" ON \"evento_auditoria\"(\"operador\");",
			"CREATE INDEX \"ix_auditoria_sesion\" ON \"evento_auditoria\"(\"sesion_id\");",
		],
		Autoincremento: true);

	public static readonly DefinicionTabla CatalogoTipoIncidencia = new(
		"catalogo_tipo_incidencia",
		ClavePrimaria: "id",
		Columnas:
		[
			Columna.Entero("id"),
			Columna.Texto("nombre", nulable: false),
			Columna.Entero("exige_descripcion"),
			Columna.Entero("orden"),
		],
		Indices: []);

	public static readonly DefinicionTabla CatalogoSeveridad = new(
		"catalogo_severidad",
		ClavePrimaria: "id",
		Columnas:
		[
			Columna.Texto("id", nulable: false),
			Columna.Texto("nivel", nulable: false),
			Columna.Entero("orden"),
			Columna.Texto("hexadecimal", nulable: false),
		],
		Indices: []);

	public static readonly DefinicionTabla CatalogoAfectacion = new(
		"catalogo_afectacion",
		ClavePrimaria: "id",
		Columnas:
		[
			Columna.Entero("id"),
			Columna.Texto("nombre", nulable: false),
		],
		Indices: []);

	public static readonly DefinicionTabla CatalogoCuerpo = new(
		"catalogo_cuerpo",
		ClavePrimaria: "clave",
		Columnas:
		[
			Columna.Texto("clave", nulable: false),
			Columna.Texto("nombre", nulable: false),
		],
		Indices: []);

	public static readonly DefinicionTabla CatalogoMeta = new(
		"catalogo_meta",
		ClavePrimaria: "clave",
		Columnas:
		[
			Columna.Texto("clave", nulable: false),
			Columna.Texto("version", nulable: false),
			Columna.Texto("evidencia_formatos", nulable: false),
			Columna.Entero("evidencia_tamano_maximo_mb"),
			Columna.Entero("evidencia_maximo_archivos"),
		],
		Indices: []);

	/// <summary>Todas, de padres a hijos.</summary>
	public static readonly IReadOnlyList<DefinicionTabla> Tablas =
	[
		OperadorLocal,
		UnidadLocal,
		SesionLocal,
		IncidenciaLocal,
		EvidenciaLocal,
		IntentoIncidencia,
		IntentoEvidencia,
		EventoAuditoria,
		CatalogoTipoIncidencia,
		CatalogoSeveridad,
		CatalogoAfectacion,
		CatalogoCuerpo,
		CatalogoMeta,
	];
}
