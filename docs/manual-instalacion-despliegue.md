# JTT-1338 - Manual de instalación y despliegue inicial

## 1. Objetivo

Este documento describe los pasos necesarios para obtener, restaurar, compilar, probar y ejecutar la estructura inicial de la App Operador.

El manual corresponde al alcance inicial definido en la JTT-1338 y cubre:

- Obtención del proyecto desde Bitbucket.
- Preparación del entorno de desarrollo.
- Restauración de dependencias y workloads.
- Compilación de la solución.
- Ejecución de pruebas unitarias y de integración.
- Ejecución de la aplicación en Android.
- Generación de un APK de depuración.
- Inicialización de la base de datos SQLite.
- Solución de errores comunes.

No contempla firma productiva, publicación en tiendas, certificados de distribución, CI/CD definitivo ni integración productiva con el API de Jacob.

---

## 2. Repositorio

El código fuente se encuentra publicado en el repositorio corporativo de Bitbucket:

[https://bitbucket.org/ipte-desarrollo/app-operador/src/main/](https://bitbucket.org/ipte-desarrollo/app-operador/src/main/)

Rama base:

```text
main
```

Para clonar el proyecto:

```bash
git clone https://bitbucket.org/ipte-desarrollo/app-operador.git
cd app-operador
```

Cuando el repositorio solicite autenticación, deberá utilizarse la cuenta corporativa autorizada o el mecanismo de acceso configurado por el equipo.

---

## 3. Tecnologías utilizadas

La estructura inicial utiliza las siguientes tecnologías:

| Componente                      | Tecnología                   |
| ------------------------------- | ---------------------------- |
| Plataforma principal            | .NET MAUI                    |
| Framework                       | .NET 10                      |
| Patrón de presentación          | MVVM                         |
| Organización interna            | Clean Architecture           |
| Toolkit de interfaz             | CommunityToolkit.Maui 13.0.0 |
| Toolkit MVVM                    | CommunityToolkit.Mvvm 8.4.2  |
| Persistencia local              | SQLite                       |
| Acceso a SQLite                 | sqlite-net-pcl 1.11.285      |
| Pruebas                         | xUnit                        |
| Pruebas de arquitectura         | NetArchTest.Rules            |
| Simulación de dependencias      | NSubstitute                  |
| Plataforma inicial de ejecución | Android                      |

El proyecto también declara destinos para iOS, Mac Catalyst y Windows cuando el sistema de compilación correspondiente se encuentra disponible. La validación inicial de esta historia se concentra en Android.

---

## 4. Estructura de la solución

La solución se encuentra organizada en los siguientes proyectos:

```text
AppOperador.Domain
AppOperador.Aplicacion
AppOperador.Infrastructure
AppOperador.Mobile
AppOperador.UnitTests
AppOperador.IntegrationTests
```

### 4.1 AppOperador.Domain

Contiene las reglas y objetos centrales del dominio.

Esta capa no depende de MAUI, SQLite, servicios externos ni componentes de infraestructura.

### 4.2 AppOperador.Aplicacion

Contiene los contratos, interfaces y modelos utilizados por los casos de uso de la aplicación.

Define las abstracciones que posteriormente son implementadas por infraestructura o por servicios simulados.

### 4.3 AppOperador.Infrastructure

Contiene los adaptadores de infraestructura, principalmente la persistencia local mediante SQLite.

### 4.4 AppOperador.Mobile

Contiene la aplicación .NET MAUI:

- Vistas XAML.
- ViewModels.
- Navegación.
- Registro de dependencias.
- Recursos visuales.
- Configuración específica de cada plataforma.
- Servicios simulados utilizados durante el desarrollo inicial.

### 4.5 AppOperador.UnitTests

Contiene pruebas unitarias de dominio, aplicación y arquitectura.

Este proyecto no referencia directamente la infraestructura, con el objetivo de conservar el aislamiento de las capas internas.

### 4.6 AppOperador.IntegrationTests

Contiene pruebas de los adaptadores de infraestructura contra una base SQLite real y temporal.

Estas pruebas no utilizan la base de datos privada de la aplicación instalada en el dispositivo.

---

## 5. Prerrequisitos

Para trabajar con la solución se requiere:

- Git.
- .NET SDK 10.
- Workload de .NET MAUI.
- Android SDK.
- Java Development Kit compatible con el Android SDK instalado.
- Emulador Android o dispositivo físico autorizado para depuración.
- Visual Studio con soporte para .NET MAUI o una terminal con las herramientas de .NET y Android correctamente configuradas.

Para comprobar la instalación de .NET:

```bash
dotnet --version
```

El resultado deberá mostrar una versión compatible con .NET 10.

Para consultar los workloads instalados:

```bash
dotnet workload list
```

Debe aparecer el workload de MAUI o los componentes correspondientes a Android.

Cuando MAUI no se encuentre instalado:

```bash
dotnet workload install maui
```

Después de clonar el repositorio, se recomienda restaurar los workloads requeridos por el proyecto:

```bash
dotnet workload restore
```

---

## 6. Restauración de dependencias

Desde la carpeta raíz del repositorio:

```bash
dotnet restore app-operador.slnx
```

Este comando restaura las dependencias NuGet utilizadas por los proyectos de dominio, aplicación, infraestructura, interfaz móvil y pruebas.

La restauración debe finalizar sin errores.

Las advertencias deberán revisarse antes de continuar, especialmente aquellas relacionadas con versiones vulnerables o incompatibles de SQLite.

---

## 7. Compilación inicial

Para compilar la aplicación Android en configuración de depuración:

```bash
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj \
  -f net10.0-android \
  -c Debug
```

En PowerShell puede ejecutarse en una sola línea:

```powershell
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj -f net10.0-android -c Debug
```

La compilación correcta debe finalizar con:

```text
0 errores
```

Las salidas se generan dentro de:

```text
AppOperador.Mobile/bin/Debug/net10.0-android/
```

También puede abrirse el archivo:

```text
app-operador.slnx
```

desde Visual Studio y seleccionar `AppOperador.Mobile` como proyecto de inicio.

---

## 8. Ejecución de pruebas

### 8.1 Pruebas unitarias y de arquitectura

Ejecutar:

```bash
dotnet test AppOperador.UnitTests/AppOperador.UnitTests.csproj -c Debug
```

Estas pruebas validan:

- Reglas del dominio.
- Vigencia de la sesión offline.
- Prioridad y transición de registros de sincronización.
- Independencia de la capa de dominio.
- Aislamiento de los ViewModels.
- Referencias permitidas entre proyectos.

### 8.2 Pruebas de integración de SQLite

Ejecutar:

```bash
dotnet test AppOperador.IntegrationTests/AppOperador.IntegrationTests.csproj -c Debug
```

Estas pruebas crean archivos SQLite temporales y validan los adaptadores reales de persistencia.

No utilizan ni modifican los datos almacenados por una instalación normal de la App Operador.

### 8.3 Criterio de éxito

Las pruebas deben finalizar sin errores.

Cuando una prueba falle, deberá corregirse la causa antes de generar el APK utilizado para revisión.

---

## 9. Ejecución en Android

### 9.1 Desde Visual Studio

1. Abrir `app-operador.slnx`.
2. Seleccionar `AppOperador.Mobile` como proyecto de inicio.
3. Seleccionar un emulador Android o dispositivo físico.
4. Seleccionar la configuración `Debug`.
5. Iniciar la aplicación.

### 9.2 Desde terminal

Con un emulador iniciado o un dispositivo conectado:

```bash
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj \
  -t:Run \
  -f net10.0-android \
  -c Debug
```

En PowerShell:

```powershell
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj -t:Run -f net10.0-android -c Debug
```

Para consultar los dispositivos detectados por Android Debug Bridge:

```bash
adb devices
```

El dispositivo deberá aparecer con estado:

```text
device
```

Cuando aparezca como `unauthorized`, deberá aceptarse la autorización de depuración desde el dispositivo físico.

---

## 10. Funcionamiento de la versión inicial

La versión correspondiente a esta arquitectura utiliza dos tipos de implementaciones.

### 10.1 Persistencia real

Las incidencias, evidencias, intentos de sincronización, eventos de auditoría y catálogo provisional se almacenan mediante SQLite.

La información guardada sobrevive al cierre y reapertura de la aplicación.

### 10.2 Servicios simulados

Mientras se completa la integración con Jacob, se utilizan servicios simulados para:

- Autenticación.
- Conectividad.
- Ubicación.
- Sesión inicial.

Estos adaptadores permiten recorrer el flujo visual y funcional sin depender todavía del canal móvil del API de Jacob.

La sustitución de estos servicios por implementaciones reales se realizará en las historias técnicas correspondientes, sin modificar las vistas ni los ViewModels que consumen sus interfaces.

---

## 11. Acceso simulado

Para validar el flujo inicial de autenticación pueden utilizarse los siguientes datos:

### Acceso autorizado

```text
Usuario: cualquier valor distinto de sinpermiso
Contraseña: demo
Unidad: cualquiera de las unidades disponibles
```

### Usuario sin permiso de uso de la aplicación

```text
Usuario: sinpermiso
Contraseña: demo
```

### Credencial inválida

Utilizar cualquier contraseña distinta de:

```text
demo
```

La aplicación mostrará el mensaje de rechazo correspondiente.

Estos valores son exclusivamente de desarrollo y desaparecerán al integrar la autenticación real con Jacob.

---

## 12. Base de datos SQLite

La base de datos se crea automáticamente durante el primer uso de los repositorios locales.

Nombre del archivo:

```text
appoperador.db3
```

La ubicación se obtiene mediante:

```text
FileSystem.AppDataDirectory
```

La versión inicial del esquema es:

```text
1
```

Las tablas iniciales son:

- `incidencia_local`
- `evidencia_local`
- `intento_sincronizacion`
- `evento_auditoria`
- `catalogo_tipo_incidencia`

Cuando la base se encuentra vacía, la aplicación agrega un catálogo provisional de tipos de incidencia.

Este catálogo será sustituido posteriormente por la información autorizada que entregue Jacob.

---

## 13. Limpieza de los datos locales

Para reiniciar completamente la aplicación durante desarrollo puede borrarse su almacenamiento desde la configuración de Android o mediante ADB.

```bash
adb shell pm clear com.companyname.appoperador.mobile
```

Esta operación elimina:

- La sesión local.
- La base SQLite.
- Las incidencias pendientes.
- Las evidencias almacenadas.
- La cola de sincronización.
- La bitácora.

Debe utilizarse únicamente en ambientes de desarrollo o prueba.

No deberá ejecutarse sobre un dispositivo que contenga información pendiente que deba conservarse.

---

## 14. Generación del APK de depuración

Para generar un APK destinado a pruebas internas:

```bash
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj \
  -f net10.0-android \
  -c Debug \
  -p:AndroidPackageFormats=apk
```

En PowerShell:

```powershell
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj -f net10.0-android -c Debug -p:AndroidPackageFormats=apk
```

El archivo resultante se genera dentro de:

```text
AppOperador.Mobile/bin/Debug/net10.0-android/
```

Para instalarlo manualmente:

```bash
adb install -r AppOperador.Mobile/bin/Debug/net10.0-android/<nombre-del-archivo>.apk
```

El nombre exacto del archivo puede variar según la versión del SDK y la configuración del empaquetador.

Este APK es únicamente para depuración y validación interna. No utiliza firma productiva ni está preparado para publicación en tiendas.

---

## 15. Permisos actuales de Android

La versión inicial declara los siguientes permisos:

```text
android.permission.ACCESS_NETWORK_STATE
android.permission.INTERNET
```

La ubicación y la captura de evidencias todavía se encuentran desacopladas mediante servicios simulados o implementaciones provisionales.

Los permisos definitivos de ubicación, cámara y archivos deberán agregarse al implementar las historias funcionales que utilicen directamente estas capacidades del dispositivo.

---

## 16. Configuración y secretos

La versión inicial no requiere una URL productiva del API de Jacob porque la autenticación y comunicación se encuentran simuladas.

No deben incluirse en el repositorio:

- Contraseñas.
- Tokens de acceso.
- Refresh tokens.
- Llaves privadas.
- Cadenas de conexión productivas.
- Datos personales de operadores.
- Evidencias capturadas durante operación real.

Al implementar el API, los valores por ambiente deberán manejarse mediante configuración externa o almacenamiento seguro.

---

## 17. Errores comunes

### 17.1 No se encuentra el workload de MAUI

Síntoma:

```text
NETSDK1147
```

Solución:

```bash
dotnet workload install maui
dotnet workload restore
```

Después:

```bash
dotnet restore app-operador.slnx
```

### 17.2 No se detecta un dispositivo Android

Comprobar:

```bash
adb devices
```

Verificar que:

- El emulador se encuentre iniciado.
- La depuración USB esté habilitada.
- El dispositivo haya autorizado el equipo.
- El Android SDK esté configurado correctamente.

### 17.3 Error de SQLite relacionado con libc.so.6

Síntoma aproximado:

```text
dlopen failed: library "libc.so.6" not found
```

La solución ya fija las dependencias SQLite correctas para Android y excluye los binarios Linux incompatibles.

Cuando el error reaparezca después de modificar dependencias:

1. No eliminar las referencias explícitas a `SQLitePCLRaw.lib.e_sqlite3.android`.
2. No reducir las librerías SQLitePCLRaw a la versión vulnerable anterior.
3. Borrar las carpetas `bin` y `obj`.
4. Restaurar nuevamente.
5. Recompilar el proyecto Android.

Ejemplo en PowerShell:

```powershell
Get-ChildItem -Recurse -Directory -Filter bin | Remove-Item -Recurse -Force
Get-ChildItem -Recurse -Directory -Filter obj | Remove-Item -Recurse -Force
dotnet restore app-operador.slnx
dotnet build AppOperador.Mobile/AppOperador.Mobile.csproj -f net10.0-android -c Debug
```

### 17.4 La base SQLite contiene información anterior

Limpiar los datos de la aplicación desde Android o ejecutar:

```bash
adb shell pm clear com.companyname.appoperador.mobile
```

Esta acción borra toda la información local.

---

## 18. Restricciones de la versión inicial

La estructura publicada todavía no representa una versión productiva.

Se encuentran fuera del alcance de este manual:

- Autenticación real contra Jacob.
- JWT productivo.
- Configuración definitiva de ambientes.
- Sincronización real con el backend.
- Catálogos productivos.
- Firma de APK o AAB para distribución.
- Publicación en Google Play o App Store.
- Certificados de iOS.
- Automatización de CI/CD.
- Matriz definitiva de dispositivos compatibles.

Estas funcionalidades se implementarán y documentarán dentro de sus historias técnicas correspondientes.

---

## 19. Lista de validación

Antes de considerar instalado correctamente el proyecto, comprobar:

- [ ] El repositorio fue clonado desde Bitbucket.
- [ ] Se encuentra seleccionada la rama `main`.
- [ ] El SDK de .NET 10 está instalado.
- [ ] El workload de MAUI está instalado.
- [ ] Las dependencias fueron restauradas.
- [ ] La aplicación Android compila sin errores.
- [ ] Las pruebas unitarias finalizan correctamente.
- [ ] Las pruebas de integración finalizan correctamente.
- [ ] La aplicación inicia en un emulador o dispositivo Android.
- [ ] La pantalla de acceso permite seleccionar una unidad.
- [ ] La contraseña de desarrollo `demo` permite recorrer el acceso simulado.
- [ ] La base SQLite conserva la información después de reiniciar la aplicación.
- [ ] Se genera el APK de depuración.

---

## 20. Evidencia relacionada

### Jira

```text
JTT-1338 — Arquitectura
```

### Repositorio

[https://bitbucket.org/ipte-desarrollo/app-operador/src/main/](https://bitbucket.org/ipte-desarrollo/app-operador/src/main/)

### Documentación relacionada

- Arquitectura base de la App Operador móvil.
- Diseño del canal móvil en el API de Jacob.
- Definición funcional de autenticación y sesión.
- Historias técnicas de frontend, base de datos e integración con Jacob.

---

## 21. Resultado

Con la publicación de este manual se documentan los pasos iniciales para instalar, compilar, probar, ejecutar y empaquetar la App Operador.

El documento, junto con la arquitectura publicada en Confluence y la solución versionada en Bitbucket, completa la evidencia correspondiente a la JTT-1338.
