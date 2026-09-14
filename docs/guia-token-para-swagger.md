# Cómo obtener el token de la App Operador para probar en Swagger

> Guía corta para autorizarse en Swagger y probar los endpoints del canal móvil sin la app.
> Para armar el paquete, ver la guía de APK por ambiente que acompaña a esta.

**A Swagger no se puede entrar tecleando la contraseña.** `POST /ITS/AppLogin/Preauth` no la
acepta en claro: la espera **cifrada con RSA-OAEP-SHA256 y codificada en Base64**. Mandarla tal
cual responde `400` con `appoperador.credencial.ilegible`, que no es un fallo del ambiente ni de
la cuenta. Y el acceso son dos pasos: la preautenticación devuelve un desafío y las unidades del
operador, y el segundo paso los consume para abrir la sesión.

La app ya hace las dos cosas y ya tiene el token de una sesión válida. Por eso el token se saca
de la app y no se fabrica a mano.

> ⚠️ **El token es una credencial.** Quien lo tenga puede actuar como ese operador contra Jacob
> hasta que la sesión termine. Se trata como una contraseña: no se pega en un ticket, ni en una
> página de Confluence, ni en un chat abierto.

---

## 1. Armar el paquete que puede copiar el token

> **Este punto es para quien compila los paquetes.** Si el paquete ya está entregado, se puede
> saltar directo al punto 2.

Un paquete normal **no trae** esta capacidad. Hay que pedirla al compilar, agregando
`-p:HabilitarCopiaToken=true` al comando de siempre:

```powershell
# Temporales fuera del perfil de usuario. Igual que en las otras guías: si la ruta del
# perfil lleva acentos o eñes, sin esto la compilación falla entera.
mkdir D:\build-temp -Force
$env:TMP = 'D:\build-temp'; $env:TEMP = 'D:\build-temp'

# QA, con copia de token
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=QA -p:HabilitarCopiaToken=true -p:AndroidPackageFormat=apk

# Desarrollo, con copia de token
dotnet publish AppOperador.Mobile -f net10.0-android -c Release -p:Ambiente=Desarrollo -p:HabilitarCopiaToken=true -p:AndroidPackageFormat=apk
```

Se combina con cualquier ambiente y con la exportación de base de datos: son interruptores
independientes.

Un valor que no sea `true` o `false` **aborta la compilación**; no se apaga en silencio.

**El paquete se llama distinto.** Los que pueden copiar el token llevan el sufijo `-token`:

| Comando | Archivo |
|---|---|
| Con `-p:HabilitarCopiaToken=true` | `AppOperador-QA-2026-09-09-token.apk` |
| Con las dos capacidades | `AppOperador-QA-2026-09-09-exportaBD-token.apk` |
| Sin la propiedad | `AppOperador-QA-2026-09-09.apk` |

Así se distinguen de un vistazo en una carpeta compartida, y publicar uno no sobrescribe al
otro. La compilación imprime además un aviso al terminar.

> **Los paquetes de Debug ya la traen encendida.** En Release siempre hay que pedirla.

**El token que salga es el del ambiente del paquete.** Un APK de QA da un token que solo sirve
contra el Swagger de QA; contra el de desarrollo responde `401`.

---

## 2. Copiar el token, en el teléfono

1. Abrir la app y entrar con el operador **que tenga los dos permisos**.
2. Ir a la pestaña **Perfil** y bajar hasta la tarjeta **Token de sesion**.
3. Tocar **Copiar token de sesion** y confirmar en el aviso.
4. La app dice qué módulos trae firmados el token. **Se esperan dos:**
   `APP_OPERADOR_MOVIL` y `APP_OPERADOR_CAPTURA`.

**Si solo aparece `APP_OPERADOR_MOVIL`, detenerse aquí.** Es un problema de permisos del
operador, no del token: con ese token todos los `POST` responden
`appincidencias.permiso.revocado` y medio recorrido de Swagger no prueba nada. Hay que pedir el
permiso de captura para esa cuenta y volver a ingresar.

**Para pasarlo a la computadora donde corre Swagger:**

| Dónde corre la app | Cómo |
|---|---|
| Emulador de Android | El portapapeles ya es el de la computadora: pegar directo. |
| Teléfono físico | Usar **Compartir** en el mismo aviso, por un medio de la empresa. |

La copia queda anotada en la bitácora de la app, con nivel de advertencia. **El token no se
escribe en la bitácora ni en ningún log**: solo consta que se copió.

---

## 3. Autorizarse en Swagger

| Ambiente | Swagger |
|---|---|
| Desarrollo | `http://192.168.100.215:81/swagger` |
| QA | `http://192.168.100.230:81/swagger` |

1. Abrir el Swagger del ambiente que corresponda.
2. Botón **Authorize**, arriba a la derecha.
3. Pegar el token. Si el campo pide también el esquema, va `Bearer ` y luego el token.
4. **Authorize** y **Close**. A partir de ahí todas las peticiones salen firmadas.

Para comprobar que quedó bien, `GET /ITS/AppCatalogos/Vigentes`: sin cuerpo, y de su respuesta
salen los identificadores que piden los demás endpoints.

---

## 4. Lo que conviene saber antes de probar

- **Sacar el token no le quita la sesión a nadie.** El canal móvil no limita a una sesión por
  operador, así que Swagger y la app pueden trabajar a la vez con la misma cuenta.
- **El token vence con la sesión.** Si Swagger empieza a responder `401`, la sesión de la app
  terminó: volver a ingresar y copiar el token otra vez. Revalidar la sesión desde la app
  **no** emite un token nuevo, así que el que ya se copió sigue sirviendo.
- **Cerrar sesión en la app invalida el token copiado.** El cierre lo revoca en el servidor.
- **`POST /ITS/AppIncidencias` es idempotente por `uuid`.** Repetir un `uuid` ya usado devuelve
  el folio viejo y la prueba parece pasar sin haber probado nada. Cambiarlo en cada corrida,
  salvo cuando se quiera probar justamente eso.
- **Un paquete con esta capacidad no se deja instalado en un teléfono de operación.** Es un
  paquete de prueba; al terminar, se desinstala o se reemplaza por el normal.
