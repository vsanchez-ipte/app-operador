# Cómo generar el APK para cada ambiente

> Guía corta para quien necesite armar un paquete de la App Operador sin conocer el proyecto.
> Para instalarlo y probarlo, ver la guía del emulador Android que acompaña a esta.

**La dirección del servidor ya no se edita en el código.** Se elige al compilar, con la
propiedad `Ambiente`. Un mismo código fuente produce el paquete de cualquier ambiente.

---

## 1. Los comandos

Desde la raíz del repositorio (`c:\repos\app-operador`), en PowerShell:

```powershell
# Desarrollo
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=Desarrollo -p:AndroidPackageFormat=apk

# QA
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=QA -p:AndroidPackageFormat=apk
```

Es el mismo comando: **solo cambia la palabra después de `-p:Ambiente=`.**

| Ambiente | A dónde apunta | Para qué |
|---|---|---|
| `Desarrollo` | `http://192.168.100.215:81` | Servidor de desarrollo |
| `QA` | `http://192.168.100.230:81` | Servidor de calidad, donde prueba QA |
| `Simulado` | — sin servidor | Mostrar las pantallas sin nada levantado |
| `Local` | el API de la propia máquina | Es el valor por omisión: si no se pone la propiedad, sale este |

## 2. Dónde queda el archivo

```
AppOperador.Mobile\bin\Release\net10.0-android\publish\
```

Ahí aparecen dos `.apk`. **El que se instala es el que termina en `-Signed.apk`**; el otro no
está firmado y Android lo rechaza.

> **Esa carpeta siempre se llama igual.** Si se generan dos ambientes seguidos, el segundo
> pisa al primero. Conviene copiar el APK y renombrarlo en cuanto sale, por ejemplo
> `AppOperador-QA-2026-08-13.apk`.

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

## 4. Si el nombre del ambiente está mal escrito

**La compilación se detiene**, con este mensaje:

```
error : Ambiente 'Produccion' no reconocido. Use Local, Desarrollo, QA o Simulado.
```

Es deliberado: si cayera a `Local` en silencio, saldría un paquete apuntando a la máquina de
quien lo compiló y nadie lo notaría hasta que fallara en manos de otro.

## 5. Nota para máquinas con acentos en el nombre de usuario

Si la ruta del perfil de Windows lleva acentos o eñes —por ejemplo
`C:\Users\VíctorAlfonsoSánchez`—, la precompilación AOT falla en **todos** los ensamblados
con *«The specified response file can not be read»*. En ese caso, mandar los temporales a
otra ruta antes de publicar:

```powershell
mkdir D:\build-temp -Force
$env:TMP = 'D:\build-temp'; $env:TEMP = 'D:\build-temp'
# y luego el comando del punto 1
```

Salida de emergencia si aún así falla: agregar `-p:RunAOTCompilation=false
-p:AndroidEnableProfiledAot=false`. Da un APK válido y algo más lento (~25 MB en vez de ~32).

**En una máquina cuyo usuario no lleve acentos, nada de esto hace falta.**

## 6. Requisitos de la máquina

- .NET SDK con el workload de MAUI instalado (`dotnet workload list` debe mostrar `maui`).
- No hace falta Visual Studio ni el emulador para *generar* el paquete: solo para probarlo.
- La primera compilación tarda varios minutos; las siguientes son mucho más rápidas.
