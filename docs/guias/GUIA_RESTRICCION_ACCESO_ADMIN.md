# Guía: restringir el acceso de red al Admin (DocumentIA)

> Medida transitoria mientras no exista autenticación (EasyAuth) en el Admin.
> Restringir el acceso de red **no da identidad por usuario**: la auditoría de
> tipologías, plugins y prompts seguirá registrando "no-autenticado" (o
> "ADMIN-UI", según el punto en que se encuentre el desarrollo). Esto es una
> medida para reducir la exposición del panel, complementaria a la
> autenticación descrita en `docs/guias/GUIA_EASYAUTH_ADMIN.md`, y no la
> sustituye.

## Estado de partida

Comprobado el 2026-07-31 en dev y prod (pre no comprobado; verificar antes de
aplicar cualquier cambio).

| Entorno | App Service | Resource group | Suscripción | Host |
|---|---|---|---|---|
| dev | `srbwebadmindevdocai` | `SRBRGDEVDOCSAI` | `8764f9ff-fe37-4c03-bde9-6294622bef6d` (Core Desarrollo) | `srbwebadmindevdocai.azurewebsites.net` |
| pre | `srbwebadminpredocai` | `SRBRGPREDOCSAI` | `a4f6b357-8f13-4488-9ee8-b9f635426f91` (Core Preproduccion) | `srbwebadminpredocai.azurewebsites.net` |
| prod | `srbwebadminprodocai` | `SRBRGDOCSAIPROD` | `647c7246-54bc-4d31-b909-431cacf03272` (Producción Central) | `srbwebadminprodocai.azurewebsites.net` |

Estado actual de cada App Service (dev y prod comprobados; pre pendiente de
comprobar):

- Autenticación de App Service (EasyAuth): deshabilitada (`enabled: false`).
- Restricciones de acceso: una única regla "Allow all" con acción Allow, y
  acción por defecto Allow. Es decir, hoy no hay ninguna restricción de red
  efectiva.
- `publicNetworkAccess`: `Enabled`.
- Sin private endpoints configurados.
- El Admin de prod tiene integración VNet de **salida** (subred
  `ServiciosProduccionDocumentAI` de `RedServiciosProduccion`). Esta
  integración sirve para que el Admin alcance recursos internos (por ejemplo,
  la base de datos), pero **no limita el acceso entrante** al propio Admin.
- El backend (Azure Functions) está protegido por function key
  (`x-functions-key`); las restricciones de acceso del Admin que se describen
  aquí no afectan al backend.

## Requisitos previos

- Rangos de IP públicos de salida corporativos / VPN, proporcionados por el
  equipo de redes: `[completar: rangos IP corporativos]`.
- Permisos de `Website Contributor` (o superiores) sobre el App Service a
  modificar, y sesión iniciada con `az login` en la suscripción del entorno
  correspondiente.

## Pasos

1. Seleccionar la suscripción del entorno:

   ```bash
   az account set --subscription "8764f9ff-fe37-4c03-bde9-6294622bef6d"  # dev
   ```

2. Añadir una regla Allow por cada rango de IP corporativo que deba tener
   acceso, sobre el sitio principal (no el `scm`, ver aviso más abajo):

   ```bash
   az webapp config access-restriction add \
     --resource-group SRBRGDEVDOCSAI \
     --name srbwebadmindevdocai \
     --rule-name "Red-Corporativa-1" \
     --action Allow \
     --ip-address "[completar: rango IP corporativo]/32" \
     --priority 100
   ```

   Repetir con `--priority` creciente (200, 300, ...) por cada rango adicional
   (por ejemplo, distintas sedes o rangos de VPN).

3. **Importante**: en el momento en que se añade la primera regla Allow, Azure
   cambia automáticamente la acción por defecto de `Allow` a `Deny`. Esto no
   es una regla que se configure aparte: es un efecto colateral de añadir
   cualquier regla explícita. En la práctica significa que, tras el paso
   anterior, todo el tráfico que no coincida con ninguna regla Allow queda
   bloqueado con un `403` a nivel de plataforma (antes de llegar a la
   aplicación). Conviene comprobarlo explícitamente (ver "Verificación") para
   no dar por hecho que sigue habiendo acceso general.

## Aviso operativo importante: el sitio `scm` (Kudu)

El sitio de despliegue `scm` (Kudu, usado por Azure DevOps para publicar) tiene
su **propia lista de reglas de acceso, independiente** de la del sitio
principal. Restringir el sitio principal con los pasos anteriores **no
restringe** el `scm`: el pipeline de despliegue seguirá funcionando con
normalidad salvo que se restrinja también el `scm` de forma explícita.

Si se decide restringir también el `scm`, hay tres opciones:

- **(a) Dejar `scm` sin restringir.** Es la opción más simple y, en la
  práctica, razonable: el sitio `scm` exige credenciales de publicación
  (publish profile / Entra) en cualquier caso, por lo que dejarlo abierto a
  nivel de red no equivale a dejarlo abierto a cualquiera sin autenticación.
- **(b) Restringir `scm` permitiendo el service tag `AzureCloud`**, usando
  `--scm-site true`, si el despliegue se ejecuta desde agentes hospedados de
  Microsoft (Microsoft-hosted agents):

  ```bash
  az webapp config access-restriction add \
    --resource-group SRBRGDEVDOCSAI \
    --name srbwebadmindevdocai \
    --rule-name "Agentes-Azure-DevOps" \
    --action Allow \
    --service-tag AzureCloud \
    --priority 100 \
    --scm-site true
  ```

- **(c) Usar agentes self-hosted** ubicados en la red corporativa, y
  restringir el `scm` por rango de IP igual que el sitio principal.

**Advertencia**: restringir el `scm` por rango de IP concreto **no es viable**
si el pipeline usa agentes hospedados de Microsoft, porque sus direcciones IP
de salida cambian y no están garantizadas dentro de un rango estable; en ese
caso hay que usar el service tag `AzureCloud` (opción b) o migrar a agentes
self-hosted (opción c).

## Verificación

1. Comprobar las reglas efectivas en el sitio principal y en el `scm`:

   ```bash
   az webapp config access-restriction show \
     --resource-group SRBRGDEVDOCSAI \
     --name srbwebadmindevdocai
   ```

   Confirmar que aparecen las reglas Allow esperadas y que la acción por
   defecto (`defaultAction`) es `Deny`.

2. Prueba funcional desde dentro de la red/VPN corporativa: abrir el host del
   entorno (por ejemplo, `https://srbwebadmindevdocai.azurewebsites.net`) y
   confirmar que el Admin carga con normalidad.

3. Prueba funcional desde fuera (por ejemplo, con datos móviles o una red
   ajena a la VPN): confirmar que la petición se rechaza con `403`.

4. Prueba de despliegue: lanzar el pipeline de Azure DevOps y confirmar que la
   publicación se completa sin errores de red hacia el `scm`.

## Reversión

Para volver al estado de partida ("Allow all", sin restricciones):

```bash
az webapp config access-restriction remove \
  --resource-group SRBRGDEVDOCSAI \
  --name srbwebadmindevdocai \
  --rule-name "Red-Corporativa-1"
```

Repetir para cada regla añadida (incluida la del `scm`, si se creó). Al
eliminar todas las reglas explícitas, la acción por defecto vuelve a `Allow`.

## Orden recomendado

Aplicar primero en **dev**: añadir las reglas, comprobar el despliegue desde
Azure DevOps y el acceso desde dentro/fuera de la red. Solo tras confirmar que
todo funciona como se espera, repetir el mismo procedimiento en **pre** y,
finalmente, en **prod**.
