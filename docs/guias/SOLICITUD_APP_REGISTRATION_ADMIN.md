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
> - **Emisión de ID tokens (imprescindible)**: en Authentication → "Implicit
>   grant and hybrid flows", marcar **"ID tokens (used for implicit and hybrid
>   flows)"**. App Service Authentication lo requiere; sin esta opción el inicio
>   de sesión falla con el error `AADSTS700054`.
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
> - **Restricción de acceso requerida**: activar **"Assignment required" =
>   Yes** en la enterprise application asociada, y asignar únicamente el
>   grupo de administradores de DocumentIA descrito en el punto siguiente (no
>   todo el tenant). Así, aunque cualquier usuario corporativo pudiera en
>   teoría autenticarse, solo los miembros de ese grupo podrán iniciar sesión
>   en la aplicación.
> - **Creación del grupo de acceso**: se solicita además la creación de un
>   **grupo de seguridad** en Entra ID para gobernar quién puede entrar al
>   Admin, y que se den de alta en él los usuarios del listado que se adjunta
>   en este mismo ticket (ver "Listado de usuarios"). Datos propuestos:
>   - Nombre propuesto: `[completar: nombre segun la convencion de nomenclatura
>     corporativa, p. ej. SEC-DocumentIA-Admins]`.
>   - Tipo: grupo de **seguridad** (no Microsoft 365), con miembros asignados
>     de forma directa (no dinámico).
>   - Uso: asignación a la enterprise application del Admin de DocumentIA.
>   - Miembros iniciales: los indicados en el listado adjunto.
>   - Gestión posterior de altas y bajas: se solicita indicar el procedimiento
>     para añadir o retirar miembros más adelante. Si es posible, designar como
>     **propietario del grupo** al responsable funcional de DocumentIA, de modo
>     que las altas y bajas ordinarias no requieran un ticket nuevo.
>   - Si se opta por una app registration por entorno, indicar si se desea un
>     único grupo para los tres entornos o un grupo por entorno. Lo habitual es
>     un único grupo para dev y pre, y un grupo diferenciado para producción.

### Listado de usuarios

Adjuntar al ticket la relación de personas que deben tener acceso:

| Nombre y apellidos | Usuario corporativo (UPN) | Entornos | Observaciones |
|---|---|---|---|
| [completar] | [completar] | dev / pre / prod | |

> Nota para quien tramite la solicitud: la asignación de **grupos** a una
> enterprise application requiere licencia Microsoft Entra ID P1 o superior.
> Si el tenant no dispusiera de ella, la alternativa es asignar los usuarios
> del listado **individualmente** a la aplicación; en ese caso conviene
> confirmar en el mismo ticket cuál de las dos vías se aplicará, para saber
> cómo se gestionarán después las altas y bajas.

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

## Resumen de lo que se solicita

Para que no se pierda ningún punto al abrir el ticket:

1. App registration single-tenant (una por entorno, o una con los tres redirect URIs).
2. Los redirect URIs indicados y la emisión de ID tokens habilitada, sin permisos de Graph más allá del inicio de sesión.
3. Creación del grupo de seguridad de acceso al Admin.
4. Alta en ese grupo de los usuarios del listado adjunto.
5. Asignación del grupo a la enterprise application con "Assignment required = Yes".
6. Devolución de Application (client) ID y Directory (tenant) ID, y del client secret
   (o configuración directa de Authentication en el App Service por parte del equipo).
7. Procedimiento y propietario para gestionar altas y bajas del grupo en el futuro.

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
