# Endpoints del canal móvil — qué se puede probar en Swagger y con qué cuerpo

> Guía de pruebas para QA. Los cuerpos están listos para pegar y editar; lo que hay que
> cambiar en cada uno está señalado. Para conseguir el token, ver
> `guia-token-para-swagger.md`.

| Ambiente | Swagger |
|---|---|
| Desarrollo | `http://192.168.100.215:81/swagger` |
| QA | `http://192.168.100.230:81/swagger` |

**Antes de empezar:** botón **Authorize**, pegar el token, **Close**. Y usar un operador con
**los dos permisos** (`APP_OPERADOR_MOVIL` y `APP_OPERADOR_CAPTURA`); sin el segundo, todos los
`POST` de incidencias responden `appincidencias.permiso.revocado`.

---

## Resumen

| # | Endpoint | ¿Cuerpo? | Notas |
|---|---|---|---|
| 1 | `GET /ITS/AppCatalogos/Vigentes` | No | **Empezar aquí**: de la respuesta salen los ids de todo lo demás |
| 2 | `POST /ITS/AppIncidencias` | Sí, JSON | El grueso de las pruebas |
| 3 | `GET /ITS/AppIncidencias/{uuid}/Evidencias` | No | Qué evidencias tiene ya esa incidencia |
| 4 | `POST /ITS/AppIncidencias/{uuid}/Evidencias` | **No es JSON**: archivo | Se sube con el selector de Swagger |
| 5 | `GET /ITS/AppLogin/Estado` | No | Sonda de comunicación |
| 6 | `POST /ITS/AppLogin/Revalidar` | No | Renueva la ventana offline |
| 7 | `POST /ITS/AppLogin/Logout` | No | **Al final**: revoca la sesión y con ella el token |
| — | `POST /ITS/AppLogin/Preauth` y `POST /ITS/AppLogin` | — | **No se pueden probar a mano.** Ver el último apartado |

**El orden importa.** Catálogos → incidencia → evidencias → los de sesión, y `Logout` de último.

---

## 1. `GET /ITS/AppCatalogos/Vigentes`

Sin cuerpo. **De aquí se copian los ids** que piden los demás endpoints.

De la respuesta, anotar:

| Qué | Para qué |
|---|---|
| Un `tipos[].id` con `exigeDescripcion: false` | Las pruebas normales |
| El `tipos[].id` con `exigeDescripcion: true` (el «Otro») | La prueba de nota obligatoria |
| Un `severidades[].id` | Es **guid**, no número |
| Un `afectaciones[].id` | Es **número** |
| `limitesEvidencia` | Formatos, tamaño máximo y cuántos archivos por incidencia |

> ⚠️ **Trampa de nombres.** El catálogo lo llama **`severidades`** y el cuerpo de la incidencia
> lo llama **`idGravedad`**. Es el mismo dato.

**Qué comprobar aquí:** que `version` sea una fecha sin hora, que estén los cuatro `cuerpos`
(A, B, C, D), y que **ningún tipo desactivado aparezca** en la lista.

---

## 2. `POST /ITS/AppIncidencias`

El cuerpo listo para pegar:

```json
{
  "uuid": "0c9e7765-a747-4451-84cc-116f5e319d9a",
  "idTipoIncidencia": 11,
  "idGravedad": "a50790a4-c545-4b2d-9140-58df9e3e690b",
  "idAfectacion": 3,
  "km": 133.350,
  "fuenteKilometro": "GPS",
  "latitud": 32.5149,
  "longitud": -116.6280,
  "cuerpo": "C",
  "nota": "Prueba de Swagger, canal movil.",
  "fchCapturaCampo": "2026-09-09T18:24:13.419Z"
}
```

**Lo que hay que cambiar:**

| Campo | Qué poner |
|---|---|
| `uuid` | **Uno nuevo en cada corrida.** Ver el aviso de abajo |
| `idTipoIncidencia`, `idGravedad`, `idAfectacion` | Los del paso 1, **de este mismo servidor** |
| `km` | Entre **120.000 y 148.000** |
| `fuenteKilometro` | `GPS` o `MANUAL`, nada más |
| `cuerpo` | `A`, `B`, `C` o `D` |
| `nota` | Opcional, hasta 1000 caracteres. Si el tipo exige descripción, **mínimo 8** |
| `fchCapturaCampo` | UTC, con la `Z` al final |

> ⚠️ **El `POST` es idempotente por `uuid`.** Repetir uno ya usado devuelve `200` con el folio
> viejo y `yaExistia: true`: la prueba **parece** pasar sin haber probado nada. Cambiarlo
> siempre, salvo cuando se quiera probar justamente eso.

> ⚠️ **Lo que Swagger mete en el ejemplo y hay que BORRAR.** El ejemplo trae seis campos que no
> van: `idOperadorSincroniza`, `idRol`, `idSesionActual` e `idVehiculo` los pisa el servidor con
> los del token; y `idSesionOrigen` e `idOperadorCaptura` —si se dejan con el guid de ejemplo
> `3fa85f64-…`— hacen que el guardado falle con un `error.interno` que no explica nada. Solo se
> mandan cuando de verdad capturó **otro** operador en **otra** sesión.

### Variantes para probar cada rechazo

Sobre el mismo cuerpo, cambiando **una sola cosa** y con un `uuid` nuevo cada vez:

| Cambio | Debe responder |
|---|---|
| `"km": 119.999` | `appincidencias.km.fueradecorredor` |
| `"km": 148.001` | `appincidencias.km.fueradecorredor` |
| `"cuerpo": "E"` | Error de validación: el cuerpo debe ser A, B, C o D |
| `"fuenteKilometro": "gps"` | Error de validación: solo `GPS` o `MANUAL` |
| Un `idTipoIncidencia` desactivado | `appincidencias.catalogo.invalido` |
| Un `idAfectacion` que no exista | `appincidencias.catalogo.invalido` |
| Tipo con `exigeDescripcion: true` y `"nota": "corta"` | `appincidencias.nota.requerida` |
| **El mismo `uuid` dos veces** | `200`, mismo folio, `yaExistia: true` |

Cuerpo mínimo, para comprobar que lo opcional de verdad lo es (sin gravedad, sin nota, sin
coordenadas):

```json
{
  "uuid": "5d8ad4f7-1099-4e5b-86c8-4cf0b711ca37",
  "idTipoIncidencia": 11,
  "idAfectacion": 3,
  "km": 133.350,
  "fuenteKilometro": "MANUAL",
  "cuerpo": "A",
  "fchCapturaCampo": "2026-09-09T18:24:13.419Z"
}
```

---

## 3. `GET /ITS/AppIncidencias/{uuid}/Evidencias`

Sin cuerpo. El `uuid` va en la ruta y es **el mismo que se mandó al crear la incidencia**, no el
folio.

Devuelve lo que el servidor ya tiene de esa incidencia: un `evidencias[]` con `idEvidencia`,
`nombreOriginal`, `tipoMime`, `tamanoBytes`, `hashSha256` y `fchAlta` de cada archivo, más
`archivosAdjuntos` y `maximoArchivos`. Existe porque en carretera una subida puede perder la
respuesta: la app pregunta **cuáles** subió, no cuántas.

### Cómo probarlo, paso a paso

1. Registrar una incidencia con la sección 2 y **guardar su `uuid`**.
2. Consultar con ese `uuid`: `200`, `evidencias: []` y `archivosAdjuntos: 0`. Ya dice algo — la
   incidencia existe y todavía no tiene nada.
3. Subir una foto con la sección 4.
4. Consultar otra vez: un elemento en la lista y `archivosAdjuntos: 1`.
5. **Subir exactamente el mismo archivo otra vez.** La subida responde `200` con
   `yaExistia: true` y el conteo **no** sube. Al consultar sigue habiendo **una sola** entrada.
6. Consultar con un `uuid` inventado: `appevidencias.incidencia.noexiste`, **no** una lista
   vacía.

**El paso 6 es el que más vale.** Distinguir «esta incidencia nunca llegó» de «llegó y no tiene
evidencias» es lo que evita que la app suba archivos contra un `uuid` que el servidor no conoce.

**El paso 5 se une con lo del nombre.** Sube la misma foto con **otro nombre** y también la
deduplica: la llave es `(incidencia, contenido)`, no el nombre. Y en esa misma consulta se ve el
`tipoMime` **detectado por contenido** — una foto llamada `foto.txt` aparece como `image/jpeg`.

> **Este endpoint pide el permiso de captura**, no el general. Un operador que solo tenga
> `APP_OPERADOR_MOVIL` recibe aquí `appincidencias.permiso.revocado`, aunque los catálogos sí le
> respondan. Es a propósito: quien puede subir es quien puede saber si subió.

---

## 4. `POST /ITS/AppIncidencias/{uuid}/Evidencias`

**No lleva JSON**: es un archivo. Swagger muestra un selector; el campo se llama `archivo` y el
`uuid` va en la ruta.

| Límite | Valor por omisión |
|---|---|
| Formatos | JPEG, PNG, BMP y PDF — y video MP4/MOV **si el ambiente ya lo publica** |
| Tamaño por archivo | 15 MB |
| Archivos por incidencia | 8 |

> Los valores reales son **los que devuelve `limitesEvidencia`** en el paso 1: se cambian por
> configuración del servidor, sin APK nuevo. Si no coinciden con esta tabla, mandan los del
> catálogo.

### Qué vale la pena probar

| Prueba | Debe responder |
|---|---|
| Un archivo de formato no permitido (por ejemplo un `.docx`) | `appevidencias.formato.nopermitido` |
| Un archivo de más de 15 MB | `appevidencias.archivo.demasiadogrande` |
| El noveno archivo de la misma incidencia | `appevidencias.archivos.demasiados` |
| Un `uuid` de incidencia que no existe | `appevidencias.incidencia.noexiste` |
| Mandar el `POST` sin elegir archivo | `appevidencias.archivo.requerido` |

**Las dos pruebas que demuestran que la validación es de verdad.** El servidor decide el tipo
**por el contenido del archivo, no por su nombre**: lee los primeros bytes y los compara contra
las firmas conocidas —un JPEG empieza con `FF D8 FF`, un PDF con el texto `%PDF`—. La extensión
y la cabecera `Content-Type` las escribe quien sube el archivo, así que no se miran.

| Prueba | Debe responder | Por qué |
|---|---|---|
| Renombrar un `.txt` a `.jpg` y subirlo | `appevidencias.formato.nopermitido` | El nombre dice JPEG pero los bytes no. **Si esto se acepta, la validación es cosmética** |
| Renombrar una foto real a `foto.txt` y subirla | `200`, se acepta | El nombre no decide nada. **No es un defecto**: es la misma regla vista al revés |

La segunda sorprende, y por eso conviene hacerla: confirma que el nombre del archivo no
participa en la decisión.

---

## 5, 6 y 7. Los de sesión

Ninguno lleva cuerpo. Solo necesitan el token.

| Endpoint | Qué hace | Qué comprobar |
|---|---|---|
| `GET /ITS/AppLogin/Estado` | Sonda de comunicación | `200` con sesión viva |
| `POST /ITS/AppLogin/Revalidar` | Renueva la ventana offline | `200`. **No emite un token nuevo**: el copiado sigue sirviendo |
| `POST /ITS/AppLogin/Logout` | Cierra la sesión | `200` |

**`Logout` va al final**: revoca la sesión en el servidor y con ella muere el token copiado.
Justo después conviene comprobar dos cosas:

1. **Llamar `Logout` otra vez responde `200`, no un error.** Es idempotente a propósito.
2. **`Estado` ya falla**, aunque el token siga siendo válido criptográficamente. Es la
   separación entre «el token es auténtico» y «la sesión sigue viva».

Para seguir probando después de eso hay que ingresar en la app y copiar el token de nuevo.

---

## Los dos que no se pueden probar a mano

`POST /ITS/AppLogin/Preauth` y `POST /ITS/AppLogin` **no se pueden ejercitar desde Swagger.**

`Preauth` espera el correo **y** la contraseña **cifrados**, cada uno en su propio bloque
RSA-OAEP-SHA256 y en Base64. Tecleados en claro responde `400` con
`appoperador.credencial.ilegible`. Y `POST /ITS/AppLogin` necesita el `challengeId` que solo
emite `Preauth`, que además **vive 5 minutos y es de un solo uso**.

**El acceso se prueba desde la app**, que es el cliente real y hace ese cifrado en cada ingreso.
Otro detalle por si se prueba el camino triste: tras una credencial equivocada, el operador queda
**bloqueado un minuto**.

---

## Qué significa cada error

| Respuesta | Qué pasó | A quién le toca |
|---|---|---|
| `401` al entrar | El token no lo firmó **este** servidor, o la sesión ya cerró | Copiar el token otra vez, del paquete del ambiente correcto |
| `appincidencias.permiso.revocado` | Al rol le falta `APP_OPERADOR_CAPTURA` | Permisos del operador |
| `appincidencias.km.fueradecorredor` | El km salió de 120–148 | Es el cuerpo enviado |
| `appincidencias.catalogo.invalido` | El tipo, la afectación o la gravedad no están vigentes **aquí** | Sacar los ids del paso 1, del mismo servidor |
| `appincidencias.nota.requerida` | El tipo exige descripción y la nota tiene menos de 8 caracteres | Es el cuerpo enviado |
| `appincidencias.plaza.noresuelta` | **No es el cuerpo**: al ambiente le faltan los rangos de plazas, o están traslapados | Base de datos |
| `error.interno` | Algo tronó del lado del servidor y el mensaje no lo explica | Reportarlo con el cuerpo exacto que se mandó: el detalle está en el log del API |
