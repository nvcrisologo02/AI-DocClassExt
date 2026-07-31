# Solicitud de app registration para autenticar el Admin (DocumentIA)

> Objetivo de este documento: servir de base para el ticket/solicitud al
> equipo de identidad y seguridad, para que se cree (o se autorice crear) la
> app registration necesaria para activar App Service Authentication
> (EasyAuth) en el Admin de DocumentIA. Ver el procedimiento de configuración
> en `docs/guias/GUIA_EASYAUTH_ADMIN.md`.

## Qué se pide

Texto para el ticket:

> Se solicita la creación de una app registration en Entra ID para habilitar
> el inicio de sesión corporativo (App Service Authentication / EasyAuth) en
> una aplicación web interna de administración (DocumentIA Admin).
>
> - **Tipo**: app registration *single-tenant* (solo cuentas de esta
>   organización).
> - **Finalidad**: inicio de sesión de usuarios corporativos en una
>   aplicación web interna de administración. No se trata de una aplicación
>   pública ni de cara a terceros.
> - **Permisos de Microsoft Graph requeridos**: ninguno más allá del inicio de
>   sesión básico (el permiso delegado `User.Read`, si el proceso de creación
>   lo añade por defecto). No se solicitan permisos de aplicación ni acceso a
>   datos de Graph, Exchange, SharePoint ni ningún otro recurso.
> - **Redirect URIs (tipo Web)**, uno por entorno:
>   - `https://srbwebadmindevdocai.azurewebsites.net/.auth/login/aad/callback`
>   - `https://srbwebadminpredocai.azurewebsites.net/.auth/login/aad/callback`
>   - `https://srbwebadminprodocai.azurewebsites.net/.auth/login/aad/callback`
>
>   Se puede optar por una app registration por entorno (una para dev, otra
>   para pre, otra para prod) o por una única app registration con los tres
>   redirect URIs anteriores. Lo habitual, y más limpio operativamente
>   (rotación de secretos y ciclo de vida independientes por entorno), es
>   crear **una app registration por entorno**.
> - **Qué se necesita recibir de vuelta**, por cada app registration creada:
>   - Application (client) ID.
>   - Directory (tenant) ID.
>   - Un client secret (o, si el equipo de identidad lo prefiere, que sea el
>     propio equipo quien configure la autenticación directamente en el App
>     Service correspondiente, sin necesidad de compartir el secret).
> - **Restricción de acceso recomendada**: activar **"Assignment required" =
>   Yes** en la enterprise application asociada, y asignar únicamente el
>   grupo de administradores de DocumentIA (no todo el tenant). Así, aunque
>   cualquier usuario corporativo pudiera en teoría autenticarse, solo el
>   grupo asignado podrá iniciar sesión en la aplicación.

## Alternativa que suele ser más sencilla de conceder

Si crear la app registration por parte del equipo de identidad supone
tiempos de espera largos, una alternativa habitual es solicitar que se asigne
al solicitante el rol de Entra ID **"Application Developer"**. Este rol
permite crear app registrations propias (y gestionar el consentimiento de
aplicaciones que uno mismo ha creado) sin otorgar privilegios adicionales
sobre el directorio: no da acceso a gestionar usuarios, grupos, ni otras
aplicaciones del tenant. Es, por tanto, un rol acotado que suele ser más
rápido de conceder que delegar la creación en el equipo de identidad para
cada app puntual.

## Qué ya está preparado en la aplicación

El código del Admin ya está preparado para EasyAuth: lee la cabecera
`X-MS-CLIENT-PRINCIPAL-NAME` que App Service Authentication inyecta en las
peticiones autenticadas y la usa como usuario de auditoría en tipologías,
plugins y prompts (ver `docs/guias/GUIA_EASYAUTH_ADMIN.md`). No se requiere
ningún cambio de código para activar la autenticación: basta con crear la app
registration y configurar Authentication en el App Service de cada entorno.

## Mientras tanto

Mientras se resuelve la solicitud de la app registration:

- Se puede aplicar una restricción de acceso de red al Admin como medida
  transitoria de reducción de exposición (no sustituye a la autenticación).
  Ver `docs/guias/GUIA_RESTRICCION_ACCESO_ADMIN.md`.
- El propio Admin ya deshabilita las operaciones de escritura cuando no hay
  un usuario autenticado, quedando en modo de solo lectura hasta que EasyAuth
  esté activo.
