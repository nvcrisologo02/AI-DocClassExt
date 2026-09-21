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
clasificadores que el sistema usa realmente en producción. El resto de las
dos cuentas Foundry de PRO (analyzers de pruebas, versiones descartadas,
experimentos) no se versiona aquí — queda listado en
`inventory-prod-foundry.json` (cuenta primaria) e
`inventory-prod-foundry-westeurope.json` (cuenta secundaria) como inventario
de referencia para una limpieza futura de los recursos origen.

## Ficheros

- **`resources.<env>.json`** (`dev` / `pre` / `prod`): inventario de cuentas
  de Azure AI por entorno — `openai_primary`, `cu_primary`, `cu_secondary` y
  `di` — con su grupo de recursos y endpoint. Es el mapa que resuelve los
  alias de recurso (`ResourceAlias`) introducidos en las Tareas 1-4 del plan.

- **`deployments.<env>.json`**: los deployments de modelo (nombre de
  deployment, modelo base, versión, SKU y capacidad) que debe tener cada
  cuenta OpenAI del entorno. La clave `intent` distingue las dos naturalezas
  posibles del fichero:
  - `"intent": "desired"` (`deployments.dev.json`, `deployments.pre.json`):
    **estado deseado**, no observado — incluyen `gpt-5-mini`, que a día de hoy
    solo existe en PRO; son las Tareas 1 y 2 del plan las que lo crean en DEV
    y PRE.
  - `"intent": "observed"` (`deployments.prod.json`): estado **real**,
    verificado por ARM (`source: "arm"`, `observedAtUtc` con la fecha exacta
    del listado — `az cognitiveservices account deployment list`, sin
    escritura, el 2026-09-21). Incluye `gpt-4o` y un segundo `gpt-5-mini` en
    la cuenta secundaria (`srbaisrv-westeurope`) que no estaban en la
    hipótesis inicial del plan.

- **`di-artifacts.json`**: clasificadores y modelos personalizados de
  Document Intelligence en el recurso origen (`srbdiprodocai`, PRO) que hay
  que copiar a DEV y PRE, con el nombre de configuración que los referencia.
  Cada entrada de `classifiers`/`models` lleva una clave `promote` (booleano):
  marca si la Tarea 14 debe copiar ese artefacto o no. El clasificador
  `DocumentAICC_v1` tiene `promote: true`. El modelo `DI_NS1.4_v0` tiene
  `promote: false` y además `pendingDataFix: true` y
  `referencedByModelId: "DI_NS_1.4_v1"`: resuelto en la Tarea 8 (AB#100310),
  el id real del modelo en el recurso origen es `DI_NS1.4_v0`
  (`DI_NS_1.4_v1`, el que aún referencia la fila de `ModeloConfigs`
  `nota.simple.1_4.azure-di`, no existe). `note` documenta el porqué (fila
  `ModeloConfigs` Id=3 huérfana: ninguna tipología activa la usa, la
  tipología `nota.simple.1_4` extrae por Content Understanding, no por DI) sin
  decidir nada por sí sola — la decisión está en `promote`/`pendingDataFix`;
  no se ha tocado nada en base de datos desde aquí.

- **`datasets/<clasificador>@<version>.manifest.json`**: dataset de
  entrenamiento/referencia (contenedor, prefijo, fecha de corte y ficheros)
  usado por Document Intelligence Studio o Content Understanding Studio para
  un analyzer/clasificador. `DocumentAICC_v1@1.manifest.json` quedó con
  `"status": "pendiente de acceso al storage"` (Tarea 8, Step 3): el
  contenedor `documentai` de `srbstgproapppdocai` existe y no tiene
  restricciones de red, pero mi identidad no tiene el rol de datos
  (Storage Blob Data Reader) necesario para listar su contenido y localizar
  el prefijo exacto del proyecto del clasificador. Nota: PRO usa **dos**
  cuentas de storage con datasets (ver tabla en "Analyzers y sus datasets" más
  abajo); la petición a plataforma para este manifiesto y para
  "Estado inicial de DEV y PRE" cubre las dos, no solo `srbstgproapppdocai`.

- **`analyzers/*.json`**: definición de cada analyzer de Content
  Understanding referenciado por `ModeloConfigs`, exportada desde el recurso
  origen de PRO sin los campos de solo lectura (`status`, `createdAt`,
  `lastModifiedAt`, `warnings`, `supportedModels`), más `sourceAccount`/
  `sourceEndpoint`/`exportedAtUtc` (o `sourceAccounts`/`sourceEndpoints` en
  plural, ver abajo) para saber de qué cuenta salió cada copia. Son la
  entrada para reconstruir el analyzer en DEV y PRE (ver "Flujo de
  promoción" y el spike de la Copy API más abajo sobre por qué se
  reconstruye en vez de copiar). Generados por
  `scripts/ai/export-analyzer-definitions.ps1`.

  PRO tiene dos cuentas Foundry (`upe48-mm2avmdm-swedencentral` y
  `srbaisrv-westeurope`) y dos de los seis analyzers referenciados existen en
  ambas. Se exportaron y compararon (ignorando los campos de origen):
  - `CU_NS_1.6_0_GGAA`: **idéntico** en las dos cuentas → un solo fichero
    `analyzers/CU_NS_1.6_0_GGAA.json` con `sourceAccounts` y
    `sourceEndpoints` en plural (array con las dos cuentas).
  - `CU_NS_1.5_0`: **difiere** (`tags` trae `projectId`/`templateId` en
    Sweden Central y viene `null` en West Europe) → dos ficheros:
    `analyzers/CU_NS_1.5_0.json` (Sweden Central, `sourceAccount` singular) y
    `analyzers/CU_NS_1.5_0@srbaisrv-westeurope.json` (West Europe). **La
    Tarea 12 debe decidir cuál de las dos reconstruir en DEV/PRE** (o si hace
    falta reconstruir ambas) antes de dar esto por resuelto.

  ### Analyzers y sus datasets

  | Analyzer | Cuenta de storage | Contenedor | Prefijo |
  |---|---|---|---|
  | `CU_NS_1.4_3` | `srbstgproapppdocai` | `documentai` | `labelingProjects/01ecc742-3215-4bf8-bdc2-ea7a7ef00fd1/train` |
  | `CU_NS_1.5_0` (las dos cuentas Foundry) | `srbstgproapppdocai` | `documentai` | `labelingProjects/01ecc742-3215-4bf8-bdc2-ea7a7ef00fd1/train` |
  | `CU_NS_1.6_0_GGAA` | `srbstgproapppdocai` | `documentai` | `labelingProjects/01ecc742-3215-4bf8-bdc2-ea7a7ef00fd1/train` |
  | `CERA16_v1` | `srbstgproapppdocai` | `documentai` | `labelingProjects/30189708-d5c2-48ab-8ecf-beb626f8fabb/train` |
  | `CERA44_vado` | `srbstgprodocai` | `documentai` | `labelingProjects/6fad949e-aacd-4220-8eb6-2629e51dd7ff/train` |
  | `CERA46` | `srbstgprodocai` | `documentai` | `labelingProjects/246552b6-ae9f-42f8-b172-d12d508a9038/train` |

  Extraído de `knowledgeSources` de los 6 exports (más la copia West Europe
  de `CU_NS_1.5_0`, con el mismo prefijo). Dos cuentas de storage en total:
  `srbstgproapppdocai` (4 analyzers, 2 prefijos) y `srbstgprodocai` (2
  analyzers, 2 prefijos). La petición de rol a plataforma para reconstruir
  estos datasets en DEV/PRE debe pedir **Storage Blob Data Reader sobre las
  dos cuentas**, no solo sobre `srbstgproapppdocai`.

- **`inventory-prod-foundry.json`** / **`inventory-prod-foundry-westeurope.json`**:
  listado de analyzers custom (excluye los `prebuilt-*`) de **cada una** de
  las dos cuentas Foundry de PRO — no hay un "Foundry de PRO" único, son dos
  cuentas independientes. El primero es de `upe48-mm2avmdm-swedencentral` (45
  analyzers custom); el segundo, de `srbaisrv-westeurope` (90 analyzers en
  total, 88 `prebuilt-*` y solo 2 custom: los mismos `CU_NS_1.5_0` y
  `CU_NS_1.6_0_GGAA` que también están en Sweden Central). Cada entrada lleva
  un flag `referenced` que marca cuáles están también en `analyzers/*.json`.
  Sirve para identificar en el futuro qué analyzers de cada cuenta ya no se
  usan y se pueden retirar.

## Flujo de promoción (Tareas 10 a 14 — aún no existen)

El orden previsto para aplicar esta definición a DEV y PRE es:

1. **Datasets** — preparar los datos de entrenamiento/referencia que
   necesiten los analyzers y clasificadores antes de recrearlos.
2. **Deployments** — crear en cada cuenta OpenAI de DEV/PRE los deployments
   de `deployments.<env>.json` (asegura que el modelo/versión/SKU exista
   antes de que algo dependa de él).
3. **Analyzers de Content Understanding, por reconstrucción** — crear cada
   analyzer de `analyzers/*.json` en el recurso Foundry del entorno destino a
   partir de la definición exportada. La reconstrucción es el mecanismo de
   promoción elegido (spec §2); la Copy API existe (`:grantCopyAuthorization`
   + `:copy`) pero exige el rol **Cognitive Services User** de la misma
   identidad en origen y destino y no se ha probado end-to-end — ver el
   spike más abajo.
4. **Clasificadores de Document Intelligence, por Copy API** — copiar los
   artefactos de `di-artifacts.json` con `promote: true` desde el recurso
   origen al recurso destino usando la Copy API de Document Intelligence
   (esta sí soporta copia entre recursos; es una API distinta de la de
   Content Understanding del paso anterior).
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

**Storage de los datasets (Tarea 8, Step 3, AB#100310) -- pendiente de
plataforma.** Los datasets de entrenamiento/referencia de los analyzers y del
clasificador viven en **dos** cuentas de storage de PRO, `srbstgproapppdocai`
y `srbstgprodocai` (ver tabla "Analyzers y sus datasets" más arriba); mi
identidad no tiene el rol de datos necesario para listar blobs en ninguna de
las dos. Petición a plataforma: conceder **Storage Blob Data Reader** al
usuario del proyecto sobre `srbstgproapppdocai` y sobre `srbstgprodocai`
(idealmente acotado al contenedor `documentai` de cada una), o como
alternativa entregar el listado de blobs bajo los prefijos de la tabla.

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

Cuenta primaria (Sweden Central), los 6 analyzers y el inventario:

```powershell
pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.4_3,CU_NS_1.5_0,CU_NS_1.6_0_GGAA,CERA16_v1,CERA44_vado,CERA46
```

Cuenta secundaria (West Europe), solo para los ids que también existen ahí,
generando su propio inventario:

```powershell
pwsh scripts/ai/export-analyzer-definitions.ps1 -Ids CU_NS_1.5_0,CU_NS_1.6_0_GGAA `
    -SourceEndpoint https://srbaisrv-westeurope.services.ai.azure.com `
    -InventoryFile infra/ai/inventory-prod-foundry-westeurope.json
```

Añade `-SkipInventory` si solo quieres reexportar uno o dos analyzers de una
cuenta sin volver a listar (ni pisar el inventario) de esa cuenta — por
ejemplo, para comparar una cuenta contra otra en un directorio aparte antes
de decidir si el resultado va a `analyzers/<id>.json` o a
`analyzers/<id>@<cuenta>.json` (ver la sección de analyzers más arriba).

El script hace únicamente operaciones de lectura (GET) contra la cuenta
Foundry indicada; no crea, modifica ni borra ningún analyzer. Pagina la
lista completa (sigue `nextLink` hasta que no hay más páginas) y ordena el
inventario por `analyzerId`. Cada JSON generado (analyzers e inventario) se
escribe en UTF-8 sin BOM, con salto de línea LF y una línea final, igual que
el resto de `infra/ai/`. Un GET que falla lanza `GET <url> -> <status>:
<body>` con el cuerpo de error de la API, en vez de fallar silenciosamente
más adelante al parsear `$null` como JSON.

Nota de implementación: el script obtiene el cuerpo de cada analyzer con
`Invoke-WebRequest` y un token de `az account get-access-token`, decodificando
la respuesta explícitamente como UTF-8, en vez de leer la salida de texto de
`az rest`. Se verificó que `az rest` recodifica su salida con la página de
códigos activa de la consola y sustituye caracteres no representables (tildes,
eñes) por el carácter de sustitución U+FFFD, perdiendo contenido real de las
definiciones (por ejemplo, "Dirección" llegaba corrupto). Leer los bytes de la
respuesta directamente evita esa pérdida.
