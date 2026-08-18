# Cómo obtener una copia legible de la base de datos de la App Operador

> Guía corta para revisar en el equipo qué guarda la app: qué tablas hay, qué se escribió y
> cómo se relacionan los datos. Para armar el paquete, ver la guía de APK por ambiente que
> acompaña a esta.

**La base del teléfono está cifrada y no se puede abrir copiándola.** La app la guarda con
SQLCipher y la clave vive en el almacén seguro del sistema, que no la entrega ni al conectar
el teléfono por cable. Un archivo sacado con el explorador no lo abre ninguna herramienta.

Por eso la conversión la hace la propia app, desde dentro, y entrega una copia **SQLite
estándar sin cifrar** que abre cualquier visor.

---

## 1. Armar el paquete que puede exportar

Un paquete normal **no trae** esta capacidad. Hay que pedirla al compilar, agregando
`-p:HabilitarExportacionBaseDatos=true` al comando de siempre:

```powershell
# Temporales fuera del perfil de usuario. Igual que en la otra guía: si la ruta del
# perfil lleva acentos o eñes, sin esto la compilación falla entera.
mkdir D:\build-temp -Force
$env:TMP = 'D:\build-temp'; $env:TEMP = 'D:\build-temp'

# QA, con exportación
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=QA -p:HabilitarExportacionBaseDatos=true -p:AndroidPackageFormat=apk

# Desarrollo, con exportación
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=Desarrollo -p:HabilitarExportacionBaseDatos=true -p:AndroidPackageFormat=apk
```

Se combina con cualquier ambiente: la exportación y el servidor al que apunta el paquete son
dos cosas independientes.

Un valor que no sea `true` o `false` **aborta la compilación**; no se apaga en silencio.

**El paquete se llama distinto.** Los que pueden exportar llevan el sufijo `-exportaBD`:

| Comando | Archivo |
|---|---|
| Con `-p:HabilitarExportacionBaseDatos=true` | `AppOperador-QA-2026-08-18-exportaBD.apk` |
| Sin la propiedad | `AppOperador-QA-2026-08-18.apk` |

Así se distinguen de un vistazo en una carpeta compartida, y publicar uno no sobrescribe al
otro. La compilación con exportación imprime además un aviso al terminar.

> **Los paquetes de Debug ya la traen encendida.** `dotnet build ... -c Debug` produce un
> paquete con el botón visible sin pedir nada. En Release siempre hay que pedirla.

---

## 2. Exportar, en el teléfono

1. Abrir la app y entrar con el operador.
2. Ir a la pestaña **Perfil** y bajar hasta la tarjeta **Diagnostico**.
3. Tocar **Exportar base de datos**.
4. Leer el aviso y confirmar con **Exportar**.
5. El sistema abre su selector de documentos. Elegir dónde guardar —**Descargas** es lo más
   cómodo para sacarlo después— y aceptar.
6. La app confirma con el nombre del archivo, la versión de esquema, cuántas tablas trae y
   el tamaño.

Si la tarjeta **Diagnostico** no aparece, el paquete instalado no es uno de los del punto 1.

El nombre que se propone es del estilo `AppOperador-QA-20260818-130500-esquema3.db3`: trae el
ambiente, la fecha, la hora y la versión del esquema, que es lo que hace falta para saber qué
se está mirando semanas después. **No lleva el nombre del operador ni la unidad**, porque el
nombre del archivo se ve en carpetas compartidas.

---

## 3. Traer el archivo al equipo

Cualquiera de las tres sirve; la primera es la más rápida si el teléfono está conectado:

```powershell
# Si se guardó en Descargas
adb pull /sdcard/Download/AppOperador-QA-20260818-130500-esquema3.db3 D:\revision\
```

- **Cable, sin adb:** el teléfono montado como almacenamiento, y copiar desde `Download`.
- **Sin cable:** compartir el archivo desde la app de Archivos del teléfono.

### En el emulador LDPlayer 9

LDPlayer trae su propio `adb`, aunque no quede en el `PATH`. Desde **PowerShell**:

```powershell
# Ver qué hay exportado
& "D:\LDPlayer\LDPlayer9\adb.exe" shell ls -l /sdcard/Download/

# Traerlo
& "D:\LDPlayer\LDPlayer9\adb.exe" pull /sdcard/Download/<archivo>.db3 "D:\revision\"

# Y borrarlo del emulador al terminar
& "D:\LDPlayer\LDPlayer9\adb.exe" shell rm /sdcard/Download/<archivo>.db3
```

> **En Git Bash estos comandos fallan**: convierte `/sdcard/Download/` a una ruta de Windows
> antes de pasarla. Usar PowerShell.

Sin comandos, la barra lateral de LDPlayer tiene un botón de compartir archivos que abre una
carpeta visible desde Windows y desde el emulador.

---

## 4. Abrirlo

Con **DB Browser for SQLite** (o cualquier visor: DBeaver, la extensión SQLite de VS Code).

Se abre con *Abrir base de datos*, **sin contraseña**. Si el programa pide una, el archivo que
se copió no es el exportado sino la base cifrada del teléfono: repetir desde el punto 2.

Dentro están las seis tablas del esquema 3:

| Tabla | Qué tiene |
|---|---|
| `incidencia_local` | Incidencias y borradores capturados |
| `evidencia_local` | Metadatos de evidencias (hoy sin flujo que la escriba) |
| `intento_sincronizacion` | Resultado de cada intento de envío a Jacob |
| `evento_auditoria` | Bitácora local, máximo 200 eventos |
| `catalogo_tipo_incidencia` | Tipos de incidencia disponibles |
| `SesionLocal` | La sesión recuperable, una sola fila |

Las relaciones entre incidencia, evidencia e intentos son **lógicas**: se siguen por el `uuid`
de la incidencia, no hay claves foráneas declaradas y por eso el visor no dibuja el diagrama
solo.

Las fechas operativas se guardan como *ticks* UTC, no como texto. Para leerlas:

```sql
SELECT clave_local,
       datetime((marca_utc / 10000000) - 62135596800, 'unixepoch') AS capturada_utc,
       estado, gravedad, operador
FROM incidencia_local
ORDER BY marca_utc DESC;
```

---

## 5. Cuidados

**La copia no está cifrada.** Trae incidencias, operador, unidad y bitácora en claro, y desde
que se guarda queda fuera del resguardo de la app: cualquiera que tenga el archivo lo lee.

- Borrarla del teléfono en cuanto se haya copiado al equipo.
- Borrarla del equipo al terminar la revisión.
- No adjuntarla a un ticket ni dejarla en una carpeta compartida.
- **No dejar instalado en un teléfono de operación un paquete que pueda exportar.** Para eso
  están los paquetes normales, que no traen la capacidad dentro.

La copia temporal que la app crea mientras se elige el destino se borra sola, tanto si se
guardó como si se canceló o si algo falló a media escritura.

---

## 6. Si algo sale mal

| Qué pasa | Qué significa |
|---|---|
| No aparece la tarjeta **Diagnostico** | El paquete instalado no trae la capacidad. Ver el punto 1 |
| «No se guardó la copia» | Se canceló el selector. No queda nada sin cifrar en el teléfono |
| «La copia no es un archivo SQLite estándar» | La conversión salió mal y **no se entrega el archivo**. Reportarlo |
| «A la copia le faltan objetos del esquema» | Igual que el anterior: la comprobación detuvo la entrega |
| El visor pide contraseña | Se copió la base del teléfono, no la exportada. Repetir desde el punto 2 |

La app comprueba la copia antes de ofrecerla —firma del archivo, integridad, versión de
esquema y que estén todas las tablas e índices— y si algo no cuadra prefiere no entregar nada
antes que entregar un archivo del que se sacarían conclusiones falsas.
