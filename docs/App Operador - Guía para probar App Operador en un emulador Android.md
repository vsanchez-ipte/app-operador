# App Operador \- Guía para probar App Operador en un emulador Android

Esta guía explica cómo instalar y ejecutar el APK de **App Operador** en una computadora. El emulador recomendado para las pruebas de QA es **LDPlayer 9**.

Los casos disponibles, limitaciones de la versión y recorridos sugeridos se encuentran en el archivo `LEEME-QA.txt`, incluido junto con cada entrega.

## 1. Archivos necesarios

Descargue los archivos desde la carpeta autorizada de OneDrive:

[AppOperador](https://iptesolu-my.sharepoint.com/:f:/g/personal/vsanchez_ipte_com_mx/IgBJBBHXW__CTp8TXSP9tukCAXmY17WdmNunQqIgSkmxFzw?e=5DuXyo&xsdata=MDV8MDJ8fGFhMDMwZWYxZDlhYzRhNDRlNzFiMDhkZWY5NmQwYzVlfDY0MWRmYzFiNzlhNTRmNzg4MzY1NzNhZjdjMmQwMTI2fDB8MHw2MzkyMjI0NDM4ODc0ODY3NDd8VW5rbm93bnxWR1ZoYlhOVFpXTjFjbWwwZVZObGNuWnBZMlY4ZXlKRFFTSTZJbFJsWVcxelgwRlVVRk5sY25acFkyVmZVMUJQVEU5R0lpd2lWaUk2SWpBdU1DNHdNREF3SWl3aVVDSTZJbGRwYmpNeUlpd2lRVTRpT2lKUGRHaGxjaUlzSWxkVUlqb3hNWDA9fDF8TDJOb1lYUnpMekU1T2paaVltRXhZV000WmpOa05qUmtNVFJoWkdVNU9EbGlZV1k1TldRd1pXVXlRSFJvY21WaFpDNTJNaTl0WlhOellXZGxjeTh4TnpnMk5qUTNOVGczTVRnd3xkMDE0YWU2NzBlM2I0OTdiODQ2ZjA4ZGVmOTZkMGM1ZXwxYzA1MDcxNTk2YzY0ZWUwYWU4ZTdlOTI2NWMzNjYxNA%3D%3D&sdata=TzBtRms3N3JQZlBwRDFOdlVCNlpTMXdFdmRIR21qUFY3Uy92ZjVaWG1Qdz0%3D&ovuser=641dfc1b-79a5-4f78-8365-73af7c2d0126%2Cvsanchez%40ipte.com.mx)

La carpeta contiene:

- El APK de App Operador que se debe probar.
- El instalador de LDPlayer 9: `LDPlayer9_es_1009_ld.exe`.
- El archivo `LEEME-QA.txt` con la información específica de la entrega.

Antes de comenzar, lea el archivo `LEEME-QA.txt` y confirme que está utilizando el APK más reciente.

> 

## 2. Antes de instalar

- La computadora debe estar conectada a la red de la empresa o a la VPN.
- El emulador utiliza la conexión de la computadora. Si la computadora no puede acceder al servidor de QA, la aplicación tampoco podrá hacerlo.
- Se necesita una cuenta del ambiente de QA con permiso para usar App Operador y al menos una unidad asignada.
- Las cuentas del ambiente de desarrollo no funcionan en QA.
- Las cuentas con las que se realizan pruebas actualmente son <fvazquez@ipte.com.mx> y <operador.prueba@ipte.com.mx> Ambas cuentas comparten credenciales

## 3. Instalar y preparar LDPlayer 9

1. Ejecute el archivo `LDPlayer9_es_1009_ld.exe`.
2. Complete la instalación y abra LDPlayer 9.
3. Espere a que aparezca la pantalla principal de Android.
4. Confirme que la computadora siga conectada a la red de la empresa o a la VPN.

> **Sobre la publicidad**    
> LDPlayer y otros emuladores similares están pensados principalmente para ejecutar juegos de Android en una PC. Por ello, pueden mostrar anuncios, recomendaciones o accesos a contenido gaming. Estos elementos pertenecen al emulador y **no forman parte de App Operador**, por lo que no deben reportarse como defectos del APK. A pesar de esta publicidad, sus herramientas permiten instalar y probar el APK de forma sencilla.

No es necesario abrir los anuncios ni instalar las aplicaciones recomendadas por el emulador.

> Vista Home del emulador. Al entrar abre LDStore por defecto, se puede cerrar la pestaña de la aplicación en la parte superrios, o bien, precionar el botón Home ubicado en la parte inferior derecha en forma de círculo.
> 
> 

## 4. Instalar el APK

Puede utilizar cualquiera de estas opciones:

### Opción recomendada: arrastrar el archivo

1. Abra LDPlayer 9.
2. Localice el APK descargado desde OneDrive.
3. Arrastre el archivo sobre la ventana del emulador.
4. Espere a que termine la instalación.
5. Abra App Operador desde el nuevo icono.

### Opción alternativa: botón para instalar APK

1. Seleccione **Instalar APK** en la barra derecha de LDPlayer.
2. Busque el APK descargado.
3. Seleccione el archivo y espere a que finalice la instalación.

> Botón **Instalar APK** de LDPlayer.
> 
> Ícono de App Operador después de la instalación.
> 
> 

## 5. Comprobar que sea la versión correcta

1. Abra App Operador.
2. Conceda el permiso de ubicación cuando se solicite. Es obligatorio para el acceso en esta versión.
3. Inicie sesión con una cuenta del ambiente de QA.
4. Entre a la pantalla **Perfil**.
5. Confirme que la versión termine en **“QA”**.

Ejemplo esperado: `1.0 · QA`.

- Si termina en **“Desarrollo”**, el APK apunta al servidor de desarrollo y los resultados no sirven para esta ronda.
- Si muestra solamente **“1.0”**, sin un ambiente, corresponde a un paquete local y tampoco debe utilizarse.

> Pantalla Perfil mostrando una versión terminada en **QA**.

## 6. Consideraciones para las pruebas

- La ubicación de LDPlayer es simulada. Para las pruebas actuales de autenticación basta con conceder el permiso y mantener activo el servicio de ubicación.
- Si se probará el vencimiento de una sesión, entre a **Ajustes \> Sistema \> Fecha y hora** y desactive la fecha y hora automáticas. Esto permite adelantar el reloj del emulador.
- Para conocer los recorridos que pueden probarse y los comportamientos que no deben registrarse como defectos, consulte `LEEME-QA.txt`.

## 7. Otros emuladores posibles

LDPlayer 9 es el entorno de referencia, pero el APK también puede desplegarse en:

| Emulador | Forma de instalar el APK | Consideración |
| --- | --- | --- |
| **BlueStacks 5** | Arrastrar el APK o usar **Install APK** | También puede mostrar contenido y publicidad gaming. Usa más recursos, pero está más actualizado con versión Android 11 |
| **Android Emulator de Android Studio** | Arrastrar el APK o usar `adb install` | Requiere más configuración y recursos |
| **Genymotion Desktop** | Arrastrar el APK o usar `adb install` | Orientado a pruebas técnicas; puede requerir licencia |

Si se utiliza otro emulador, indique su nombre, versión y versión de Android en el reporte. Cuando un defecto solo ocurra en una alternativa, intente reproducirlo también en LDPlayer 9.

## 8. Si algo falla

Antes de reportar un defecto, compruebe:

- Que la computadora siga conectada a la red de la empresa o a la VPN.
- Que la versión mostrada en Perfil termine en **QA**.
- Que se utilice una cuenta del ambiente de QA.
- Que la cuenta tenga permiso de App Operador y una unidad asignada.
- Que el permiso de ubicación esté concedido.

En el reporte incluya la versión mostrada en Perfil, el emulador utilizado, el paso donde ocurrió el problema, el mensaje completo y si había conexión en ese momento.
