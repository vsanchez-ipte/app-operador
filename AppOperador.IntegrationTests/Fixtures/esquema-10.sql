-- Base local tal como la dejaba la app en el esquema 10, antes de las llaves foráneas.
--
-- El DDL es el que generaba sqlite-net a partir de las entidades de entonces, copiado tal cual
-- (nombres en PascalCase incluidos). Los datos cubren todo lo que la migración tiene que
-- conservar o resolver: incidencias en todos los estados, una de antes de JTT-1394, una
-- simulada sin operador, evidencias de todos los estados y una huérfana, intentos de las dos
-- clases y uno huérfano, bitácora de antes y después del esquema 10, la sesión guardada, y
-- referencias a sesiones que ya no están (s-010, s-020) o de las que no se sabe nada (s-099).

CREATE TABLE "SesionLocal" (
"Id" integer primary key not null ,
"SessionId" varchar ,
"Operador" varchar ,
"Rol" varchar ,
"UnidadId" varchar ,
"UnidadClave" varchar ,
"UnidadDescripcion" varchar ,
"Permisos" varchar ,
"ValidadoUtcTicks" integer ,
"OfflineHastaUtcTicks" integer ,
"MonotonicoAlValidarTicks" integer ,
"VersionAplicacion" varchar ,
"VersionCatalogos" varchar );

CREATE TABLE "catalogo_afectacion" (
"id" integer primary key not null ,
"nombre" varchar );

CREATE TABLE "catalogo_cuerpo" (
"clave" varchar primary key not null ,
"nombre" varchar );

CREATE TABLE "catalogo_meta" (
"clave" varchar primary key not null ,
"version" varchar ,
"evidencia_formatos" varchar ,
"evidencia_tamano_maximo_mb" integer ,
"evidencia_maximo_archivos" integer );

CREATE TABLE "catalogo_severidad" (
"id" varchar primary key not null ,
"nivel" varchar ,
"orden" integer ,
"hexadecimal" varchar );

CREATE TABLE "catalogo_tipo_incidencia" (
"id" integer primary key not null ,
"nombre" varchar ,
"exige_descripcion" integer ,
"orden" integer );

CREATE TABLE "evento_auditoria" (
"id" integer primary key autoincrement not null ,
"instante_utc_ticks" integer ,
"monotonico_ticks" integer ,
"nivel" integer ,
"mensaje" varchar ,
"operacion" integer ,
"resultado" integer ,
"motivo_codigo" varchar ,
"operador" varchar ,
"rol" varchar ,
"permiso" varchar ,
"unidad_clave" varchar ,
"sesion_id" varchar ,
"origen" integer );

CREATE TABLE "evidencia_local" (
"uuid" varchar primary key not null ,
"incidencia_uuid" varchar ,
"ruta_archivo" varchar ,
"nombre_original" varchar ,
"tipo_medio" varchar ,
"bytes" integer ,
"estado" integer ,
"creado_utc_ticks" integer ,
"intentos" integer ,
"ultimo_error_codigo" varchar );

CREATE TABLE "incidencia_local" (
"uuid" varchar primary key not null ,
"clave_local" varchar ,
"tipo_clave" varchar ,
"tipo_nombre" varchar ,
"kilometro" varchar ,
"fuente_kilometro" integer ,
"kilometro_metros" integer ,
"gps_latitud" float ,
"gps_longitud" float ,
"gps_precision_metros" float ,
"gps_instante_utc_ticks" integer ,
"gravedad" integer ,
"severidad_id" varchar ,
"severidad_nombre" varchar ,
"severidad_orden" integer ,
"prioridad" integer ,
"version_catalogo" varchar ,
"nota" varchar ,
"estado" integer ,
"folio_central" varchar ,
"operador" varchar ,
"unidad_vehicular" varchar ,
"creado_utc_ticks" integer ,
"actualizado_utc_ticks" integer ,
"intentos" integer ,
"ultimo_error_codigo" varchar ,
"MonotonicoTicks" integer ,
"SesionOrigen" varchar ,
"PermisoOrigen" varchar );

CREATE TABLE "intento_sincronizacion" (
"id" integer primary key autoincrement not null ,
"registro_uuid" varchar ,
"clase" integer ,
"instante_utc_ticks" integer ,
"exito" integer ,
"codigo" integer ,
"codigo_texto" varchar ,
"mensaje" varchar );

CREATE INDEX "ix_auditoria_instante" on "evento_auditoria"("instante_utc_ticks");
CREATE INDEX "ix_auditoria_operador" on "evento_auditoria"("operador");
CREATE INDEX "ix_evidencia_estado" on "evidencia_local"("estado");
CREATE INDEX "ix_evidencia_incidencia" on "evidencia_local"("incidencia_uuid");
CREATE UNIQUE INDEX "ix_incidencia_clave" on "incidencia_local"("clave_local");
CREATE INDEX "ix_incidencia_estado" on "incidencia_local"("estado");
CREATE INDEX "ix_intento_registro" on "intento_sincronizacion"("registro_uuid");

-- Catálogo descargado el 20 de agosto.
INSERT INTO "catalogo_tipo_incidencia" VALUES (12, 'Vehículo averiado', 0, 1);
INSERT INTO "catalogo_tipo_incidencia" VALUES (107, 'Otro', 1, 2);
INSERT INTO "catalogo_severidad" VALUES ('sev-1', 'Crítico', 1, '#EB1409');
INSERT INTO "catalogo_severidad" VALUES ('sev-2', 'Advertencia', 2, '#F5A623');
INSERT INTO "catalogo_afectacion" VALUES (1, 'Total');
INSERT INTO "catalogo_afectacion" VALUES (2, 'Parcial');
INSERT INTO "catalogo_cuerpo" VALUES ('A', 'Cuerpo A');
INSERT INTO "catalogo_cuerpo" VALUES ('B', 'Cuerpo B');
INSERT INTO "catalogo_meta" VALUES ('vigente', '2026-08-20', 'image/jpeg,video/mp4', 25, 8);

-- La única sesión que la versión 10 sabía guardar: la del turno en curso (2 de septiembre).
INSERT INTO "SesionLocal" VALUES (1, 's-030', 'ana.lopez', 'Operador de campo', 'u-7', 'VEH-07', 'Camioneta 07',
	'APP_OPERADOR_MOVIL,CAPTURA_INCIDENCIAS', 639239328000000000, 639239616000000000, 36000000000, '1.4.0', '2026-08-20');

-- Incidencias. Estados: 1 borrador, 2 pendiente, 3 enviando, 4 sincronizado, 5 fallido.
-- i-5 es de antes de JTT-1394 (tipo de la maqueta, sin severidad) y se capturó sin sesión.
INSERT INTO "incidencia_local" VALUES ('i-5', 'LOC-673526', 'OBJETO', 'Objeto en la vía', '010+500', 2, NULL, NULL, NULL, NULL, NULL,
	2, '', '', 0, 1, '', 'Llanta en el carril', 5, NULL, '', '', 639228096000000000, 639228096000000000, 1, NULL, 0, '', '');
INSERT INTO "incidencia_local" VALUES ('i-1', 'LOC-673527', '12', 'Vehículo averiado', '045+200', 1, 45200, 19.4326, -99.1332, 8.5, 639238464000000000,
	0, 'sev-2', 'Advertencia', 2, 1, '2026-08-20', 'Camión detenido en acotamiento', 4, 'INC-APK-2026-0034', 'ana.lopez', 'VEH-01',
	639238464000000000, 639238518000000000, 2, NULL, 12345, 's-010', 'APP_OPERADOR_MOVIL');
INSERT INTO "incidencia_local" VALUES ('i-2', 'LOC-673528', '12', 'Vehículo averiado', '050+000', 2, 50000, NULL, NULL, NULL, NULL,
	0, 'sev-1', 'Crítico', 1, 2, '2026-08-20', 'Auto volcado', 2, NULL, 'ana.lopez', 'VEH-07',
	639239364000000000, 639239364000000000, 0, NULL, 23456, 's-030', 'APP_OPERADOR_MOVIL');
INSERT INTO "incidencia_local" VALUES ('i-3', 'LOC-673529', '107', 'Otro', '300+000', 2, 300000, NULL, NULL, NULL, NULL,
	0, 'sev-2', 'Advertencia', 2, 1, '2026-08-20', 'Kilómetro fuera del corredor a propósito', 5, NULL, 'ana.lopez', 'VEH-07',
	639239364600000000, 639239382600000000, 3, 'appincidencias.km.fueradecorredor', 23457, 's-030', 'APP_OPERADOR_MOVIL');
INSERT INTO "incidencia_local" VALUES ('i-4', 'LOC-673530', NULL, NULL, '012+', 2, NULL, NULL, NULL, NULL, NULL,
	0, '', '', 0, 1, '2026-08-20', '', 1, NULL, 'ana.lopez', 'VEH-07',
	639239365200000000, 639239365200000000, 0, NULL, 0, '', '');
INSERT INTO "incidencia_local" VALUES ('i-6', 'LOC-673531', '12', 'Vehículo averiado', '020+100', 2, 20100, NULL, NULL, NULL, NULL,
	0, 'sev-2', 'Advertencia', 2, 1, '2026-08-20', 'Del otro operador', 3, NULL, 'luis.mtz', 'VEH-03',
	639239382000000000, 639239382000000000, 1, NULL, 34567, 's-020', 'APP_OPERADOR_MOVIL');

-- Evidencias. e-9 apunta a una incidencia que ya no existe: lo que dejaba borrar un borrador.
INSERT INTO "evidencia_local" VALUES ('e-1', 'i-1', '/datos/evidencias/e-1.jpg', 'foto1.jpg', 'image/jpeg', 120000, 4, 639238464000000000, 1, NULL);
INSERT INTO "evidencia_local" VALUES ('e-2', 'i-2', '/datos/evidencias/e-2.jpg', 'foto2.jpg', 'image/jpeg', 98000, 2, 639239364000000000, 0, NULL);
INSERT INTO "evidencia_local" VALUES ('e-3', 'i-2', '/datos/evidencias/e-3.mp4', 'video.mp4', 'video/mp4', 30000000, 5, 639239364000000000, 1, 'appevidencias.tamano');
INSERT INTO "evidencia_local" VALUES ('e-4', 'i-4', '/datos/evidencias/e-4.jpg', 'borrador.jpg', 'image/jpeg', 45000, 2, 639239365200000000, 0, NULL);
INSERT INTO "evidencia_local" VALUES ('e-9', 'i-borrado', '/datos/evidencias/e-9.jpg', 'huerfana.jpg', 'image/jpeg', 10, 2, 639239365200000000, 0, NULL);

-- Intentos. Clase 1 incidencia, 2 evidencia. El 1 trae el código HTTP entero de antes de
-- JTT-1401; el 6 apunta a un registro que ya no existe.
INSERT INTO "intento_sincronizacion" VALUES (1, 'i-1', 1, 639238500000000000, 0, 503, NULL, 'Servicio no disponible');
INSERT INTO "intento_sincronizacion" VALUES (2, 'i-1', 1, 639238518000000000, 1, NULL, NULL, NULL);
INSERT INTO "intento_sincronizacion" VALUES (3, 'i-3', 1, 639239382000000000, 0, NULL, 'appincidencias.km.fueradecorredor', 'El kilómetro 300+000 está fuera del corredor');
INSERT INTO "intento_sincronizacion" VALUES (4, 'i-3', 1, 639239382600000000, 0, NULL, 'appincidencias.km.fueradecorredor', 'El kilómetro 300+000 sigue fuera del corredor');
INSERT INTO "intento_sincronizacion" VALUES (5, 'e-3', 2, 639239383200000000, 0, NULL, 'appevidencias.tamano', 'El archivo excede el tamaño admitido');
INSERT INTO "intento_sincronizacion" VALUES (6, 'i-borrado', 1, 639239383200000000, 0, NULL, 'appincidencias.error.tecnico', 'Intento sin registro');

-- Bitácora. La 1 es de antes del esquema 10 (sin operador ni origen); la 9 apunta a una
-- sesión de la que no se sabe el operador.
INSERT INTO "evento_auditoria" VALUES (1, 639228096000000000, NULL, 1, 'Aplicación iniciada', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL);
INSERT INTO "evento_auditoria" VALUES (2, 639238464000000000, 100, 1, 'Acceso concedido', 1, 1, NULL, 'ana.lopez', NULL, NULL, NULL, NULL, 1);
INSERT INTO "evento_auditoria" VALUES (3, 639238464000000000, 101, 1, 'Sesión creada', 3, 1, NULL, 'ana.lopez', 'Operador de campo', 'APP_OPERADOR_MOVIL,CAPTURA_INCIDENCIAS', 'VEH-01', 's-010', 1);
INSERT INTO "evento_auditoria" VALUES (4, 639238518000000000, 102, 1, 'Incidencia LOC-673527 enviada', 21, 1, NULL, 'ana.lopez', 'Operador de campo', 'APP_OPERADOR_MOVIL,CAPTURA_INCIDENCIAS', 'VEH-01', 's-010', 1);
INSERT INTO "evento_auditoria" VALUES (5, 639238752000000000, 103, 1, 'Sesión cerrada', 5, 1, NULL, 'ana.lopez', 'Operador de campo', 'APP_OPERADOR_MOVIL,CAPTURA_INCIDENCIAS', 'VEH-01', 's-010', 1);
INSERT INTO "evento_auditoria" VALUES (6, 639239328000000000, 200, 1, 'Sesión creada', 3, 1, NULL, 'ana.lopez', 'Operador de campo', 'APP_OPERADOR_MOVIL,CAPTURA_INCIDENCIAS', 'VEH-07', 's-030', 1);
INSERT INTO "evento_auditoria" VALUES (7, 639239382000000000, 201, 2, 'Incidencia LOC-673529 rechazada', 21, 2, 'appincidencias.km.fueradecorredor', 'ana.lopez', 'Operador de campo', 'APP_OPERADOR_MOVIL,CAPTURA_INCIDENCIAS', 'VEH-07', 's-030', 2);
INSERT INTO "evento_auditoria" VALUES (8, 639239382600000000, 300, 1, 'Sesión creada', 3, 1, NULL, 'luis.mtz', 'Supervisor', 'APP_OPERADOR_MOVIL', 'VEH-03', 's-020', 1);
INSERT INTO "evento_auditoria" VALUES (9, 639239383200000000, 301, 1, 'Línea con sesión sin datos', 0, NULL, NULL, NULL, NULL, NULL, NULL, 's-099', 2);

PRAGMA user_version = 10;
