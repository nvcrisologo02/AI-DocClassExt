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

## Resolución de problemas

- **Tras el login, el callback (`/.auth/login/aad/callback`) falla y el payload del
  POST trae `AADSTS700054: response_type 'id_token' is not enabled for the
  application`**: la app registration no tiene habilitada la emisión de ID tokens,
  que EasyAuth necesita (flujo híbrido OpenID Connect). Arreglo (requiere ser owner
  de la app registration o rol equivalente): Entra ID → App registrations → la app →
  Authentication → "Implicit grant and hybrid flows" → marcar **ID tokens** → Save;
  o por CLI: `az ad app update --id <client-id> --enable-id-token-issuance true`.
  Aplica al momento, sin reiniciar el App Service. El error real viaja en el
  *payload* del POST de Entra al callback (visible en las herramientas de
  desarrollador del navegador, pestaña Payload), no en el cuerpo de la respuesta.
- **Visitar `/.auth/login/aad/callback` directamente devuelve 401**: es el
  comportamiento esperado; esa URL es donde Entra devuelve el token, no una página
  para navegar. Las URLs de personas son la raíz de la app o `/.auth/login/aad`.
- **`Cannot use auth v2 commands when the app is using auth v1`** al usar
  `az webapp auth`: ver el paso de upgrade del esquema en la sección de comandos.

## Datos y comandos por entorno

Los identificadores de esta sección no son secretos (el client secret nunca se
documenta: viaja por canal seguro y se almacena en Key Vault).

### DEV (activada y verificada 2026-08-06)

| Elemento | Valor |
|---|---|
| App registration | `DocumentIA Admin - DEV` (single-tenant) |
| Application (client) ID | `32aba075-f88e-4012-9ed4-44655123001d` |
| Directory (tenant) ID | `1a213c5a-2e3d-4ae4-b0ba-075c42f9700e` |
| Enterprise application (Service Principal) ID | `e3ffe62e-41b6-4f32-a971-468c76166eec` |
| Redirect URI | `https://srbwebadmindevdocai.azurewebsites.net/.auth/login/aad/callback` |
| Assignment required | Sí |
| Emisión de ID tokens | **Requerida** (Authentication → "Implicit grant and hybrid flows" → *ID tokens*); sin ella el login falla con `AADSTS700054` |
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

# 3) Si el App Service sigue en el esquema de auth v1 (classic) — es el caso de las
#    apps que nunca han configurado Authentication —, los comandos "az webapp auth"
#    modernos fallan con "Cannot use auth v2 commands when the app is using auth v1".
#    Comprobar y, si procede, hacer el upgrade unico a v2 (irreversible; inocuo
#    cuando no hay auth configurada porque no migra nada):
az webapp auth config-version show --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d \
  --resource-group SRBRGDEVDOCSAI --name srbwebadmindevdocai
az webapp auth config-version upgrade --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d \
  --resource-group SRBRGDEVDOCSAI --name srbwebadmindevdocai

# 4) Configurar el proveedor Microsoft y activar la autenticación
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

### PRO (activada; configuración verificada en el App Service 2026-09-04)

| Elemento | Valor |
|---|---|
| App registration | `DocumentIA Admin - PRO` (single-tenant) |
| Application (client) ID | `c216f80f-8fa4-40cf-b28a-6b7f6a640fce` |
| Directory (tenant) ID | `1a213c5a-2e3d-4ae4-b0ba-075c42f9700e` |
| Enterprise application (Service Principal) ID | `1fe8b941-2941-47f2-af08-b276be1af101` |
| Redirect URI | `https://srbwebadminprodocai.azurewebsites.net/.auth/login/aad/callback` |
| Assignment required | Sí |
| Emisión de ID tokens | Habilitada (verificado) |
| Grupo de acceso asignado | `GSEC-DocumentIA-Admin-PRO` (único principal asignado) |
| Client secret | En Key Vault `srbkvprodocai`, secreto `AdminEasyAuthClientSecret` |
| App Service | `srbwebadminprodocai` (RG `SRBRGDOCSAIPROD`, suscripción Producción Central `647c7246-54bc-4d31-b909-431cacf03272`) |

Activación (misma secuencia que DEV, con los valores de producción):

```bash
# 1) Guardar el client secret en el Key Vault de produccion (valor recibido por canal seguro)
az keyvault secret set --vault-name srbkvprodocai --name AdminEasyAuthClientSecret --value "<CLIENT_SECRET>"

# 2) App setting que EasyAuth lee, como referencia a Key Vault
az webapp config appsettings set --subscription 647c7246-54bc-4d31-b909-431cacf03272   --resource-group SRBRGDOCSAIPROD --name srbwebadminprodocai   --settings "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET=@Microsoft.KeyVault(VaultName=srbkvprodocai;SecretName=AdminEasyAuthClientSecret)"

# 3) Upgrade del esquema de auth a v2 si sigue en v1 (ver seccion de resolucion de problemas)
az webapp auth config-version show --subscription 647c7246-54bc-4d31-b909-431cacf03272   --resource-group SRBRGDOCSAIPROD --name srbwebadminprodocai
az webapp auth config-version upgrade --subscription 647c7246-54bc-4d31-b909-431cacf03272   --resource-group SRBRGDOCSAIPROD --name srbwebadminprodocai

# 4) Configurar el proveedor Microsoft y activar la autenticacion
az webapp auth microsoft update --subscription 647c7246-54bc-4d31-b909-431cacf03272   --resource-group SRBRGDOCSAIPROD --name srbwebadminprodocai   --client-id c216f80f-8fa4-40cf-b28a-6b7f6a640fce   --issuer https://login.microsoftonline.com/1a213c5a-2e3d-4ae4-b0ba-075c42f9700e/v2.0   --client-secret-setting-name MICROSOFT_PROVIDER_AUTHENTICATION_SECRET

az webapp auth update --subscription 647c7246-54bc-4d31-b909-431cacf03272   --resource-group SRBRGDOCSAIPROD --name srbwebadminprodocai   --enabled true --action RedirectToLoginPage

# 5) Reinicio para que el middleware enganche a la primera
az webapp restart --subscription 647c7246-54bc-4d31-b909-431cacf03272   --resource-group SRBRGDOCSAIPROD --name srbwebadminprodocai
```

Consideraciones específicas de producción:

- **Momento de activación**: al activar, cualquier sesión abierta del Admin de
  producción exigirá login; avisar a los usuarios del grupo. El backend de
  clasificación no se ve afectado (autenticación independiente por function key).
- **Al completar el login**, el modo solo lectura se desactiva: las escrituras en
  producción quedan operativas con auditoría de UPN real. Verificar con la
  sección "Verificación" completa (incluido el paso 4).
- **Aviso de entorno**: la franja roja "ENTORNO: PRODUCCIÓN" depende del app
  setting `EnvironmentName=Production` en el Function App de producción, que
  aplica el pipeline (encolado desde `develop`) o puede fijarse a mano; es
  independiente de la autenticación.
- **Caducidad del client secret**: registrar la fecha igual que en DEV.

### PRE (activada 2026-09-04)

| Elemento | Valor |
|---|---|
| App registration | `DocumentIA Admin - PRE` (single-tenant) |
| Application (client) ID | `5dbd017b-03a0-44cc-9376-f60e217aecfa` |
| Directory (tenant) ID | `1a213c5a-2e3d-4ae4-b0ba-075c42f9700e` |
| Enterprise application (Service Principal) ID | `70166628-91e1-4fcb-a43d-1d16c7133507` |
| Redirect URI | `https://srbwebadminpredocai.azurewebsites.net/.auth/login/aad/callback` |
| Assignment required | Sí |
| Emisión de ID tokens | Habilitada (verificado) |
| Grupo de acceso asignado | `GSEC-DocumentIA-Admin-PRE` |
| Client secret | En Key Vault `srbkvpredocai`, secreto `AdminEasyAuthClientSecret` |
| App Service | `srbwebadminpredocai` (RG `SRBRGPREDOCSAI`, suscripción Core Preproduccion `a4f6b357-8f13-4488-9ee8-b9f635426f91`) |

Activación (misma secuencia que DEV/PRO, con los valores de preproducción):

```bash
# 1) Guardar el client secret en el Key Vault de pre (valor recibido por canal seguro)
az keyvault secret set --vault-name srbkvpredocai --name AdminEasyAuthClientSecret --value "<CLIENT_SECRET>"

# 2) App setting que EasyAuth lee, como referencia a Key Vault
az webapp config appsettings set --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91   --resource-group SRBRGPREDOCSAI --name srbwebadminpredocai   --settings "MICROSOFT_PROVIDER_AUTHENTICATION_SECRET=@Microsoft.KeyVault(VaultName=srbkvpredocai;SecretName=AdminEasyAuthClientSecret)"

# 3) Upgrade del esquema de auth a v2 si sigue en v1
az webapp auth config-version upgrade --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91   --resource-group SRBRGPREDOCSAI --name srbwebadminpredocai

# 4) Configurar el proveedor Microsoft y activar la autenticacion
az webapp auth microsoft update --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91   --resource-group SRBRGPREDOCSAI --name srbwebadminpredocai   --client-id 5dbd017b-03a0-44cc-9376-f60e217aecfa   --issuer https://login.microsoftonline.com/1a213c5a-2e3d-4ae4-b0ba-075c42f9700e/v2.0   --client-secret-setting-name MICROSOFT_PROVIDER_AUTHENTICATION_SECRET

az webapp auth update --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91   --resource-group SRBRGPREDOCSAI --name srbwebadminpredocai   --enabled true --action RedirectToLoginPage

# 5) Reinicio para que el middleware enganche a la primera
az webapp restart --subscription a4f6b357-8f13-4488-9ee8-b9f635426f91   --resource-group SRBRGPREDOCSAI --name srbwebadminpredocai
```

Resultado de la activación (2026-09-04):

- El App Service estaba en esquema de auth **v1**; se hizo el upgrade a v2 (paso 3).
- La referencia a Key Vault de `MICROSOFT_PROVIDER_AUTHENTICATION_SECRET` resuelve
  correctamente (`configreferences/appsettings` → `status=Resolved`); la identidad
  administrada del Admin ya tenía `Key Vault Secrets User` sobre `srbkvpredocai`.
- `authsettingsV2` de PRE queda idéntica a la de PRO salvo el `clientId`.
- Comprobación de comportamiento, igual en los tres entornos: petición sin cabeceras
  de navegador → `401`; petición con `Accept: text/html` → `302` a
  `login.microsoftonline.com`. El `401` de una llamada plana (curl, sondas) es el
  comportamiento normal de EasyAuth, no un fallo de configuración.
- Login real verificado el 2026-09-04: el acceso al Admin de PRE redirige al login
  corporativo y la aplicación queda operativa tras autenticarse.

Nota: en la enterprise application de PRE hay, además del grupo, un usuario
asignado de forma individual (mismo patrón que se observó en DEV). Si no es
deliberado, retirarlo para que todo el acceso se gobierne por el grupo.
Comprobado el 2026-09-04: siguen asignados `GSEC-DocumentIA-Admin-PRE` y ese
usuario individual, con *assignment required* activo.
