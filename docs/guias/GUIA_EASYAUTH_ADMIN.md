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

## Notas

- La autenticación del Admin es independiente de la del backend Functions, que
  sigue protegido por function key (`x-functions-key`) como hasta ahora.
- Si el App Service tiene slots, la configuración de Authentication se aplica por slot.
- Para restringir el acceso a un grupo concreto de usuarios (no todo el tenant),
  en la app registration: **Enterprise application > Properties > Assignment required = Yes**
  y asignar usuarios/grupos en **Users and groups**.
