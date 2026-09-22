# IA propia por entorno y promoción de artefactos de IA (DEV, PRE, PRO)

- **Work item**: Epic AB#100298; Features AB#100299 (fase 0), AB#100300 (fase 1), AB#100301 (fase 2), AB#100302 (fase 3); PBIs AB#100303 a AB#100321, uno por tarea del plan
- **Fecha**: 2026-09-15
- **Rama**: `docs/ia-propia-por-entorno` (diseño); la implementación irá en `feature/100298-ia-propia-por-entorno` desde `develop` posterior al tag `release-2026-09-15`
- **Estado**: diseño aprobado por secciones, pendiente de plan de implementación
- **Origen**: análisis del documento *DocumentIA: estrategia multientorno para Azure AI Foundry* contrastado con el estado real de la plataforma

## Problema

Cada entorno tiene su propio stack de IA provisionado (Document Intelligence y dos cuentas
Foundry con Content Understanding y Azure OpenAI), pero por decisión funcional de julio de
2026 la configuración de DEV, PRE y PRO apunta a los recursos de PRO. Consecuencias
verificadas:

- Las evaluaciones lanzadas desde DEV compiten por la cuota de tokens de PRO y provocan
  429 que falsean mediciones (2026-07-31: 118 respuestas 429 en tres horas).
- Los analyzers experimentales de DEV se crean dentro del Foundry de PRO. Cualquier
  experimento toca producción.
- La factura de IA del grupo de recursos de PRO incluye el consumo de DEV y PRE. En
  septiembre la BD de PRO registra la mitad de las páginas de layout facturadas.
- El endpoint físico de PRO vive dentro de cada fila de `ModeloConfigs`. Replicar la
  configuración entre entornos obliga a reescribir los datos.
- No existe un mecanismo de promoción para los artefactos entrenados: clasificadores
  custom de Document Intelligence y analyzers de Content Understanding se crean a mano en
  el recurso de PRO y su definición no está en el repositorio.

## Objetivo

DEV y PRE consumen sus propios recursos de IA, construidos desde una definición única y
versionada; los artefactos de IA (analyzers, clasificadores, deployments, configuración en
BD) se promocionan entre entornos por pipeline con puertas de calidad; PRE es puerta
técnica de release; PRO no cambia de recursos. La filosofía del proyecto se conserva: dar
de alta modelos, tipologías, prompts y catálogos sigue siendo un cambio de datos sin
despliegue.

## Fuera de alcance

- Cambiar recursos, deployments o nombres en PRO. Los deployments de PRO se mantienen,
  incluido el deployment `gpt-4o-mini` que sirve `gpt-4.1-mini`.
- Limpiar el Foundry de PRO (25 `projectAnalyzer_*` y versiones huérfanas). Solo se
  inventarían.
- Infraestructura como código de los recursos Azure. La provisión es de plataforma.
- Copiar datos operativos de PRO a PRE o DEV. PRE trabaja con el corpus gobernado del
  harness de evaluación.
- Cambiar el modo de autenticación por defecto. Se mantienen `DefaultAzureCredential` y
  `ApiKey` configurables por fila, como hoy.
- UAT de negocio en PRE.
- Migración de gpt-4.1-mini a gpt-5-mini (trabajo aparte ya en curso).

## Estado verificado el 2026-09-15

| Elemento | DEV | PRE | PRO |
|---|---|---|---|
| Document Intelligence | `srbdidevdocai`, sin clasificadores ni modelos custom | `srbdipredocai`, sin clasificadores ni modelos custom | `srbdiprodocai`: `DocumentAICC_v0` (5 tipos), `DocumentAICC_v1` (15 tipos), `modelo1`, `DI_NS1.4_v0` |
| Foundry 01 | `srbaisrv01devdocai`, deployments idénticos a PRO salvo gpt-5-mini; analyzers no listables con el usuario actual | `srbaisrv01predocai`, ídem | `upe48-mm2avmdm-swedencentral` (Sweden Central): 45 analyzers custom, 6 deployments incluido gpt-5-mini DataZoneStandard |
| Foundry 02 | `srbaisrv02devdocai`, deployments gpt-4.1, gpt-4.1-mini y embeddings; sin analyzers | `srbaisrv02predocai`, ídem | `srbaisrv-westeurope` (West Europe): `CU_NS_1.5_0`, `CU_NS_1.6_0_GGAA` |
| Identidad de la Function | `SystemAssigned`; Cognitive Services User sobre DI y Foundry 01, Foundry User sobre Foundry 01; sin rol sobre Foundry 02 | por verificar | por verificar |
| `ModeloConfigs` | 18 filas, todas `DefaultAzureCredential`, todas con endpoint de PRO | BD vacía hasta el release de esta semana | referencia |
| Acceso de red a la IA | público habilitado en los tres recursos | por verificar | privado con private endpoints |

Artefactos referenciados por filas activas de `ModeloConfigs` en DEV, que son los que se
promocionan:

- Content Understanding: `CU_NS_1.4_3`, `CU_NS_1.5_0` (primario y secundario),
  `CERA16_v1`, `CERA44_vado`, `CERA46`. En PRO además `CU_NS_1.6_0_GGAA`.
- Document Intelligence: clasificador `DocumentAICC_v1`, extractor `DI_NS_1.4_v1`, layout
  `prebuilt-layout`. Discrepancia a resolver en el inventario: la fila de DEV referencia
  `DI_NS_1.4_v1`, pero el recurso de PRO lista `DI_NS1.4_v0` y `modelo1`; o el listado
  está paginado o la fila apunta a un modelo inexistente.
- Azure OpenAI: deployments `gpt-4o-mini` (sirve gpt-4.1-mini) y `gpt-5-mini`.
- Datos de etiquetado de CU: `srbstgproapppdocai/documentai`, prefijo
  `labelingProjects/01ecc742-3215-4bf8-bdc2-ea7a7ef00fd1/train`. Los de DI están en un
  proyecto de Document Intelligence Studio sobre un blob de PRO, pendiente de localizar.

## Diseño

### 1. Definición común y resolución por entorno

**Alias de recurso en la fila.** Cada fila de `ModeloConfigs` declara en
`ConfiguracionJson` un `ResourceAlias` con uno de estos valores: `openai_primary`,
`cu_primary`, `cu_secondary`, `di`. Las filas quedan idénticas en los tres entornos,
incluidos los nombres de deployment, que ya coinciden en los tres Foundry.

**Mapa por entorno en App Settings.** `AI__Resources__<alias>__Endpoint` (los alias usan guion bajo porque los nombres de App Setting no admiten guion), cargado por el
pipeline desde variables por entorno, sin literales en el YAML. Correspondencia:

| Alias | DEV | PRE | PRO |
|---|---|---|---|
| `openai_primary` | `srbaisrv01devdocai` | `srbaisrv01predocai` | `upe48-mm2avmdm-swedencentral` |
| `cu_primary` | `srbaisrv01devdocai` | `srbaisrv01predocai` | `upe48-mm2avmdm-swedencentral` |
| `cu_secondary` | `srbaisrv02devdocai` | `srbaisrv02predocai` | `srbaisrv-westeurope` |
| `di` | `srbdidevdocai` | `srbdipredocai` | `srbdiprodocai` |

**Resolución en los loaders.** Los cuatro loaders de registro de modelos (clasificación,
extracción, layout, prompt) resuelven el endpoint al cargar la fila: si la fila trae
`Endpoint` explícito se respeta; si no, se toma del alias. Alias sin mapa produce un error
explícito al construir el registro, no en la primera llamada. El modo de autenticación y la
key siguen en la fila y en Key Vault como hoy. El seed `models.json` del repo se alinea con
el mismo esquema.

**Lo que no cambia.** Dar de alta un modelo, un analyzer, una tipología, un prompt o un
catálogo sigue siendo una fila por Admin API que referencia un alias existente. Solo un
recurso físico nuevo, evento raro, requiere un App Setting nuevo, sin despliegue de código.
El override de `Endpoint` en la fila se conserva para urgencias.

**Configuración como artefacto versionado.** `ModeloConfigs`, `PromptTemplates`,
`Tipologias`, `CatalogoTdn1` y `CatalogoTdn2` se exportan con
`scripts/database/replicate-config-data.ps1` a un fichero `.sql` idempotente por release,
guardado como artefacto de pipeline y referenciado desde la anotación del tag. Un hash de
las tablas activas se calcula en cada entorno y se compara con el del release. La deriva es
informativa: avisa de cambios hechos en caliente que hay que retroportar a DEV antes de la
siguiente promoción. No bloquea ni impide cambios por Admin en PRO.

### 2. Promoción de artefactos de IA

**Definición en el repositorio.** Carpeta `infra/ai/` (nombre definitivo en el plan) con:

- `analyzers/<analyzerId>.json`: export del analyzer sin campos de solo lectura
  (`createdAt`, `lastModifiedAt`, `status`, `warnings`). Incluye `baseAnalyzerId`,
  `config`, `fieldSchema`, `knowledgeSources` y `models`.
- `datasets/<analyzerId>@<version>.<env>.manifest.json`: contenedor, prefijo, fecha de corte,
  número de ficheros y hash de cada uno. Uno por versión de analyzer y entorno destino
  (el mismo dataset copiado a DEV y a PRE deja dos manifiestos), y otro por proyecto
  de etiquetado de DI.
- `di-artifacts.json`: clasificadores y modelos custom de DI a promocionar, con
  identificador, recurso origen y tipos de documento.
- `deployments.<env>.json`: nombre, modelo, versión, SKU y capacidad de cada deployment.
- `resources.<env>.json`: el mapa alias a endpoint, fuente de los App Settings.

Solo entran artefactos referenciados por filas activas de `ModeloConfigs`. El resto del
Foundry de PRO se inventaría en un fichero aparte para limpieza futura.

**Datasets por entorno.** El contenedor de etiquetado de CU y el proyecto de DI Studio se
copian al storage de cada entorno bajo un prefijo inmutable por versión
(`labeling/<analyzerId>@<version>/`). La identidad del Foundry de cada entorno recibe
*Storage Blob Data Reader* sobre su propio storage. No se conceden lecturas entre
suscripciones.

**Content Understanding: reconstrucción.** `scripts/deployment/recreate-cu-analyzer.ps1`
se parametriza por entorno y pasa a leer del export del repo y del dataset del entorno, con
`PUT create-or-replace` y sondeo hasta `ready`. Tras cada build, un juego fijo de
documentos por analyzer se procesa en origen y destino y se exige un acuerdo de campos igual
o superior al `MinFieldsRatio` de la fila. La Copy API por token (`getCopyAuthorization`
en origen, `:copy` en destino) se prueba como spike aparte; si funciona será un atajo
operativo, no el mecanismo de promoción.

> **Enmienda 2026-09-21 (Tarea 13, AB#100315).** La validación post-build demostró que
> la reconstrucción no reproduce PRO: el dataset actual de los `labelingProjects` de PRO
> no es el que entrenó los analyzers de PRO (etiquetas de `CERA44_vado` y `CERA46`
> vaciadas el 2026-07-17, las de `CERA16_v1` reescritas el 2026-08-20, las de `CU_NS`
> modificadas el 2026-07-16; los analyzers de PRO son anteriores salvo
> `CU_NS_1.6_0_GGAA`). Acuerdo de campos PRO/DEV reconstruido 47-85 % frente a una línea
> base PRO/PRO de 86-91 %. El storage `srbstgproapppdocai` no tiene versionado, así que
> el estado de etiquetas que entrenó PRO no es recuperable en general.
>
> **Decisión:** el mecanismo de promoción de analyzers de CU pasa a ser la **Copy API
> oficial** (`:grantCopyAuthorization` en origen con `targetAzureResourceId` +
> `targetRegion`, `:copy` en destino con `sourceAzureResourceId` + `sourceAnalyzerId` +
> `sourceRegion`, `api-version 2025-11-01`; admite distinta suscripción y región; exige
> Cognitive Services User a la identidad que ejecuta en ambos recursos, sin roles
> cruzados entre recursos). Probada en DEV con `CERA46` → `CERA46_copytest`: copia en 9 s,
> definición idéntica, acuerdo con PRO 90,0 % global y 95,6 % en `extract`.
> `scripts/ai/copy-cu-analyzers.ps1` la implementa; `infra/ai/cu-analyzers.json` fija el
> alcance: los **24 analyzers con nombre de negocio** de PRO (incluidas versiones
> `_v1`/`_v2`), no solo los 6 referenciados por `ModeloConfigs`; los `projectAnalyzer_*`
> internos de Studio quedan fuera. La reconstrucción (`build-analyzers.ps1`) y los
> datasets versionados se conservan para reentrenar con identificador nuevo, no para
> promocionar. La validación de la Tarea 13 (`validate-analyzer.ps1`) sigue siendo la
> puerta, con la línea base `-SelfCheck` como referencia del umbral.

**Document Intelligence: copia exacta.** Copy API oficial para `DocumentAICC_v1` y
`DI_NS_1.4_v1`: `authorizeCopy` en el recurso destino, `copyTo` desde el origen, sondeo
de la operación. Regiones soportadas incluyen West Europe. El resultado es idéntico al
origen. Un reentrenamiento futuro parte del dataset versionado y crea un identificador
nuevo con sufijo.

**Deployments y cuota.** DEV y PRE ya tienen los deployments de PRO con los mismos nombres
y versiones. Falta `gpt-5-mini` en `DataZoneStandard`, que requiere cuota DataZone en
*Core Desarrollo* y *Core Preproducción*. La lista declarativa se aplica con un script
idempotente sobre `az cognitiveservices account deployment`. Ningún nombre cambia.

**Regla de versionado.** En PRE y PRO nunca se sobrescribe un identificador existente. Una
versión nueva es identificador nuevo, fila nueva en `ModeloConfigs` y cambio de la
tipología por datos, con vuelta atrás por datos.

### 3. Pipeline, puertas, identidades, fases y vuelta atrás

**Pipeline.** Dos etapas nuevas, ambas parametrizadas por entorno:

- `ai-artifacts`: aplica `deployments.<env>.json`, copia clasificadores DI, reconstruye
  analyzers CU desde el repo y el dataset del entorno, y ejecuta la validación post-build.
  Idempotente: un artefacto ya presente con la misma definición no se reconstruye.
- `config-seed`: aplica el `.sql` de configuración del release y calcula el hash de
  deriva. Tiene las mismas restricciones de alcance a SQL que `azure-pipelines-migrations.yml`:
  donde el pipeline no llega, se ejecuta desde el mismo mecanismo que hoy usan las
  migraciones en ese entorno.

Las variables de endpoint por entorno sustituyen a los literales de `azure-pipelines.yml`.

**Puertas por entorno.** Antes de dar un entorno por promocionado:

1. Smoke E2E post-despliegue (`tests/e2e-postdeploy`, perfil `smoke`) en verde.
2. Golden del harness de evaluación (repo Batch) contra el entorno, con comparación
   estadística frente a la línea base del entorno origen; sin regresión significativa.
3. Coste: el grupo de recursos del entorno factura solo su propio consumo.
4. Deriva cero entre el export del release y las tablas activas.

**Identidades y secretos.** La identidad de cada Function recibe *Cognitive Services User*
y *Foundry User* sobre los tres recursos de IA de su entorno (en DEV falta el Foundry 02).
Las keys de los recursos locales entran en el Key Vault del entorno con los nombres de
secreto actuales, para que ambos modos de autenticación funcionen. Los roles de las
identidades de DEV y PRE sobre los recursos de PRO se retiran en la última fase. La
identidad del Foundry de cada entorno recibe lectura sobre el storage de etiquetado de su
entorno.

**Fases.**

0. *Definición y alias*, en una rama desde `develop` posterior al tag
   `release-2026-09-15`: inventario, carpeta `infra/ai/`, alias en loaders y seed, export
   con hash, variables por entorno en el pipeline, spike de la Copy API de CU. Sin cambio
   de comportamiento en ningún entorno.
1. *DEV con IA propia*: datasets copiados, cuota y deployment de gpt-5-mini, roles de la
   identidad, keys en Key Vault, analyzers reconstruidos, clasificadores copiados,
   App Settings de alias, cambio de filas a alias, puertas 1 a 4.
2. *PRE como puerta técnica*: misma receta que DEV. Runbook de release actualizado: PRE
   recibe artefacto de código, `.sql` de configuración y artefactos de IA por pipeline.
3. *PRO*: solo alias en filas y App Settings apuntando a los recursos actuales; retirada
   de los roles de DEV y PRE sobre PRO.

**Vuelta atrás.** En cualquier fase, el mapa de alias vuelve a apuntar a los recursos de
PRO mientras el acceso compartido siga abierto, que es hasta el cierre de la fase 3. Los
artefactos construidos en DEV y PRE no afectan a PRO.

**Responsables.** Cada tarea del plan lleva ejecutor: proyecto (código, scripts, datos,
App Settings, keys en Key Vault, builds de analyzers y copias de clasificadores con
Contributor sobre el grupo de recursos) o plataforma (cuota, deployments si el permiso no
alcanza, asignaciones de rol, red). Las tareas de plataforma se validan con ellos antes de
arrancar la fase 1.

## Riesgos

| Riesgo | Mitigación |
|---|---|
| Analyzers de las cuentas Foundry 01 de DEV y PRE no inventariados (sin rol de lectura) | Tarea de inventario con rol *Cognitive Services User* temporal o listado por plataforma antes de la fase 1 |
| La identidad de DEV tiene roles sobre PRO no visibles desde la suscripción de DEV | Inventario de asignaciones desde la suscripción de PRO antes de la fase 3 |
| Cuota DataZone para gpt-5-mini no concedida a tiempo | DEV arranca con gpt-4.1-mini local; gpt-5-mini sigue en PRO hasta que llegue la cuota |
| Reconstrucción de analyzer con comportamiento distinto | Validación post-build con juego fijo y umbral; golden antes de dar el entorno por bueno |
| Dataset de DI Studio no localizado | La promoción de DI no lo necesita (copia exacta); bloquea solo reentrenamientos |
| Acceso de red: DI y Foundry de DEV con acceso público; PRO con private endpoints | Comprobar resolución DNS desde la Function de cada entorno en la fase 1, dado el precedente de fallos de DNS a privatelink en DEV |
| Latencia y cuota distintas entre West Europe y Sweden Central | Equivalencia se mide por calidad y acuerdo de campos, no por latencia |
| Cambios en caliente en PRO no retroportados | Hash de deriva por entorno y revisión en cada release |

## Pendientes que resuelve el plan

- Nombre definitivo de la carpeta de definición y del artefacto `.sql` de configuración.
- Resultado del spike de la Copy API de CU con flujo de token.
- Localización del proyecto de DI Studio y su contenedor.
- Confirmación con plataforma de quién ejecuta cuota, deployments y roles.

## Referencias

- `docs/infraestructura/INFRAESTRUCTURA_REAL_DESPLEGADA.md`
- `docs/guias/GUIA_EXTRACCION_AZURE_CONTENT_UNDERSTANDING.md` §12
- `scripts/deployment/recreate-cu-analyzer.ps1`, `scripts/database/replicate-config-data.ps1`
- `scripts/arm/analyzer-CU_NS_1.5_0-export.json`
- Documentación oficial: Content Understanding disaster recovery y copy analyzers;
  Document Intelligence custom classifier copy; Azure OpenAI quota por suscripción y región;
  calendario de retirada de modelos (gpt-4.1-mini: 2027-04-14).
