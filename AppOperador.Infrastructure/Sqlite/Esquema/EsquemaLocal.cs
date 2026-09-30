namespace AppOperador.Infrastructure.Sqlite.Esquema;

// Referencia donde hay historial; nombre copiado donde el catálogo se reemplaza. Todas las llaves son ON DELETE RESTRICT.
internal static class EsquemaLocal
{
	// Un cambio futuro sube este número y agrega su paso en MigradorEsquema.
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

	// Historial: el índice único parcial impide dos vigentes.
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

	// Llaves nulables: las filas capturadas sin sesión no tienen a quién apuntar.
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

	// Salió de intento_sincronizacion: una llave foránea no puede apuntar a dos tablas.
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

	// Solo sesion_id es llave: la línea del acceso se escribe antes de que exista la sesión.
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

	// De padres a hijos: la migración las crea en este orden.
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
