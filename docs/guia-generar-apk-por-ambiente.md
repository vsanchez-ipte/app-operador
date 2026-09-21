# Cómo generar el APK para cada ambiente

> Guía corta para quien necesite armar un paquete de la App Operador sin conocer el proyecto.
> Para instalarlo y probarlo, ver la guía del emulador Android que acompaña a esta.

**La dirección del servidor ya no se edita en el código.** Se elige al compilar, con la
propiedad `Ambiente`. Un mismo código fuente produce el paquete de cualquier ambiente.

Hay una segunda propiedad, `HabilitarExportacionBaseDatos`, que agrega una herramienta para
revisar la base de datos local. Es independiente del ambiente y está apagada salvo que se
pida. Ver el punto 6.

---

## 1. Los comandos

Desde la raíz del repositorio (`c:\repos\app-operador`), en PowerShell. **Copiar el bloque
completo, incluidas las dos primeras líneas:**

```powershell
# Temporales fuera del perfil de usuario. Ver el punto 5: si la ruta del perfil
# lleva acentos o eñes, sin esto la compilación falla entera.
mkdir D:\build-temp -Force
$env:TMP = 'D:\build-temp'; $env:TEMP = 'D:\build-temp'

# Desarrollo
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=Desarrollo -p:AndroidPackageFormat=apk

# QA
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=QA -p:AndroidPackageFormat=apk
```

Es el mismo comando: **solo cambia la palabra después de `-p:Ambiente=`.**

Las dos primeras líneas valen para la terminal abierta: al abrir otra, hay que repetirlas.
En una máquina cuyo usuario no lleve acentos no hacen falta, pero tampoco estorban.

| Ambiente | A dónde apunta | Para qué |
|---|---|---|
| `Desarrollo` | `http://192.168.100.215:81` | Servidor de desarrollo |
| `QA` | `http://192.168.100.230:81` | Servidor de calidad, donde prueba QA |
| `Simulado` | — sin servidor | Mostrar las pantallas sin nada levantado |
| `Local` | el API de la propia máquina | Es el valor por omisión: si no se pone la propiedad, sale este |

### Si además se necesita revisar la base de datos

Se agrega `-p:HabilitarExportacionBaseDatos=true` al mismo comando. Se combina con cualquier
ambiente:

```powershell
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=QA -p:HabilitarExportacionBaseDatos=true -p:AndroidPackageFormat=apk
```

Ese paquete trae un botón que entrega una copia legible de la base local. **La copia no está
cifrada**, así que no es un paquete para dejar instalado en un teléfono de operación. El
detalle está en `guia-exportar-base-datos.md`.

## 2. Dónde queda el archivo

```
AppOperador.Mobile\bin\Release\net10.0-android\publish\
```

Ahí aparecen tres `.apk`. **El que se entrega es el que lleva el nombre con el ambiente y la
fecha:**

```
AppOperador-QA-2026-08-13.apk                      <- este
com.companyname.appoperador.mobile-Signed.apk      lo genera el SDK; mismo contenido
com.companyname.appoperador.mobile.apk             sin firmar, Android lo rechaza
```

La compilación lo nombra sola, con el patrón
`AppOperador-<Ambiente>-<aaaa-MM-dd>[-exportaBD].apk`, y lo anuncia al terminar:

```
Paquete para entregar: bin\Release\net10.0-android\publish\AppOperador-QA-2026-08-13.apk
```

**El sufijo `-exportaBD` aparece solo si se pidió la herramienta de base de datos**, y en ese
caso la compilación agrega un aviso:

```
Paquete para entregar: ...\AppOperador-QA-2026-08-18-exportaBD.apk
AVISO: este paquete puede exportar la base local sin cifrar. No dejarlo instalado en un
       telefono de operacion.
```

Los que empiezan con `com.companyname` son los que arma el SDK de Android a partir del
identificador de la aplicación. No se borran porque el IDE los usa para instalar y depurar,
pero **no son los que se mandan**: entre dos ambientes se ven idénticos.

> **La carpeta siempre se llama igual.** Si se generan dos ambientes seguidos, los archivos
> `com.companyname...` se pisan; los nombrados no, mientras cambien de ambiente, de día o de
> sufijo. Aun así, conviene sacar el paquete de ahí en cuanto sale.

> **Cuidado con los paquetes anteriores al 18 de agosto de 2026.** El sufijo se agregó ese
> día. Un paquete con herramienta de base de datos armado antes lleva el nombre de uno normal
> y no hay forma de distinguirlos por el archivo. Ver el punto 3 para comprobarlo.

## 3. Cómo comprobar que apunta a donde debe

**En la app:** abrir la pantalla **Perfil**. La versión lleva el ambiente al lado:

```
1.0 · QA            -> paquete de QA
1.0 · Desarrollo    -> paquete de Desarrollo
1.0                 -> paquete local, sin ambiente
```

Se puso justamente para esto: dos paquetes instalados se ven idénticos, y es fácil revisar
uno creyendo que es el otro.

**Antes de entregarlo,** si se quiere confirmar sin instalar nada, buscar la dirección dentro
del ensamblado publicado:

```powershell
$b = [IO.File]::ReadAllBytes('AppOperador.Mobile\bin\Release\net10.0-android\AppOperador.Mobile.dll')
[Text.Encoding]::Unicode.GetString($b).Contains('192.168.100.230')   # True si es el de QA
```

### Si trae o no la herramienta de base de datos

**En la app:** abrir **Perfil** y bajar hasta el final. Si aparece una tarjeta **Diagnostico**
con el botón «Exportar base de datos», el paquete la trae. Si después de la tarjeta
**Auditoria** no hay nada más, no la trae.

**Sobre el archivo,** sin instalarlo. Sirve sobre todo para paquetes anteriores al 18 de
agosto de 2026, que no llevan el sufijo en el nombre:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($f in Get-ChildItem *.apk) {
    $zip = [IO.Compression.ZipFile]::OpenRead($f.FullName)
    $n = ($zip.Entries | Where-Object { $_.FullName -like '*HttpUtility*' }).Count
    $zip.Dispose()
    "{0,-45} {1}" -f $f.Name, $(if ($n -gt 0) { "SI exporta" } else { "no exporta" })
}
```

Busca una librería que solo entra al paquete cuando la herramienta está encendida, porque la
arrastra el selector de archivos que usa para guardar la copia. Si la herramienta está
apagada, nada del código llega hasta ahí y esa librería no se incluye.

## 4. Si una propiedad está mal escrita

**La compilación se detiene.** Con el ambiente:

```
error : Ambiente 'Produccion' no reconocido. Use Local, Desarrollo, QA o Simulado.
```

Y con la herramienta de base de datos, que solo admite `true` o `false`:

```
error : HabilitarExportacionBaseDatos '1' no es valido. Use true o false.
```

Es deliberado en los dos casos. Si el ambiente cayera a `Local` en silencio, saldría un
paquete apuntando a la máquina de quien lo compiló y nadie lo notaría hasta que fallara en
manos de otro. Y un `1` o un `True ` con espacio se tomarían como apagado, de modo que el
paquete saldría sin la herramienta que se pidió.

## 5. El error del AOT y los acentos del perfil

Si la ruta del perfil de Windows lleva acentos o eñes —por ejemplo
`C:\Users\VíctorAlfonsoSánchez`— y **no** se ejecutaron las dos primeras líneas del punto 1,
la compilación falla con veintitantos errores como este, uno por ensamblado:

```
error : Precompiling failed for ...\linked\AppOperador.Aplicacion.dll with exit code 1.
        The specified response file can not be read
```

Asusta por la cantidad, pero **la causa es una sola**: la precompilación AOT escribe un
archivo de respuesta en la carpeta temporal del usuario y no logra leerlo de vuelta cuando la
ruta tiene caracteres no ASCII. Mandando los temporales a `D:\build-temp` desaparece.

La solución está en el punto 1. Si ya falló, basta con ejecutar esas dos líneas y repetir el
comando; no hace falta limpiar nada. Si aun así se repite, borrar
`AppOperador.Mobile\obj\Release` y volver a intentar.

Salida de emergencia si aún así falla: agregar `-p:RunAOTCompilation=false
-p:AndroidEnableProfiledAot=false`. Da un APK válido y algo más lento (~25 MB en vez de ~32).

**En una máquina cuyo usuario no lleve acentos, nada de esto hace falta.**

## 6. La herramienta de base de datos, en corto

Existe porque la base local va cifrada y su clave la guarda el sistema operativo, que no la
entrega: copiar el archivo del teléfono no permite abrirlo. La conversión la hace la propia
app, y por eso hay que pedirla al compilar.

| | |
|---|---|
| Cómo se pide | `-p:HabilitarExportacionBaseDatos=true` |
| Se combina con | cualquier ambiente |
| Por omisión | apagada en Release, encendida en Debug |
| Cómo se reconoce el paquete | sufijo `-exportaBD`, y tarjeta «Diagnostico» en Perfil |
| Qué produce | una copia de la base **sin cifrar**, legible con cualquier visor |

**Va en su propia propiedad y no en el ambiente a propósito.** Derivarla de `QA` haría que
todo paquete de QA pudiera dejar la base en claro sin que nadie lo hubiera pedido.

Con la propiedad apagada, ese código no queda dentro del paquete: el interruptor también
apaga la parte que lo implementa.

Cómo usarla, cómo traer el archivo al equipo y qué cuidados tiene: `guia-exportar-base-datos.md`.

> **Para comparar dos paquetes hay que compilarlos en limpio.** Publicar uno tras otro sobre
> la misma carpeta deja restos de la compilación anterior y los paquetes salen con distinto
> número de archivos sin que el código lo justifique. Borrar
> `AppOperador.Mobile\obj\Release\net10.0-android` antes de cada publicación.

## 7. Requisitos de la máquina

- .NET SDK con el workload de MAUI instalado (`dotnet workload list` debe mostrar `maui`).
- No hace falta Visual Studio ni el emulador para *generar* el paquete: solo para probarlo.
- La primera compilación tarda varios minutos; las siguientes son mucho más rápidas.
