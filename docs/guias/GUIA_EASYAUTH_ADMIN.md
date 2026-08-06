# Guía: activar App Service Authentication (EasyAuth) para DocumentIA.Admin

> Configuración de infraestructura: la ejecuta el propietario de la suscripción.
> El código del Admin ya está preparado: lee la cabecera `X-MS-CLIENT-PRINCIPAL-NAME`
> que EasyAuth inyecta y la usa como usuario de auditoría. Sin EasyAuth, en
> producción la auditoría registrará "no-autenticado"; en desarrollo local,
> `dev-<usuario del sistema>`.

## Pasos en Azure Portal (App Service del Admin)

1. App Service del Admin → **Settings > Authentication** → **Add identity provider**.
2. Provider: **Microsoft** (Entra ID).
   - App registration type: *Create new app registration* (o seleccionar una existente si Seguridad la proporciona).
   - Supported account types: *Current tenant — Single tenant*.
3. **Restrict access**: *Require authentication*.
4. **Unauthenticated requests**: *HTTP 302 Found redirect: recommended for websites*.
5. Token store: puede dejarse activado (por defecto). No se requiere configuración adicional de claims.
6. Guardar. Desde ese momento toda petición exige login corporativo y la app
   registra el UPN/email del usuario en la auditoría de tipologías, plugins y prompts.

## Verificación

1. Abrir la URL del Admin en una ventana privada → debe redirigir al login corporativo.
2. Publicar un cambio menor (p. ej. pasar una tipología a Draft y volver a publicar).
3. En el detalle de la tipología, sección "Auditoría de cambios", la columna
   Usuario debe mostrar el UPN del usuario en lugar de "ADMIN-UI".
4. Con la sesión ya abierta y el circuito Blazor establecido, esperar varios
   minutos sin recargar la página (para que la conexión SignalR haya quedado
   activa tras el render inicial) y entonces publicar/guardar de nuevo. La
   auditoría debe seguir mostrando el UPN, no "no-autenticado". Si aparece
   "no-autenticado" con EasyAuth activo, reportarlo como incidencia: indicaría
   una limitación del transporte SignalR para propagar cabeceras HTTP al
   circuito ya establecido.

## Notas

- La autenticación del Admin es independiente de la del backend Functions, que
  sigue protegido por function key (`x-functions-key`) como hasta ahora.
- Si el App Service tiene slots, la configuración de Authentication se aplica por slot.
- Para restringir el acceso a un grupo concreto de usuarios (no todo el tenant),
  en la app registration: **Enterprise application > Properties > Assignment required = Yes**
  y asignar usuarios/grupos en **Users and groups**.

## Datos y comandos por entorno

Los identificadores de esta sección no son secretos (el client secret nunca se
documenta: viaja por canal seguro y se almacena en Key Vault).

### DEV (verificado 2026-08-06)

| Elemento | Valor |
|---|---|
| App registration | `DocumentIA Admin - DEV` (single-tenant) |
| Application (client) ID | `32aba075-f88e-4012-9ed4-44655123001d` |
| Directory (tenant) ID | `1a213c5a-2e3d-4ae4-b0ba-075c42f9700e` |
| Enterprise application (Service Principal) ID | `e3ffe62e-41b6-4f32-a971-468c76166eec` |
| Redirect URI | `https://srbwebadmindevdocai.azurewebsites.net/.auth/login/aad/callback` |
| Assignment required | Sí |
| Grupo de acceso asignado | `GSEC-DocumentIA-Admin-DEV` |
| Client secret | En Key Vault `srbkvdevdocai`, secreto `AdminEasyAuthClientSecret` |
| App Service | `srbwebadmindevdocai` (RG `SRBRGDEVDOCSAI`, suscripción Core Desarrollo `8764f9ff-fe37-4c03-bde9-6294622bef6d`) |

Activación por línea de comandos (equivalente a los pasos del portal; el primer
comando de `auth` puede pedir instalar la extensión `authV2` de Azure CLI):

```bash
# 1) Guardar el client secret en el Key Vault de dev (valor recibido por canal seguro)
az keyvault secret set --vault-name srbkvdevdocai --name AdminEasyAuthClientSecret --value "<CLIENT_SECRET>"

# 2) App setting que EasyAuth lee, como referencia a Key Vault (el Admin ya tiene
#    el rol "Key Vault Secrets User"; el valor no queda en claro en la configuración)
az webapp config appsettings set --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d \
  --resource-group SRBRGDEVDOCSAI --name srbwebadmindevdocai \
  --settings "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET=@Microsoft.KeyVault(VaultName=srbkvdevdocai;SecretName=AdminEasyAuthClientSecret)"

# 3) Configurar el proveedor Microsoft y activar la autenticación
az webapp auth microsoft update --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d \
  --resource-group SRBRGDEVDOCSAI --name srbwebadmindevdocai \
  --client-id 32aba075-f88e-4012-9ed4-44655123001d \
  --issuer https://login.microsoftonline.com/1a213c5a-2e3d-4ae4-b0ba-075c42f9700e/v2.0 \
  --client-secret-setting-name MICROSOFT_PROVIDER_AUTHENTICATION_SECRET

az webapp auth update --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d \
  --resource-group SRBRGDEVDOCSAI --name srbwebadmindevdocai \
  --enabled true --action RedirectToLoginPage
```

Tras activar, ejecutar la sección "Verificación" completa (incluido el paso 4).
Con la identidad activa, el modo solo lectura del Admin se desactiva por sí solo
y el aviso de entorno deja de mostrar "· solo lectura (sin usuario autenticado)".

Recordatorios operativos de DEV:

- **Caducidad del client secret**: consultar la fecha en la app registration y
  registrarla donde el equipo revise vencimientos; su expiración deja el login
  del Admin fuera de servicio hasta rotarlo (rotación: nuevo secret en la app
  registration → actualizar el secreto `AdminEasyAuthClientSecret` en el Key
  Vault; no requiere tocar el App Service).
- En la enterprise application hay, además del grupo, un usuario asignado de
  forma individual (alta previa a la creación del grupo). Si no es deliberado,
  retirarlo para que todo el acceso se gobierne por el grupo.

### PRE y PROD (pendientes)

Sin app registration todavía. Al solicitarlas (ver
[SOLICITUD_APP_REGISTRATION_ADMIN.md](SOLICITUD_APP_REGISTRATION_ADMIN.md)),
replicar este mismo esquema por entorno: app registration single-tenant con el
redirect URI del entorno, grupo de acceso propio (en producción, diferenciado),
"Assignment required = Yes", secret en el Key Vault del entorno
(`srbkvpredocai` / `srbkvprodocai`) y los mismos comandos sustituyendo
suscripción, resource group, App Service e identificadores.
