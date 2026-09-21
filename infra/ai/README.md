# infra/ai — definición versionada de IA por entorno

Definición versionada de los recursos de Inteligencia Artificial (Azure OpenAI,
Content Understanding y Document Intelligence) que usa DocumentIA en cada
entorno, en el marco del plan "IA propia por entorno" (AB#100298). Esta
carpeta es la fuente de verdad de qué recurso, deployment, analyzer o
clasificador debe existir en cada entorno; no crea ni modifica nada por sí
sola, la aplican las tareas de promoción (ver más abajo).

## Regla de alcance

**Solo entran en `infra/ai/` los artefactos referenciados por filas activas de
`ModeloConfigs`** (`Activo = 1`) en cada entorno: los deployments, analyzers y
clasificadores que el sistema usa realmente en producción. El resto del
Foundry de PRO (analyzers de pruebas, versiones descartadas, experimentos)
no se versiona aquí — queda listado en `inventory-prod-foundry.json` como
inventario de referencia para una limpieza futura del recurso origen.

## Ficheros

- **`resources.<env>.json`** (`dev` / `pre` / `prod`): inventario de cuentas
  de Azure AI por entorno — `openai_primary`, `cu_primary`, `cu_secondary` y
  `di` — con su grupo de recursos y endpoint. Es el mapa que resuelve los
  alias de recurso (`ResourceAlias`) introducidos en las Tareas 1-4 del plan.

- **`deployments.<env>.json`**: los deployments de modelo (nombre de
  deployment, modelo base, versión, SKU y capacidad) que debe tener cada
  cuenta OpenAI del entorno. `deployments.dev.json` y `deployments.pre.json`
  describen el **estado deseado** (incluyen `gpt-5-mini`, que a día de hoy
  solo existe en PRO); son las Tareas 1 y 2 del plan las que lo crean en DEV y
  PRE. `deployments.prod.json` refleja los deployments reales verificados por
  ARM (`az cognitiveservices account deployment list`) el 2026-09-21, e
  incluye `gpt-4o` y un segundo `gpt-5-mini` en la cuenta secundaria
  (`srbaisrv-westeurope`) que no estaban en la hipótesis inicial del plan.

- **`di-artifacts.json`**: clasificadores y modelos personalizados de
  Document Intelligence en el recurso origen (`srbdiprodocai`, PRO) que hay
  que copiar a DEV y PRE, con el nombre de configuración que los referencia.
  Resuelto en la Tarea 8 (AB#100310): el id real del modelo es `DI_NS1.4_v0`
  (`DI_NS_1.4_v1` no existe en el recurso origen). La fila de `ModeloConfigs`
  que aún referencia el id inexistente (`nota.simple.1_4.azure-di`, activa en
  DEV y en PRO) queda anotada como pendiente de corregir por datos; no se ha
  tocado en base de datos desde aquí. Verificado además que esa fila está hoy
  huérfana: ninguna tipología activa usa esa clave para su extracción (la
  tipología `nota.simple.1_4` extrae con Content Understanding, no con DI).

- **`datasets/<clasificador>@<version>.manifest.json`**: dataset de
  entrenamiento/referencia (contenedor, prefijo, fecha de corte y ficheros)
  usado por Document Intelligence Studio o Content Understanding Studio para
  un analyzer/clasificador. `DocumentAICC_v1@1.manifest.json` quedó con
  `"status": "pendiente de acceso al storage"` (Tarea 8, Step 3): el
  contenedor `documentai` de `srbstgproapppdocai` existe y no tiene
  restricciones de red, pero mi identidad no tiene el rol de datos
  (Storage Blob Data Reader) necesario para listar su contenido y localizar
  el prefijo exacto del proyecto del clasificador.

- **`analyzers/*.json`**: definición de cada analyzer de Content
  Understanding referenciado por `ModeloConfigs`, exportada desde el recurso
  origen de PRO sin los campos de solo lectura (`status`, `createdAt`,
  `lastModifiedAt`, `warnings`, `supportedModels`). Son la entrada para
  reconstruir el analyzer en DEV y PRE (Content Understanding no ofrece copia
  entre recursos; hay que recrear la definición). Generados por
  `scripts/ai/export-analyzer-definitions.ps1`.

- **`inventory-prod-foundry.json`**: listado completo de analyzers custom del
  recurso Foundry de PRO (excluye los `prebuilt-*`), con un flag `referenced`
  que marca cuáles de ellos están también en `analyzers/*.json`. Sirve para
  identificar en el futuro qué analyzers del Foundry de PRO ya no se usan y
  se pueden retirar.

## Flujo de promoción (Tareas 10 a 14 — aún no existen)

El orden previsto para aplicar esta definición a DEV y PRE es:

1. **Datasets** — preparar los datos de entrenamiento/referencia que
   necesiten los analyzers y clasificadores antes de recrearlos.
2. **Deployments** — crear en cada cuenta OpenAI de DEV/PRE los deployments
   de `deployments.<env>.json` (asegura que el modelo/versión/SKU exista
   antes de que algo dependa de él).
3. **Analyzers de Content Understanding, por reconstrucción** — crear cada
   analyzer de `analyzers/*.json` en el recurso Foundry del entorno destino a
   partir de la definición exportada (no hay copia directa entre recursos).
4. **Clasificadores de Document Intelligence, por Copy API** — copiar los
   artefactos de `di-artifacts.json` desde el recurso origen al recurso
   destino usando la Copy API de Document Intelligence (esta sí soporta copia
   entre recursos).
5. **Validación** — comprobar que DEV/PRE clasifican con los recursos propios
   y no con los de PRO, y que los resultados son equivalentes.

Estos pasos corresponden a las Tareas 10-14 del plan y todavía no están
implementados; esta carpeta es su entrada de datos.

## Estado inicial de DEV y PRE

**Content Understanding (Tarea 8, Step 1, AB#100310) -- pendiente de plataforma.**
`GET /contentunderstanding/analyzers?api-version=2025-11-01` contra
`srbaisrv01devdocai` y `srbaisrv01predocai` devuelve **401** con cuerpo
`{"error":{"code":"PermissionDenied","message":"Principal does not have access to
API/Operation."}}` para el usuario del proyecto (`ignacio.varas@sareb.es`) en ambos
recursos. Verificado con `az role definition list --name "Cognitive Services User"
--query "[].permissions[].dataActions"`, que devuelve `["Microsoft.CognitiveServices/*"]`
-- ese comodin cubre la data action de lectura de analyzers, por lo que el rol
**Cognitive Services User** es suficiente. Peticion a plataforma: conceder el rol
**Cognitive Services User** al usuario del proyecto sobre `srbaisrv01devdocai`
(`SRBRGDEVDOCSAI`) y `srbaisrv01predocai` (`SRBRGPREDOCSAI`), o como alternativa
entregar el listado `GET /contentunderstanding/analyzers` de ambas cuentas.

## Roles cruzados sobre PRO (Tarea 8, Step 4, AB#100310)

Las identidades administradas de las Functions de DEV (`srbappdevdocai`,
`71380c7e-fbb5-4333-8b35-0126e9e87720`) y PRE (`srbapppredocai`,
`0383f8d8-7f82-46b0-a3d4-28ad9ae472ca`) tienen, cada una, **3 asignaciones de rol
Cognitive Services User** sobre recursos de `SRBRGDOCSAIPROD` (verificado con la REST
API de ARM `roleAssignments?$filter=assignedTo(...)`, a nivel de resource group y
tambien a nivel de suscripcion completa para descartar asignaciones fuera de ese RG):

- `srbdiprodocai` (recurso de Document Intelligence origen)
- `upe48-mm2avmdm-swedencentral` (recurso Foundry primario de PRO)
- `srbaisrv-westeurope` (recurso Foundry secundario de PRO)

Son 6 asignaciones en total (3 por identidad), todas con el mismo rol. Detalle completo
en `docs/auxiliares/temps/2026-09-21/roles-cross-env.json` (gitignored). Es la lista
que la Tarea de la fase 3 debe retirar una vez que DEV y PRE tengan sus propios
recursos de IA y dejen de apuntar a los de PRO. Nota tecnica: `az role assignment
list --scope ... --assignee ...` devolvio `(MissingSubscription)` en este entorno con
el proxy TLS activo; se resolvio llamando directamente a la REST API de ARM
(`az rest`) con el filtro `assignedTo('<principalId>')`, que si funciono.

## Spike Copy API de Content Understanding (Tarea 7, AB#100309)

Se probo el Step 1 (autorizacion) del flujo `grantCopyAuthorization` + `:copy` entre
`upe48-mm2avmdm-swedencentral` (origen) y `srbaisrv-westeurope` (destino, ambos en
PRO). Resultado: **200 OK**, pero la respuesta real de `api-version=2025-11-01` no
incluye un token portable como describe la guia "disaster recovery" -- solo
`targetAzureResourceId`, `targetRegion` y `expiresAt` (24h). La documentacion oficial
vigente exige el rol **Cognitive Services User** en origen y destino para la MISMA
identidad que ejecuta el `:copy`, no un secreto transportable a una identidad
distinta. Veredicto provisional: la copia parece viable si la promocion la ejecuta
una identidad con ese rol concedido temporalmente en ambos recursos, pero no se ha
confirmado el `:copy` real (no ejecutado, por decision expresa de no crear recursos
en este spike). Detalle completo, comandos y respuestas en
`docs/auxiliares/temps/2026-09-21/spike-cu-copy-token.md` (gitignored) y el script
listo para lanzar en `docs/auxiliares/temps/2026-09-21/spike-cu-copy-token.ps1`.

## Regenerar los analyzers

```powershell
pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.4_3,CU_NS_1.5_0,CU_NS_1.6_0_GGAA,CERA16_v1,CERA44_vado,CERA46
```

El script hace únicamente operaciones de lectura (GET) contra el recurso
Foundry origen; no crea, modifica ni borra ningún analyzer. Añadir un id a la
lista y volver a ejecutar actualiza `analyzers/<id>.json` y regenera
`inventory-prod-foundry.json` completo.

Nota de implementación: el script obtiene el cuerpo de cada analyzer con
`Invoke-WebRequest` y un token de `az account get-access-token`, decodificando
la respuesta explícitamente como UTF-8, en vez de leer la salida de texto de
`az rest`. Se verificó que `az rest` recodifica su salida con la página de
códigos activa de la consola y sustituye caracteres no representables (tildes,
eñes) por el carácter de sustitución U+FFFD, perdiendo contenido real de las
definiciones (por ejemplo, "Dirección" llegaba corrupto). Leer los bytes de la
respuesta directamente evita esa pérdida.
