Resumen
-------

Este script permite llamar un endpoint REST de activación de PIM sin abrir navegador, autenticándose con un Service Principal (app registration) y usando `az rest`.

Requisitos previos
- Tener instalado `az cli` (compatible con su entorno corporativo).
- Tener creado un Service Principal con permisos para gestionar PIM/roles en el tenant (consentido por un COMPLETAR_GDC_HTTP_BASIC_USERNAME). Por ejemplo: `az ad sp create-for-rbac --name "pim-activator" --sdk-auth` y conceder permisos Microsoft Graph necesarios.
- Conceder al Service Principal permisos a la API que vaya a usar (Microsoft Graph / Privileged Identity Management). Muchos endpoints de PIM requieren consentimiento COMPLETAR_GDC_HTTP_BASIC_USERNAME y permisos en Graph (por ejemplo `PrivilegedAccess.ReadWrite.AzureResources`).

Variables de entorno
- `AZ_TENANT_ID` - Tenant ID
- `AZ_CLIENT_ID` - Client (App) ID
- `AZ_CLIENT_SECRET` - Client secret
- `ACTIVATION_URI` - URI del endpoint de activación (portal > inspeccionar o solicitado por la API)
- `ACTIVATION_BODY_FILE` - (opcional) ruta a fichero JSON con el body de la petición

Uso rápido (PowerShell)
``powershell
# exportar variables en la sesión
$env:AZ_TENANT_ID = "<tenant-id>"
$env:AZ_CLIENT_ID = "<client-id>"
$env:AZ_CLIENT_SECRET = "<client-secret>"
$env:ACTIVATION_URI = "https://graph.microsoft.com/beta/.../activate"

# Ejecutar la tarea desde VS Code: Terminal -> Run Task -> Activate PIM role (service principal)
# O ejecutar directamente el script:
powershell -ExecutionPolicy Bypass -File .\scripts\activate-pim.ps1
```
# Para autenticarse con tu usuario (device code flow) usa la nueva tarea o ejecuta:
# powershell -ExecutionPolicy Bypass -File .\scripts\activate-pim.ps1 -UseUser

Notas
- Dependiendo del endpoint exacto de activación puede ser necesario usar la ruta `beta` de Microsoft Graph y/o añadir campos concretos en el body. El script actúa como wrapper: hace login con SP y ejecuta `az rest --method POST` contra la URI indicada.
- Si su entorno bloquea `az login` por certificados, el login por Service Principal evita el flujo interactivo y no abre navegador.
- Si necesita que el script construya automáticamente la URI y el body para la activación de una asignación concreta, indíqueme el tipo de PIM (Azure resources PIM vs Azure AD roles) y yo lo implemento.

Listado de asignaciones aptas
-----------------------------
He añadido `scripts\list-pim-eligible.ps1`, que intenta consultar varios endpoints beta de Microsoft Graph para localizar tus asignaciones aptas de PIM. Usa la tarea VS Code "List PIM eligible assignments (user)" y completa el device-code login cuando se solicite.

Si la petición falla por permisos, normalmente necesitarás alguno de estos permisos en Microsoft Graph: `PrivilegedAccess.Read.AzureResources`, `PrivilegedAccess.ReadWrite.AzureResources` o permisos equivalentes bajo `PrivilegedAccess`/`identityGovernance`.

Problemas con certificado TLS en `az` (error de CA bundle)
---------------------------------------------------------
Si obtienes un error como "Could not find a suitable TLS CA certificate bundle, invalid path: C:\Users\...\.sareb.es.crt", puede deberse a que alguna variable de entorno que indica la ruta del bundle está apuntando a un fichero que ya no existe.

Para probar rápido en la misma sesión de PowerShell, ejecuta:
```powershell
# ver vars relacionadas
Get-ChildItem Env:REQUESTS_CA_BUNDLE,Env:SSL_CERT_FILE,Env:CURL_CA_BUNDLE

# para desactivar temporalmente en esta sesión
Remove-Item Env:REQUESTS_CA_BUNDLE -ErrorAction SilentlyContinue
Remove-Item Env:SSL_CERT_FILE -ErrorAction SilentlyContinue
Remove-Item Env:CURL_CA_BUNDLE -ErrorAction SilentlyContinue

# luego ejecuta la tarea o el script
powershell -ExecutionPolicy Bypass -File .\scripts\list-pim-eligible.ps1 -UseUser
```

Los scripts incluidos ya intentan detectar y desactivar temporalmente variables de entorno que apunten a ficheros inexistentes, pero si prefieres gestionar el certificado corporativo correctamente, coloca el fichero `.crt` en una ruta estable y exporta la variable `REQUESTS_CA_BUNDLE` con la ruta completa.
