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
  `di` — con su grupo de recursos y endpoint. Las cuentas CU llevan además
  `subscriptionId` y `location`, que la Copy API necesita para formar el id
  ARM y la región. Es el mapa que resuelve los
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
  - `contentUnderstandingDefaults` (solo en los ficheros `desired`): mapeo
    alias de modelo → nombre de deployment que Content Understanding debe
    tener como *defaults* en cada cuenta Foundry del entorno
    (`PATCH /contentunderstanding/defaults`). Sin defaults el servicio
    rechaza cualquier build de analyzer con `DefaultsNotSet` (verificado en
    `srbaisrv01devdocai` y `srbaisrv02devdocai` el 2026-09-21), y un analyzer
    copiado sin ellos falla en el primer análisis (PRE, 2026-09-22), así que
    `scripts/ai/build-analyzers.ps1` y `scripts/ai/copy-cu-analyzers.ps1` los
    comprueban y fijan antes de tocar la cuenta (función `Ensure-Defaults`
    compartida en `scripts/ai/lib/cu-defaults.ps1`, con tests Pester en
    `scripts/ai/tests/cu-defaults.Tests.ps1`). Replica el mapeo observado en PRO (`gpt-4.1`, `gpt-4.1-mini`,
    `text-embedding-3-large` y los tres alias `prebuilt-analyzer-*`) con los
    deployments de cada cuenta; cada nombre debe existir en `accounts` del
    mismo fichero.

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

- **`di-artifacts.<env>.manifest.json`**: resultado de la última pasada de
  `scripts/ai/copy-di-artifacts.ps1` contra ese entorno: cuenta origen y
  destino, y por artefacto su estado (`copied`, `present`, `not-promoted`,
  `conflict`), fecha de copia, id de la operación y `docTypes` en origen y
  destino. Lo escribe el script (no editar a mano); se fusiona por
  `(kind, id)` con la pasada anterior. `-DryRun` no lo toca.

- **`datasets/<clasificador>@<version>.<env>.manifest.json`**: dataset de
  entrenamiento/referencia (contenedor, prefijo, fecha de corte y ficheros)
  usado por Document Intelligence Studio o Content Understanding Studio para
  un analyzer/clasificador, **uno por entorno destino** (`.dev.`, `.pre.`, `.prod.`):
  el mismo dataset copiado a DEV y a PRE deja dos manifiestos que no se
  pisan. `copy-labeling-dataset.ps1` lo escribe; con `-DryRun` no escribe
  nada. `DocumentAICC_v1@1.dev.manifest.json` es la excepción: lo escribió a
  mano la réplica del proyecto de Document Intelligence Studio del
  2026-10-09 (AB#100924), no `copy-labeling-dataset.ps1`, porque un proyecto
  de DI Studio no se reubica bajo `labeling/<id>@<version>/`: el Studio lo
  abre por carpeta y los `<clase>.jsonl` referencian rutas relativas a esa
  carpeta, así que en DEV conserva el prefijo de PRO (`classification/`).
  Lleva la misma estructura (origen, prefijo, fecha de corte, ficheros con
  tamaño y MD5) y `"kind": "di-classifier-project"` para distinguirlo. Nota:
  PRO usa **dos** cuentas de storage con datasets (ver tabla en "Analyzers y
  sus datasets" más abajo).

- **`validation/<analyzerId>.json`**: muestra de validación por analyzer
  para `scripts/ai/validate-analyzer.ps1` (paso 5): referencias a 5 blobs PDF
  del dataset copiado al entorno (`name`, `size`, `md5` tal como figuran en
  `datasets/<id>@<version>.<env>.manifest.json`; la muestra es la misma en
  todos los entornos porque el dataset lo es), elegidos de forma determinista
  (índices equiespaciados sobre los PDF del manifiesto ordenados por nombre).
  No se versiona ningún PDF; el script descarga cada blob del storage del
  entorno en el momento de validar. Se regenera con `-WriteSelection`. Los
  tres `CU_NS_*` comparten dataset y por tanto la misma muestra.

- **`cu-analyzers.json`**: los analyzers de Content Understanding de PRO que se
  promocionan a DEV/PRE por Copy API (`scripts/ai/copy-cu-analyzers.ps1`):
  los 24 con nombre de negocio (incluidas versiones `_v1`/`_v2`), con
  `promote`, si los referencia `ModeloConfigs`, y estado y fecha en origen.
  Los `projectAnalyzer_*` internos de Studio quedan fuera. Generado desde
  `inventory-prod-foundry.json` el 2026-09-21.

- **`cu-analyzers.<env>.manifest.json`**: resultado de la última pasada de
  `copy-cu-analyzers.ps1` contra ese entorno: por analyzer, id en destino,
  estado (`copied`, `present`, `conflict`), fecha, operación y si la
  definición coincide con el origen. Lo escribe el script; se fusiona por id
  de destino. `-DryRun` no lo toca. La cabecera (`sourceAccount`,
  `targetAccount`) es la de la última pasada, así que una pasada con
  `-Target cu_secondary` la pisa: las copias a la cuenta secundaria se
  guardan aparte en **`cu-analyzers.<env>.cu_secondary.manifest.json`**
  (moviendo la entrada a mano y restaurando el primario con `git checkout`).

- **`analyzers/*.json`**: definición de cada analyzer de Content
  Understanding referenciado por `ModeloConfigs`, exportada desde el recurso
  origen de PRO sin los campos de solo lectura (`status`, `createdAt`,
  `lastModifiedAt`, `warnings`, `supportedModels`), más un objeto `_origin`
  (`sourceAccount`/`sourceEndpoint`/`exportedAtUtc`, o `sourceAccounts`/
  `sourceEndpoints` en plural, ver abajo) como última clave del fichero, para
  saber de qué cuenta salió cada copia. `_origin` y `analyzerId` **no forman
  parte del cuerpo de un `PUT /contentunderstanding/analyzers/{id}`**; la
  Tarea 12 debe eliminarlos antes de enviar la definición al recurso destino.
  `processingLocation`, `tags` y `description` sí son del cuerpo del `PUT` y
  se mantienen tal cual. Son la entrada para reconstruir el analyzer en DEV y
  PRE (ver "Flujo de promoción" y el spike de la Copy API más abajo sobre por
  qué se reconstruye en vez de copiar). Generados por
  `scripts/ai/export-analyzer-definitions.ps1`.

  PRO tiene dos cuentas Foundry (`upe48-mm2avmdm-swedencentral` y
  `srbaisrv-westeurope`) y dos de los seis analyzers referenciados existen en
  ambas. Se exportaron y compararon (ignorando los campos de `_origin`):
  - `CU_NS_1.6_0_GGAA`: **idéntico** en las dos cuentas → un solo fichero
    `analyzers/CU_NS_1.6_0_GGAA.json` con `sourceAccounts` y
    `sourceEndpoints` en plural (array con las dos cuentas) dentro de
    `_origin`. Esto es una decisión manual (copias idénticas en las dos
    cuentas): una reejecución del script no la reproduce, porque siempre
    exporta una sola cuenta por vez y escribe
    `CU_NS_1.6_0_GGAA@<cuenta>.json` cuando la cuenta no es la primaria; tras
    reexportar, comparar y fusionar a mano si el resultado sigue siendo
    idéntico en las dos cuentas.
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
  | `DocumentAICC_v1` (clasificador DI) | `srbstgproapppdocai` | `documentai` | `classification` (15 `<clase>.jsonl` + carpeta por clase con PDF y `.ocr.json`) |

  Los 6 de Content Understanding salen de `knowledgeSources` de los exports
  (más la copia West Europe de `CU_NS_1.5_0`, con el mismo prefijo); el de
  Document Intelligence se localizó listando el contenedor el 2026-10-09. Dos
  cuentas de storage en total: `srbstgproapppdocai` (5 artefactos, 3
  prefijos) y `srbstgprodocai` (2 analyzers, 2 prefijos). Reconstruir estos
  datasets en DEV/PRE exige **Storage Blob Data Reader sobre las dos
  cuentas**, no solo sobre `srbstgproapppdocai`.

  **Proyectos de los Studios en DEV (2026-10-09, AB#100924).** Los datasets
  de `labeling/<id>@<version>/` son copias de `train/` para validar y
  promocionar; los Studios no los ven como proyecto. Para que los
  entrenadores etiqueten en DEV (ADR-001) se replicaron además los proyectos
  en `srbstgdevdocai/documentai`: los 11 de Content Understanding Studio
  como `labelingProjects/<guid-nuevo>/` (el proyecto se crea en el Studio
  sobre `srbaisrv01devdocai` y se rellena por script con `analyzer.json`,
  `train/` y `test/` de PRO; el Studio lee el storage con la identidad del
  recurso, que necesita Storage Blob Data Contributor) y el de Document
  Intelligence Studio como `classification/`, misma ruta que en PRO (el
  proyecto `DocumentAICC_v1 DEV` se crea en el Studio sobre `srbdidevdocai`
  apuntando a esa carpeta; DI Studio lee el storage con el token del
  usuario, que necesita Storage Blob Data Contributor sobre
  `srbstgdevdocai`). El storage de DEV lleva una regla CORS por Studio
  (`https://contentunderstanding.ai.azure.com` y
  `https://documentintelligence.ai.azure.com`; el PUT ARM de `blobServices`
  es de reemplazo y debe incluir las políticas de soft delete completas o la
  política de retención lo deniega). No se pulsa Train/Build sobre un
  proyecto replicado: crearía un artefacto paralelo al que gestiona el
  pipeline 832. Los proyectos no se replican en PRE y los de PRO se
  conservan hasta el primer ciclo DEV → PRE → PRO.

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

## Flujo de promoción (Tareas 10 a 14)

**Ruta.** Los artefactos se crean (se etiquetan y entrenan) en DEV y se
promocionan por saltos DEV → PRE → PRO, uno cada vez (ADR-001,
`docs/decisiones/ADR-001-promocion-artefactos-ia-dev-pre-pro.md`). La
resuelve `scripts/ai/lib/promotion-route.ps1` (tests en
`scripts/ai/tests/promotion-route.Tests.ps1`), que comparten
`copy-labeling-dataset.ps1`, `copy-cu-analyzers.ps1`,
`copy-di-artifacts.ps1` y `validate-analyzer.ps1`:

| Destino (`-Environment`) | Origen por defecto | Otros orígenes |
|---|---|---|
| `pre` | `dev` | `prod`, solo con `-SourceEnvironment prod -FromProd` |
| `prod` | `pre` | ninguno |
| `dev` | ninguno: exige `-SourceEnvironment prod -FromProd` | — |

Se rechaza cualquier otro salto: el mismo entorno, `dev → prod` (se salta
PRE), `pre → dev` y `-FromProd` sin origen `prod`. La copia desde PRO fue la
carga inicial de DEV y PRE (septiembre de 2026, secciones siguientes) y solo
se repite para recuperar ese estado. Los manifiestos de cada pasada llevan
`sourceEnvironment`.

`apply-deployments.ps1` no tiene origen: aplica `deployments.<env>.json` y no
corre contra PRO, que no cambia de recursos (su fichero es `observed`).

En una release, la promoción la hace el pipeline 832
`azure-pipelines-ai-artifacts.yml` (ver "Pipeline de promoción" más abajo) o,
como contingencia, los scripts a mano según
`docs/procedimientos/RELEASE_MANAGEMENT.md` 2.4.

El orden de los pasos, en cualquier salto, es (el paso 1 lo hace
`scripts/ai/copy-labeling-dataset.ps1`, el paso 2
`scripts/ai/apply-deployments.ps1`, el paso 3
`scripts/ai/copy-cu-analyzers.ps1` (y `build-analyzers.ps1` solo para
reentrenar), el paso 4
`scripts/ai/copy-di-artifacts.ps1` y el paso 5
`scripts/ai/validate-analyzer.ps1`):

1. **Datasets** — preparar los datos de entrenamiento/referencia que
   necesiten los analyzers y clasificadores antes de recrearlos.
2. **Deployments** — crear en cada cuenta OpenAI de DEV/PRE los deployments
   de `deployments.<env>.json` (asegura que el modelo/versión/SKU exista
   antes de que algo dependa de él). Ver "Aplicar los deployments de un
   entorno" más abajo.
3. **Analyzers de Content Understanding, por Copy API** — copiar cada
   analyzer de `cu-analyzers.json` desde el Foundry del origen al del destino con
   `:grantCopyAuthorization` + `:copy` (`scripts/ai/copy-cu-analyzers.ps1`).
   Es el mecanismo de promoción desde la enmienda de la spec §2 del
   2026-09-21: la reconstrucción desde dataset (`build-analyzers.ps1`) no
   reproduce PRO porque el dataset actual no es el que entrenó PRO; queda
   para reentrenar con identificador nuevo. Ver "Copiar los analyzers de
   Content Understanding a un entorno" más abajo.
4. **Clasificadores de Document Intelligence, por Copy API** — copiar los
   artefactos de `di-artifacts.json` con `promote: true` desde el recurso
   origen al recurso destino usando la Copy API de Document Intelligence
   (esta sí soporta copia entre recursos; es una API distinta de la de
   Content Understanding del paso anterior). Ver "Copiar los clasificadores
   de Document Intelligence a un entorno" más abajo.
5. **Validación** — comprobar que los analyzers copiados al destino
   devuelven los mismos campos que el origen del salto sobre una muestra fija
   de PDF del dataset del destino. Ver "Validar los analyzers de un entorno"
   más abajo. Que las Functions de DEV/PRE usen los recursos propios y no los
   de PRO es el cutover (Tarea 15), no este paso.

Estos pasos corresponden a las Tareas 10-14 del plan; esta carpeta es su
entrada de datos.

## Aplicar los deployments de un entorno

`scripts/ai/apply-deployments.ps1` es el paso 2 del flujo. Lee
`deployments.<env>.json` (solo con `intent: "desired"`; el de PRO es
`observed` y el script lo rechaza) y, para cada cuenta de `accounts`,
resuelve `subscriptionId` y `resourceGroup` en `resources.<env>.json`
(cualquier alias con ese `account` que lleve `subscriptionId`; el RG de DEV
no está en la suscripción por defecto de `az`), lista los deployments por
ARM y compara uno a uno:

- no existe → `create`: PUT de
  `Microsoft.CognitiveServices/accounts/<cuenta>/deployments/<nombre>` con
  `sku.name`/`sku.capacity` y `properties.model` (`format: OpenAI`), y sondeo
  del `provisioningState` hasta `Succeeded` (o `-TimeoutMinutes`, 10 por
  defecto);
- existe con el mismo modelo, versión, SKU y capacidad → `ok`;
- existe y difiere → `differs`: se informa y **no se modifica** (alinear a
  mano o cambiar el fichero; el script nunca hace PUT sobre uno existente);
- existe en la cuenta y no está en el fichero → `unmanaged`: se lista y
  **nunca se borra**.

Antes de tocar Azure comprueba la coherencia del fichero (campos
obligatorios, nombres repetidos y que cada entrada de
`contentUnderstandingDefaults` apunte a un deployment declarado). Los
defaults de Content Understanding en sí los fijan `build-analyzers.ps1` y
`copy-cu-analyzers.ps1`, no este script. Todas las llamadas van por ARM con token e `Invoke-WebRequest`,
no con `az.cmd`. Sale con código 1 si algún deployment queda `failed` o
`timeout`; el error de ARM (por ejemplo `InsufficientQuota` si la cuota del
SKU no está concedida en la suscripción, petición P1 de AB#100311) se
muestra en la columna `Detail`.

Ensayar siempre primero con `-DryRun` (solo lecturas):

```powershell
pwsh scripts/ai/apply-deployments.ps1 -Environment dev -DryRun
pwsh scripts/ai/apply-deployments.ps1 -Environment dev
pwsh scripts/ai/apply-deployments.ps1 -Environment pre -Account srbaisrv01predocai -DryRun
```

Estado en DEV (2026-09-22): aplicado. `gpt-5-mini` (DataZoneStandard x50)
se creó en `srbaisrv01devdocai` en 5 s (`Succeeded`); el resto ya existía y
una segunda pasada en `-DryRun` deja todo en `ok`. `gpt-4o` (GlobalStandard
x250) existe en esa cuenta sin estar en el fichero y queda como `unmanaged`.

## Reconstruir los analyzers en un entorno

`scripts/ai/build-analyzers.ps1` es el paso 3 del flujo. Para cada
`analyzers/<id>.json` (ignora las copias `<id>@<cuenta>.json`; para
`CU_NS_1.5_0` se reconstruye la de la cuenta primaria, que solo difiere de
la de West Europe en `tags`) resuelve el recurso destino en
`resources.<env>.json` (`cu_primary` por defecto; `cu_secondary` solo con
`-Target cu_secondary` y tras dar a su identidad Storage Blob Data Reader
sobre el storage del entorno), el dataset en
`datasets/<id>@<versión>.<env>.manifest.json` (la versión más alta del
entorno destino, que debe tener `status: copied`) y construye el cuerpo del PUT:
quita `analyzerId`, los campos de solo lectura y `_origin`, y reescribe
`knowledgeSources[].containerUrl`/`prefix` al dataset del entorno. Todo lo
demás (`description`, `tags`, `baseAnalyzerId`, `config`, `fieldSchema`,
`processingLocation`, `models`) viaja tal cual, así que el analyzer
resultante es la misma definición reentrenada con la copia del dataset.

Antes del primer PUT en cada cuenta comprueba los defaults de Content
Understanding (`contentUnderstandingDefaults` de `deployments.<env>.json`,
ver arriba) y hace PATCH si falta algún alias. Si el analyzer ya existe con
la misma definición (comparación con claves ordenadas de lo que se envía) y
está en `ready`, lo salta; si difiere, PUT con `allowReplace=true`; con
`-Force` reconstruye siempre. Sondea `Operation-Location` hasta
`succeeded`/`ready` (o `failed`, o `-TimeoutMinutes`, 40 por defecto).

Ensayar siempre primero: `-DryRun` no escribe nada en Azure y `-DumpDir`
vuelca el cuerpo exacto de cada PUT para revisarlo.

```powershell
pwsh scripts/ai/build-analyzers.ps1 -Environment dev -Only CERA44_vado -DryRun -DumpDir docs/auxiliares/temps/2026-09-21/build-analyzers-dev
pwsh scripts/ai/build-analyzers.ps1 -Environment dev -Only CERA44_vado
pwsh scripts/ai/build-analyzers.ps1 -Environment dev
```

Cada build reentrena desde el dataset (del orden de 20 EUR y varios minutos
por analyzer, según el plan). Como el resto de scripts de `scripts/ai/`, usa
token de `az account get-access-token` con `Invoke-WebRequest` y UTF-8
explícito en cuerpo y respuesta, nunca `az rest`.

## Copiar los analyzers de Content Understanding a un entorno

`scripts/ai/copy-cu-analyzers.ps1` es el paso 3 del flujo desde el
2026-09-21. Para cada analyzer de `cu-analyzers.json` con `promote: true` (o
`-Only`) resuelve el salto (ver "Ruta" arriba), el origen (`cu_primary` de
`resources.<origen>.json`, o el alias de `-SourceTarget`) y el destino
(`cu_primary` de `resources.<env>.json`, o `cu_secondary` con `-Target`),
exige que el origen esté `ready`, y aplica la Copy API oficial
(`api-version 2025-11-01`): `POST {origen}/analyzers/{id}:grantCopyAuthorization`
con `targetAzureResourceId` + `targetRegion` (autorización con caducidad de
24 h; en esta versión no hay token portable), `POST
{destino}/analyzers/{id}:copy` con `sourceAzureResourceId` +
`sourceAnalyzerId` + `sourceRegion`, sondeo de `Operation-Location` y GET de
verificación con comparación canónica de la definición (`fieldSchema`,
`config`, `models`, `baseAnalyzerId`, `description`, `tags`). Admite distinta
suscripción y región (PRO en Sweden Central, DEV en West Europe). El origen no
cambia. Escribe `cu-analyzers.<env>.manifest.json`.

Es idempotente: si el id ya existe en destino con la misma definición se
salta (`present`); si existe con otra definición se marca `conflict` y el
script termina con error, salvo `-Force` (`allowReplace=true`).
`-TargetSuffix` crea el clon con otro id (pruebas). El clon conserva el
`knowledgeSources` apuntando al storage de PRO: es informativo, el analyzer
copiado no necesita leerlo para analizar (verificado: DEV no tiene rol sobre
ese storage y los resultados coinciden con PRO).

Permisos: la identidad que ejecuta necesita **Cognitive Services User** en
origen y destino. No hacen falta roles cruzados entre los recursos.

Defaults de Content Understanding: antes del primer analyzer comprueba los de
la cuenta destino contra `contentUnderstandingDefaults` de
`deployments.<env>.json` y hace PATCH si falta o difiere algún alias (misma
`Ensure-Defaults` que `build-analyzers.ps1`, en `scripts/ai/lib/cu-defaults.ps1`;
en `-DryRun` solo muestra el plan). Después, por analyzer, exige que los alias
de `models` del origen (`gpt-4.1`, `text-embedding-3-large`…) estén mapeados
en destino y se para antes de copiar si no lo están. La Copy API no exige los
defaults, pero sin ellos el analyzer copiado falla en el primer análisis con
`needs a 'completion' model deployment ... but none was resolved` (visto en
PRE el 2026-09-22, cuando las dos cuentas seguían en `DefaultsNotSet` tras la
copia). `-SkipDefaults` omite las dos comprobaciones.

Prueba del 2026-09-21: `CERA46` → `CERA46_copytest` en `srbaisrv01devdocai`,
copia en 9 s, definición idéntica; `validate-analyzer.ps1 -Only CERA46
-TargetSuffix _copytest` dio 90,0 % global y 95,6 % en `extract` con markdown
idéntico 5/5, por encima de la línea base PRO/PRO (85,7 %).

```powershell
# promoción de una release: DEV -> PRE y, tras las puertas de PRE, PRE -> PRO
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment pre -DryRun
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment pre
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment prod -DryRun   # solo lectura; en real, por el pipeline
# cuenta secundaria (cu_secondary): solo los analyzers de las filas "-we" de ModeloConfigs,
# desde la secundaria del origen (el script fija antes los defaults de CU de esa cuenta)
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment pre -Only CU_NS_1.5_0,CU_NS_1.6_0_GGAA -Target cu_secondary -SourceTarget cu_secondary

# carga inicial y su recuperación: PRO -> DEV (comandos del 2026-09-21 y 2026-09-22)
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -SourceEnvironment prod -FromProd -Only CERA46 -TargetSuffix _copytest -DryRun
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -SourceEnvironment prod -FromProd -Only CERA46 -TargetSuffix _copytest -DumpDir docs/auxiliares/temps/2026-09-21/copy-cu-dev
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -SourceEnvironment prod -FromProd -Force      # los 24, sobrescribiendo los reconstruidos
pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -SourceEnvironment prod -FromProd            # puerta: los 6 con dataset en DEV
pwsh scripts/ai/copy-cu-analyzers.ps1 -Environment dev -SourceEnvironment prod -FromProd -Only CU_NS_1.5_0,CU_NS_1.6_0_GGAA -Target cu_secondary -SourceTarget cu_secondary
```

El 2026-09-22 la cuenta secundaria de DEV (`srbaisrv02devdocai`) recibió
`CU_NS_1.5_0` y `CU_NS_1.6_0_GGAA` desde `srbaisrv-westeurope`, tras fijar
sus defaults a mano con el mapeo de `deployments.dev.json` (estaba en
`DefaultsNotSet`). Sin ese paso, las filas `-we` daban 404 `ModelNotFound`
tras el cutover y el smoke no lo detectaba. Desde el 2026-09-23 el propio
`copy-cu-analyzers.ps1` hace esa comprobación (ver arriba).

## Copiar los clasificadores de Document Intelligence a un entorno

`scripts/ai/copy-di-artifacts.ps1` es el paso 4 del flujo. Para cada
entrada de `di-artifacts.json` con `promote: true` (hoy solo el clasificador
`DocumentAICC_v1`; `DI_NS1.4_v0` queda anotado como `not-promoted`) resuelve
el salto (ver "Ruta" arriba), el origen en `resources.<origen>.json` y el
destino en `resources.<env>.json` (alias `di`), comprueba que el artefacto
existe en origen (la cuenta `sourceAccount` de `di-artifacts.json` solo se
exige si el origen es PRO, porque describe la carga inicial) y aplica la Copy
API oficial de Document Intelligence (`api-version 2024-11-30`):
`POST {destino}/documentintelligence/documentClassifiers:authorizeCopy` con
el mismo id y una descripción de procedencia, `POST
{origen}/documentintelligence/documentClassifiers/{id}:copyTo` con la
autorización como cuerpo, y sondeo de `Operation-Location` hasta
`succeeded`. El origen no cambia: `copyTo` solo lee el clasificador de origen.
Al terminar verifica en destino que los `docTypes` coinciden con el origen y
escribe `di-artifacts.<env>.manifest.json`.

Es idempotente: si el artefacto ya existe en destino con la misma firma
(`docTypes`; en modelos de extracción, además los nombres de campo por
`docType`) se salta (`present`); si existe con otra firma se marca
`conflict` y el script termina con error sin tocarlo, salvo `-Force`, que lo
borra en destino y lo vuelve a copiar. Los ids no se renombran porque son lo
que referencia `ModeloConfigs` en cada entorno.

Límites verificados en la documentación oficial: la copia de clasificadores
v4.0 solo se soporta entre recursos de East US, West US 2 y West Europe (las
tres cuentas de DI del proyecto están en West Europe) y exige que el origen
se haya entrenado con `2024-11-30`. El accessToken de la autorización no se
imprime ni se vuelca.

```powershell
pwsh scripts/ai/copy-di-artifacts.ps1 -Environment pre -DryRun            # DEV -> PRE
pwsh scripts/ai/copy-di-artifacts.ps1 -Environment pre
pwsh scripts/ai/copy-di-artifacts.ps1 -Environment prod -Only DocumentAICC_v1 -DryRun   # PRE -> PRO, solo lectura
# carga inicial y su recuperación: PRO -> DEV
pwsh scripts/ai/copy-di-artifacts.ps1 -Environment dev -SourceEnvironment prod -FromProd -DryRun -DumpDir docs/auxiliares/temps/2026-09-21/copy-di-dev
pwsh scripts/ai/copy-di-artifacts.ps1 -Environment dev -SourceEnvironment prod -FromProd
```

Como el resto de scripts de `scripts/ai/`, usa token de `az account
get-access-token` con `Invoke-WebRequest` y UTF-8 explícito, nunca `az rest`.

## Validar los analyzers de un entorno

`scripts/ai/validate-analyzer.ps1` es el paso 5 del flujo (Tarea 13,
AB#100315). Para cada `analyzers/<id>.json` resuelve el salto (ver "Ruta"
arriba: compara el destino con el origen del salto, DEV para PRE y PRE para
PRO), el recurso origen (`cu_primary` de `resources.<origen>.json`, o
`-SourceTarget`) y el destino (`cu_primary` de `resources.<env>.json`, o
`cu_secondary` con `-Target`), comprueba con `GET`
que el analyzer está `ready` en los dos, toma la muestra de
`validation/<id>.json`, descarga cada PDF del storage del entorno (token de
`https://storage.azure.com/`, MD5 verificado contra el manifiesto) y lo envía
en bytes a `POST /contentunderstanding/analyzers/<id>:analyzeBinary`
(`api-version 2025-11-01`) en origen y destino. Se envía en binario porque en
esa versión `:analyze` solo acepta `inputs[].url` y la identidad del recurso
de PRO no puede leer el storage de DEV/PRE (los roles cruzados van en el otro
sentido). Sondea `Operation-Location` hasta `Succeeded`. Primero compara el
`markdown` de `result.contents[0]` (salida de OCR + layout) de los dos lados:
si es idéntico, la etapa de extracción de contenido es la misma y cualquier
diferencia de campos viene de la etapa LLM. Después compara
`result.contents[0].fields` campo a campo según el `fieldSchema` de la
definición, en forma canónica: cadenas sin espacios sobrantes e ignorando
mayúsculas, números redondeados a 6 decimales, arrays y objetos por JSON
canónico recursivo; un campo vacío en los dos lados cuenta como igual.

Por analyzer informa el acuerdo global (campos iguales / comparados) y el
acuerdo restringido a campos `extract`, que es el que mide la fidelidad de la
copia: los campos `generate` llevan varianza propia del modelo incluso contra
el mismo recurso. El umbral (`-MinFieldsRatio`, 0,85 por defecto) se aplica al
acuerdo global y, si algún analyzer queda por debajo, el script termina con
error después de procesar todos. Mide "la copia reproduce el original" sobre
documentos ya vistos en el entrenamiento, no generalización.

**Criterio de aceptación de una copia.** Un analyzer copiado se da por bueno
cuando (a) la definición en destino es idéntica a la de origen (lo comprueba
`copy-cu-analyzers.ps1`), (b) el `markdown` es idéntico en todos los pares y
(c) el acuerdo global es igual o superior a la línea base PRO/PRO menos un
margen. La línea base medida el 2026-09-21 con `-SelfCheck` fue 0,86-0,91, de
ahí el umbral 0,85: por encima de él la diferencia es varianza del modelo,
no una copia infiel. Un umbral fijo más alto (el 0,9 original del plan) haría
fallar copias correctas por ruido de los campos `generate`.

El informe completo va a `docs/auxiliares/temps/<fecha>/validacion-analyzers-<env>.txt`
(gitignored; `-OutFile` lo cambia) y `-DumpDir` guarda el resultado crudo de
cada análisis. Cada `:analyzeBinary` cuesta dinero en los dos recursos
(páginas + tokens): con 6 analyzers y 5 PDF son 60 llamadas por pasada.
`-DryRun` solo hace los `GET` de comprobación.

```powershell
# promoción: PRE frente a DEV y PRO frente a PRE
pwsh scripts/ai/validate-analyzer.ps1 -Environment pre -DryRun
pwsh scripts/ai/validate-analyzer.ps1 -Environment pre
pwsh scripts/ai/validate-analyzer.ps1 -Environment prod -DryRun   # solo GET; en real, por el pipeline
# DEV no tiene salto anterior: se compara con la carga inicial de PRO
pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -SourceEnvironment prod -FromProd -WriteSelection -DryRun   # (re)genera validation/*.json y ensaya
pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -SourceEnvironment prod -FromProd -DryRun
pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -SourceEnvironment prod -FromProd -Only CERA44_vado -DumpDir docs/auxiliares/temps/2026-09-21/validate-analyzers
pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -SourceEnvironment prod -FromProd
```

Dry-run de lectura `pre` frente a `dev` del 2026-09-24: los 6 analyzers
`ready` en los dos lados.

Resultado de la pasada contra los 24 analyzers copiados por Copy API (DEV,
2026-09-21, 60 llamadas): markdown idéntico en los 30 pares y acuerdo global
86,3 % (`CERA16_v1`), 95,0 % (`CERA44_vado`), 97,1 % (`CERA46`), 91,4 %
(`CU_NS_1.4_3`), 93,6 % (`CU_NS_1.5_0`) y 90,8 % (`CU_NS_1.6_0_GGAA`): los
seis pasan el criterio de aceptación (umbral 0,85). Las diferencias restantes
se concentran en campos `generate` y en extracciones largas truncadas a distinta
longitud, es decir, varianza del modelo.

Resultado de la primera pasada contra los analyzers **reconstruidos** (DEV,
2026-09-21, 60 llamadas), que motivó el cambio a la Copy API: markdown
idéntico en los 30 pares; acuerdo global 47-58 % en los
tres `CERA*`, 82-85 % en `CU_NS_1.4_3` y `CU_NS_1.5_0`, 93 % en
`CU_NS_1.6_0_GGAA`. La línea base PRO/PRO (`-SelfCheck`, 20 llamadas) dio
85,7 % en `CERA46` y 91,4 % en `CU_NS_1.4_3`, así que la brecha no es
varianza del modelo. Los defaults de modelo de PRO y DEV son idénticos
(`gpt-4.1-715420`, `text-embedding-3-large-030358`). La causa es que **el
dataset actual no es el que entrenó los analyzers de PRO**:

- `CERA44_vado` y `CERA46` (PRO construidos el 2026-06-25): los 37 y 45
  `.labels.json` del proyecto de etiquetado se vaciaron el 2026-07-17
  (145-156 bytes, `fieldLabels: {}`). `CERA16_v1` (PRO del 2026-06-25): los
  99 `.labels.json` se reescribieron el 2026-08-20 y los de la muestra tampoco
  tienen `fieldLabels`. DEV los reconstruyó sin ejemplos etiquetados.
- `CU_NS_1.4_3` (PRO del 2026-04-15) y `CU_NS_1.5_0` (2026-05-05): los 50
  `.labels.json` del proyecto se modificaron el 2026-07-16. En la muestra, DEV
  coincide con las etiquetas actuales en 77/97 y 76/97 campos escalares, PRO
  en 65/97 y 69/97: DEV reproduce el dataset de hoy, PRO uno anterior.
- `CU_NS_1.6_0_GGAA` (PRO del 2026-07-17, un día después de las etiquetas):
  PRO 78/87 y DEV 77/87 frente a las etiquetas, y 93 % entre sí. Es el único
  cuyo dataset actual coincide con el que entrenó PRO.

Conclusión: la reconstrucción es fiel al dataset versionado, pero ese dataset
ya no reproduce PRO para cinco de los seis analyzers. Decisión del 2026-09-21:
el baseline es PRO tal cual y la promoción pasa a la Copy API (sección
anterior); el clon `CERA46_copytest` dio 90,0 % frente al 47,1 % del
reconstruido.

El acuerdo origen/destino solo se interpreta frente a una línea base:
`-SelfCheck` analiza cada PDF dos veces contra el propio recurso origen y
compara las dos respuestas entre sí (informe
`validacion-analyzers-<env>-selfcheck.txt`). Si el acuerdo origen/destino es
del mismo orden que el acuerdo origen/origen, la diferencia es varianza del modelo
y la copia es fiel; si queda claramente por debajo, comparar la fecha
`createdAt` del analyzer en PRO con la `Last-Modified` de los `.labels.json`
del prefijo origen: si las etiquetas cambiaron después del build, el dataset
copiado no es el que entrenó PRO.

```powershell
pwsh scripts/ai/validate-analyzer.ps1 -Environment dev -SourceEnvironment prod -FromProd -SelfCheck -DumpDir docs/auxiliares/temps/2026-09-21/validate-analyzers-selfcheck
```

`-SelfCheck` analiza dos veces contra el origen del salto: con
`-Environment pre` mide DEV/DEV, no PRO/PRO.

## Pipeline de promoción (AB#100675)

`azure-pipelines-ai-artifacts.yml` lanza los pasos 1 a 5 del flujo para un
salto, más el export y el diff de configuración. Está registrado en Azure
DevOps como el pipeline **832 `AI DocClassExt (AiArtifacts)`** y usa el
service connection WIF `AI DocClassExt Promocion IA` (federado, sin
secretos; sus 15 asignaciones de rol se comprueban con el script de la
solicitud del 2026-09-22, fuera de git). Su uso dentro de una release está en
`docs/procedimientos/RELEASE_MANAGEMENT.md` 2.4 (salto DEV → PRE) y 4.12
(PRE → PRO).

| Parámetro | Valores | Efecto |
|---|---|---|
| `targetEnvironment` | `dev`, `pre` (por defecto), `prod` | Destino; el origen es el salto anterior |
| `fromProd` | booleano, `false` | Origen PRO para `dev` o `pre` (recuperar la carga inicial); quita Export y ConfigSeed |
| `preGatesPassed` | booleano, `false` | Obligatorio con `prod`: las cuatro puertas de PRE en PASS |
| `releaseTag` | texto, `auto` (por defecto) = `build-<id>` | Nombre de los ficheros de config exportados; ADO trata como obligatorio un parámetro string sin default no vacío, de ahí el centinela `auto` |
| `runValidation` | booleano, `true` | Lanza `validate-analyzer.ps1` (cuesta dinero: `analyzeBinary` en los dos lados) |
| `dryRun` | booleano, `false` | Ensayo sin escrituras: todos los scripts de `scripts/ai/` reciben `-DryRun` (solo GET y el plan de lo que harían); Export y ConfigSeed corren igual porque solo leen. Lanzar así el primer run de cada salto |

Etapas, todas en el pool privado `docia-mdp-private` (SQL y DI de PRE por
private endpoint):

1. **Guard**: rechaza `dev` sin `fromProd`, `fromProd` con `prod` y `prod`
   sin `preGatesPassed`.
2. **Export**: `export-config-release.ps1` contra la BD del origen, solo
   lectura, con el SC del origen. Artefacto `db-config`.
3. **AiArtifacts**: job de despliegue sobre el environment de ADO del
   destino (ver "Aprobaciones y Permits" más abajo). Con el SC
   `AI DocClassExt Promocion IA`: `copy-labeling-dataset.ps1` para cada
   `analyzers/<id>.json` (sin las copias `<id>@<cuenta>.json`),
   `copy-cu-analyzers.ps1` en `cu_primary` y en `cu_secondary` (solo
   `CU_NS_1.5_0` y `CU_NS_1.6_0_GGAA`), `copy-di-artifacts.ps1` y
   `validate-analyzer.ps1`. `apply-deployments.ps1` va con el SC del destino
   y no corre en `prod`. Publica los manifiestos y el informe de validación
   en el artefacto `ai-artifacts-<env>-<intento>`, también si falla: los
   manifiestos se escriben en el agente y hay que versionarlos a mano aquí.
4. **ConfigSeed**: export de la BD del destino y `diff-config-exports.py`
   por clave natural frente al export del origen (en `prod` sin
   `ModeloConfigs`). Artefacto `config-diff` con el diff y el `.hashes.json`
   del destino; su `.sql` no se publica porque puede llevar keys. **No aplica
   nada**: la configuración no se promociona por `Id` (regla fija 4 del
   runbook).

El pipeline no pasa `-Force`: sobre un entorno ya promocionado todo sale
`present` u `ok`, y un artefacto que existe en destino con otra definición
para el run con `conflict`. Una versión nueva de un analyzer o clasificador
lleva identificador nuevo; sustituir uno existente es una decisión manual
fuera del pipeline.

### Aprobaciones y Permits

- Los environments de ADO `pre` y `prod` tienen un check de aprobación
  (checks 64 y 65, creados el 2026-09-30) con un único aprobador y timeout de
  30 días. Son environments **compartidos** con el resto de pipelines de
  despliegue: la aprobación afecta a todo lo que despliega a PRE/PRO, no solo
  a este pipeline. Riesgo asumido y documentado: esos despliegues esperan la
  aprobación de una sola persona.
- La primera vez que el pipeline usa un recurso protegido (cada service
  connection, cada environment, el pool `docia-mdp-private`) ADO detiene el
  run y pide un **Permit** para ese recurso, que se concede a mano en la UI
  (el PATCH del endpoint REST `pipelinePermissions` está bloqueado en este
  entorno). Los Permits del salto DEV → PRE se concedieron durante el run
  79587; el primer run con `targetEnvironment=prod` pedirá además el Permit
  del SC de PRO y el del environment `prod`, más la aprobación de `prod`.

### Estado de verificación (fase D de AB#100675)

El ensayo en seco DEV → PRE (run 79587, 2026-09-30, `dryRun=true`,
succeeded) verificó el alcance de red del pool a los endpoints de IA y SQL de
DEV y PRE, `python` en el pool (ConfigSeed generó el diff) y el token del SC
(ningún 401). Resultado: PRE ya idéntico a DEV en artefactos de IA; diff de
config DEV/PRE: `CatalogoTdn1` 5 diferencias (conocidas), `ModeloConfigs` 11
(endpoints por entorno, sin revisar campo a campo), `PromptTemplates` 2
(solo `PublishedAtUtc`).

El ensayo en seco PRE → PRO (run 79596, 2026-09-30, `dryRun=true`,
succeeded, con el arreglo de `validate-analyzer.ps1` en dry-run) verificó lo
mismo contra PRO: cero 401 en las dos cuentas Foundry y el DI de PRO, plan
`skip`/`present` sin ningún `conflict` (PRO ya tiene todos los artefactos,
es el origen original), validación con 6/6 analyzers omitidos por no existir
aún manifiestos `.prod.` (esperado: los escribe la pasada real de
`copy-labeling-dataset.ps1`) y ConfigSeed contra la BD de PRO: diff
PRE/PRO con 0 diferencias en `CatalogoTdn2`, `PluginTipologiaConfigs`,
`PromptTemplates` y `Tipologias`, y 5 filas de `CatalogoTdn1` con
`Descripcion` distinta (CERA, COMU, CORR, CUAD, NOTS, las conocidas de la
grafía; `ModeloConfigs` fuera del diff en `prod` por diseño). Queda
pendiente para el primer run real: el contenedor `documentai` en
`srbstgprodocai` (el seco solo lista el origen). Ningún script copia
todavía el dataset del clasificador de DI (`srbstgproapppdocai`).

## Cutover de un entorno (Tarea 15)

Que las Functions de un entorno usen sus recursos propios en lugar de los de
PRO es un cambio de configuración, no de artefactos, y se hace en este orden
(ventana sin tráfico de evaluación en curso):

1. **Keys en el Key Vault del entorno** con los nombres de secreto actuales
   (`Extraction--AzureContentUnderstanding--ApiKey`,
   `Extraction--GptFallback--ApiKey`, `Classification--GptFallback--ApiKey`
   con la key de la cuenta Foundry primaria;
   `Classification--AzureDocumentIntelligence--ApiKey` con la de DI). Solo
   cubren el modo `ApiKey` de los App Settings; las filas de `ModeloConfigs`
   usan `DefaultAzureCredential`.
2. **Variables `AI_*` del entorno en `infra/ai/pipeline-variables.yml`**
   (`AI_OPENAI_PRIMARY_ENDPOINT`, `AI_CU_PRIMARY_ENDPOINT`,
   `AI_CU_SECONDARY_ENDPOINT`, `AI_DI_ENDPOINT`) apuntando a
   `resources.<env>.json`, y despliegue. Es una plantilla de variables que
   incluyen los dos pipelines que aplican App Settings a la Function App:
   el principal (`azure-pipelines.yml`) y el de Functions
   (`azure-pipelines-functions.yml`). Cualquiera de los dos crea las cuatro
   `AI__Resources__*__Endpoint` si no existen; el de Functions avisa con un
   warning si ya existen con otro valor. El bloque `dev` apunta a DEV desde
   el 2026-09-22 y `pre` a PRE desde el 2026-09-23; `prod` apunta a los
   recursos de PRO, que PRO resuelve por alias desde el 2026-10-08 (v1.0.0,
   AB#100321). Ningún pipeline cambia las
   cuatro claves de las opciones directas que ya existen con PRO (ver paso
   2b).
2b. **Forzar a mano los cuatro endpoints de las opciones directas**
   (`Extraction__AzureContentUnderstanding__Endpoint`,
   `Extraction__GptFallback__Endpoint`,
   `Classification__AzureDocumentIntelligence__Endpoint`,
   `Classification__GptFallback__Endpoint`). `ensure-app-settings.ps1`
   conserva a propósito las claves existentes, y se ha decidido no cambiarlo
   (el cutover es un paso puntual por entorno). Con los valores de
   `resources.<env>.json`, para DEV:

   ```powershell
   # El RG de DEV está en la suscripción Core Desarrollo (8764f9ff…), no en la
   # de PRO que az usa por defecto: sin --subscription da AuthorizationFailed.
   az functionapp config appsettings set --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d `
     --resource-group SRBRGDEVDOCSAI --name srbappdevdocai --output none --settings `
     "Extraction__AzureContentUnderstanding__Endpoint=https://srbaisrv01devdocai.services.ai.azure.com/" `
     "Extraction__GptFallback__Endpoint=https://srbaisrv01devdocai.openai.azure.com" `
     "Classification__AzureDocumentIntelligence__Endpoint=https://srbdidevdocai.cognitiveservices.azure.com/" `
     "Classification__GptFallback__Endpoint=https://srbaisrv01devdocai.openai.azure.com"
   ```

   Después, comprobar que las ocho claves de IA llevan hosts del entorno:

   ```powershell
   az functionapp config appsettings list --subscription 8764f9ff-fe37-4c03-bde9-6294622bef6d `
     --resource-group SRBRGDEVDOCSAI --name srbappdevdocai `
     --query "[?ends_with(name, '__Endpoint') && name != 'GDC__Endpoint'].[name, value]" -o table
   ```

   Sin este paso, `AuthMode=ApiKey` con la key del entorno contra el host de
   PRO devuelve 401 en CU, DI y fallback GPT.
3. **`scripts/ai/set-resource-aliases.sql`** contra la BD del entorno:
   asigna `ResourceAlias` (`openai_primary`, `cu_primary`, `cu_secondary`
   para las claves `-we`, `di`) a cada fila activa de `ModeloConfigs` y
   elimina el `Endpoint` explícito, con copia previa en
   `ModeloConfigs__bak_<fecha>`. Idempotente; ejecutar con lotes `GO` (patrón
   `aplicar-sql-dev.ps1`).
3b. **`scripts/ai/set-auth-mode-identity.sql`** contra la BD del entorno si
   sus filas siguen en `AuthMode = ApiKey` (las copias desde PRO las dejan
   así): pone `DefaultAzureCredential` en cada fila activa de los proveedores
   de IA, con copia previa `ModeloConfigs__bak_<fecha>`. Idempotente; el
   seed no lo revierte porque solo reinyecta propiedades ausentes o vacías.
   Requiere que la identidad de la Function App tenga ya los roles sobre las
   cuentas del entorno; DEV quedó así el 2026-09-22 y PRE lo aplica en su
   cutover (AB#100320); PRO lo aplicó en la ventana de v1.0.0 (2026-10-08,
   AB#100321).
4. **Reinicio de la Function App** para vaciar la caché de registros.
5. **Comprobación**: una petición de ingest con extracción CU y otra de
   clasificación; en App Insights `EndpointEfectivo` debe ser del entorno y
   ninguna traza debe contener `upe48-mm2avmdm-swedencentral`,
   `srbaisrv-westeurope` ni `srbdiprodocai`.
6. **`scripts/ai/clear-model-api-keys.sql`** contra la BD del entorno si sus
   filas autentican por identidad (`AuthMode = DefaultAzureCredential`): las
   copias DEV←PRO dejaron en `ConfiguracionJson.ApiKey` las keys de PRO en
   claro. No se usan con identidad, pero no deben quedarse. Idempotente, con
   copia previa `ModeloConfigs__bak_<fecha>`; solo toca filas con identidad.

Vuelta atrás: restaurar `ConfiguracionJson` desde la tabla `__bak`, devolver
las variables `AI_*` a los valores de PRO y redesplegar; los roles cruzados de
DEV y PRE sobre los recursos de PRO siguen vigentes hasta que Plataforma los
retire (Step 3 de AB#100321).

Dos trampas conocidas y cómo quedan resueltas:

- `scripts/configuration/ensure-app-settings.ps1` (el que usa el pipeline)
  no sobrescribe claves existentes, y no se cambia. Las cuatro
  `AI__Resources__*__Endpoint` las crea el despliegue; las cuatro claves de
  las opciones directas que ya existen con PRO se fuerzan a mano en cada
  entorno (paso 2b) y se comprueban con la consulta de ese paso.
- `ConfigurationSeedService.MergeMissingJsonProperties` reinyecta en cada
  arranque cualquier propiedad ausente o vacía desde los seeds
  `config/*/models.json`. Desde el 2026-09-22 los seeds **no llevan
  `Endpoint`** (solo `ResourceAlias`), así que lo único que reinyectan es
  `"Endpoint": ""`, que `AiEndpointResolver` trata como ausente y el SQL
  también (`NULLIF`). Un test unitario (`ConfigurationSeedModelosTests`)
  falla si algún seed vuelve a llevar un `Endpoint` con valor. Las filas que
  ya tienen `Endpoint` explícito (PRE y PRO hasta su cutover) no se tocan:
  el merge nunca pisa un valor no vacío.

Efecto colateral en local: `appsettings.json` declara los cuatro alias con
`Endpoint` vacío, y un alias presente pero vacío cuenta como no mapeado. Para
arrancar las Functions en local contra una BD sembrada sin `Endpoint`, hay
que rellenar `AI__Resources__*__Endpoint` en `local.settings.json` (están en
`local.settings.template.json`).

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

Cuenta primaria (Sweden Central, también el valor por defecto de
`-PrimaryEndpoint`), los 6 analyzers y el inventario:

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

Como `-SourceEndpoint` no coincide con `-PrimaryEndpoint` (comparación sin
barra final ni distinción de mayúsculas/minúsculas), el script escribe
automáticamente `analyzers/CU_NS_1.5_0@srbaisrv-westeurope.json` y
`analyzers/CU_NS_1.6_0_GGAA@srbaisrv-westeurope.json` en vez de
`<id>.json`, aunque se use el mismo `-OutDir` por defecto: una reejecución de
la cuenta secundaria nunca pisa los ficheros ya exportados de la cuenta
primaria. Si se omite `-InventoryFile`, el nombre por defecto también
incorpora la cuenta (`infra/ai/inventory-prod-foundry-<cuenta>.json`); aquí
se pasa explícito para mantener el nombre corto `westeurope` ya usado en el
repositorio. El plural `sourceAccounts`/`sourceEndpoints` de
`analyzers/CU_NS_1.6_0_GGAA.json` es una decisión manual (copias idénticas
en las dos cuentas) que una reejecución no reproduce: tras reexportar,
comparar y fusionar a mano.

Añade `-SkipInventory` si solo quieres reexportar uno o dos analyzers de una
cuenta sin volver a listar (ni pisar el inventario) de esa cuenta.

El script hace únicamente operaciones de lectura (GET) contra la cuenta
Foundry indicada; no crea, modifica ni borra ningún analyzer. Pagina la
lista completa (sigue `nextLink` hasta que no hay más páginas) y ordena el
inventario por `analyzerId`. Cada JSON generado (analyzers e inventario) se
escribe en UTF-8 sin BOM, con salto de línea LF y una línea final, igual que
el resto de `infra/ai/`. Los campos de origen de cada analyzer van agrupados
bajo `_origin`, como última clave del objeto (ver la sección de analyzers
más arriba sobre por qué no van sueltos en la raíz). Un GET que falla lanza
`GET <url> -> <status>: <body>` con el cuerpo de error de la API, en vez de
fallar silenciosamente más adelante al parsear `$null` como JSON.

Nota de implementación: el script obtiene el cuerpo de cada analyzer con
`Invoke-WebRequest` y un token de `az account get-access-token`, decodificando
la respuesta explícitamente como UTF-8, en vez de leer la salida de texto de
`az rest`. Se verificó que `az rest` recodifica su salida con la página de
códigos activa de la consola y sustituye caracteres no representables (tildes,
eñes) por el carácter de sustitución U+FFFD, perdiendo contenido real de las
definiciones (por ejemplo, "Dirección" llegaba corrupto). Leer los bytes de la
respuesta directamente evita esa pérdida.
