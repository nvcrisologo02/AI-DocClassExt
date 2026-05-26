# 11 — Inventario y auditoría de Work Items (Azure DevOps)

> **Fecha de generación:** 2026-05-01 (Europe/Madrid)
> **Modo:** SOLO LECTURA. No se ha creado, modificado, comentado, vinculado ni transicionado ningún work item.
> **Organización:** https://sareb.visualstudio.com
> **Proyecto:** AI DocClassExt
> **Fuentes:** Azure DevOps REST API vía MCP (`list_work_items` + WIQL, `get_work_item`), `git log --all --since="90 days ago"`, `docs/07_ROADMAP_PENDIENTES.md`.

> **Addendum 2026-05-26 (limpieza backlog EP7):**
> - El Epic `98519` (EP7) y su árbol completo (`98520`, `98524`, `98529`, `98534` + tasks hijas) se han transicionado a `Removed` en ADO por decisión de producto.
> - Desde esta fecha, EP7 deja de computar como abierto, bloqueante o pendiente del MVP.

> ⚠️ **Limitación documentada de la API utilizada.** Las consultas WIQL ejecutadas con `mcp_azuredevops_list_work_items` solo devuelven los campos `System.Id`, `System.State`, `System.Title` aunque la cláusula `SELECT` incluya otros (AreaPath, IterationPath, AssignedTo, Tags, ChangedDate, Priority, Severity). Para los work items abiertos críticos se ha completado la información con `mcp_azuredevops_get_work_item` (8 fetches detallados). Donde un campo no aparece para un work item, se indica `n/d` (no disponible en la respuesta API). La herramienta `search_work_items` no se ha podido usar en esta ejecución (fallo de certificado `unable to verify the first certificate`).

> 🔎 **Hallazgos relevantes constatados durante la recolección:**
> - Todos los work items leídos pertenecen a **AreaPath = `AI DocClassExt`** e **IterationPath = `AI DocClassExt`** (proyecto sin estructura de áreas/iteraciones jerarquizada).
> - Todos los work items leídos tienen **AssignedTo = null** (proyecto trabajado mayoritariamente en modo no asignado por el creador `Ignacio Varas Crisologo`).
> - El proyecto utiliza el tipo **Product Backlog Item (PBI)** como equivalente a "User Story / HU".

---

## 1. Resumen ejecutivo

### 1.1 Recuento por tipo y estado (proyecto entero)

| Tipo                     | Total | New | Approved | Committed | To Do | In Progress | To Validate | Pdte. Despliegue | Done | Removed |
|--------------------------|------:|----:|---------:|----------:|------:|------------:|-------------:|-----------------:|-----:|--------:|
| Epic                     |    15 |   2 |        0 |         0 |     — |           1 |            0 |                0 |   11 |       1 |
| Feature                  |    52 |   8 |        0 |         0 |     — |           5 |            0 |                0 |   39 |       0 |
| Product Backlog Item     |    58 |   5 |        0 |        25 |     — |           0 |            1 |                0 |   27 |       0 |
| Bug                      |     3 |   0 |        0 |         0 |     — |           0 |            0 |                0 |    3 |       0 |
| Task                     |  ~264 |   — |        — |         — |    47 |           7 |            — |                — |  ~205|      ~5 |

> El recuento de Tasks es aproximado: la consulta WIQL `Task` excede el límite duro de 200 ítems del MCP (`VS403474`). Se ha paginado por `ChangedDate >= 2026-04-01` (84 tareas) y por `[State] IN ('To Do','In Progress')` (54 tareas). Para inventario exhaustivo de Tasks ejecutar la WIQL del Apéndice 9.7 con paginación adicional.

### 1.2 Concentración por área / iteración / asignación

| Atributo            | Valor                                | % cobertura observada |
|---------------------|--------------------------------------|-----------------------|
| AreaPath            | `AI DocClassExt` (única)             | 100 %                 |
| IterationPath       | `AI DocClassExt` (única)             | 100 %                 |
| AssignedTo          | `null` (sin asignación)              | 100 % en muestra leída|
| CreatedBy / ChangedBy | `Ignacio Varas Crisologo`          | 100 % en muestra leída|

> No procede ranking de top 5 por área/iteración/asignados: el proyecto trabaja con un único nodo de área e iteración y los WI no tienen asignación nominal en los campos consultados.

### 1.3 Recuento por estado y tipo (cabeceras)

- **Epics abiertos (no Done/Removed):** 2 → `98692 EP9`, `99089 EP10`.
- **Features abiertas:** 10 → bloque EP9 (4 en `New`), bloque EP6 (`98379`, `98408`, `99065`, `99078` en `In Progress`), `98359 F5.2` (In Progress), `99091 AR-F1` (In Progress).
- **PBI/HU abiertos (Committed/New/To Validate):** 31 → la mayoría son HU del MVP base (98382-98454) que el roadmap declara funcionalmente cumplidas pero permanecen en `Committed` en ADO; 5 HU EP9 (`98697-98701`) en `New`; 1 HU `98871` en `To Validate`.
- **Bugs abiertos:** **0**. Los 3 bugs históricos del proyecto están `Done` (`98918`, `98947`, `98948`).
- **Tasks abiertas:** 33 (`To Do` + `In Progress`), concentradas en EP9 Blob y EP6 Observabilidad fase final (EP7 retirado).

---

## 2. Epics y árbol de Features hijas

> **Fuente:** WIQL `WorkItemType = 'Epic'` (15 ítems totales) + WIQL `WorkItemType = 'Feature'` agrupado heurísticamente por código `EP/F` en el título (la API no ha devuelto `System.Parent` en los listados).

| Epic ID | Título                                            | State        | Tags                              | Notas                                  |
|--------:|---------------------------------------------------|--------------|-----------------------------------|----------------------------------------|
|  98361  | EP 1 – Ingesta y orquestación de documentos       | Done         | DocumentIA; EP1                   |                                        |
|  98368  | EP 3 – Validación y motor de reglas               | Done         | n/d                               |                                        |
|  98375  | EP 5 – Configuración y tipologías                 | Done         | n/d                               | Cierre formalizado por `99162`         |
|  98392  | EP 2 – Clasificación y extracción de contenido    | Done         | n/d                               |                                        |
|  98399  | EP 4 – Persistencia y auditoría                   | Done         | n/d                               |                                        |
|  98406  | EP 6 – Observabilidad y pruebas                   | Done         | n/d                               | Sub-features 99065/99078 aún In Progress |
|  98519  | EP 7 - Proteccion de Datos y GDPR                 | **Removed**  | DocumentIA; EP7; GDPR; Security   | Descartado por decisión de producto (2026-05-26) |
|  98628  | EP 8 - Sistema de Plugins de Integracion          | Done         | n/d                               |                                        |
|  98692  | EP 9 – Mantenimiento y limpieza de Blob Storage   | **New**      | Blob; DocumentIA; EP9; Mantenimiento; Retencion | Effort: 21. Priority: 2 |
|  99063  | Obtener Activo desde documento (AssetResolver)    | Done         | n/d                               | Epic "técnico" (precursor de EP10)     |
|  99089  | EP10 - Resolucion de Activo por Direccion en AssetResolver | **In Progress** | n/d                       | Bloqueado parcialmente por Tanda C     |
|  99090  | EP10 - Resolucion de Activo por Direccion en AssetResolver | Removed   | n/d                               | Duplicado retirado, se conserva 99089  |
|  99122  | [PROMPTS] Soporte de prompt ad hoc por petición   | Done         | n/d                               | Tipo Epic en taxonomía interna         |
|  99179  | [ADMIN TIPOLOGIAS] Wizard de Alta de Tipologías Admin | Done     | n/d                               |                                        |
|  99229  | [DOCUMENTIA.BATCH] Aplicacion Windows portable …  | Done         | n/d                               | MVP cerrado                            |

### 2.1 Features hijas por Epic (mapeo heurístico por título)

> Se detalla el mapeo Epic→Feature inferido por convención de código (`EP1`→`F 1.x`, `EP6`→`F 6.x` / `F6.x`, `EP7`→`F 7.x`, `EP8`→`F 8.x`, `EP9`→`F 9.x`, etc.). Esta relación NO está vinculada explícitamente en `System.Parent` en los datos devueltos por la API.

#### EP 1 — Ingesta y orquestación (Done)
| Feature ID | Título | State |
|---:|---|---|
| 98362 | F 1.1 – API de Ingesta de documentos | Done |
| 98391 | F 1.2 – Pipeline de procesamiento (Durable Functions) | Done |

#### EP 2 — Clasificación y extracción (Done)
| Feature ID | Título | State |
|---:|---|---|
| 98365 | F 2.1 – Clasificación de tipología (mock) | Done |
| 98394 | F 2.2 – Extracción de datos de tasación (mock) | Done |
| 98458 | F 2.3 – Clasificación con Azure AI Document Intelligence | Done |
| 98464 | F 2.4 – Extracción con Azure AI Document Intelligence | Done |

#### EP 3 — Validación y motor de reglas (Done)
| Feature ID | Título | State |
|---:|---|---|
| 98397 | F 3.1 – Validaciones básicas (mock) | Done |
| 98358 | F 3.2 – Motor de reglas configurable por tipología | Done |

#### EP 4 — Persistencia y auditoría (Done)
| Feature ID | Título | State |
|---:|---|---|
| 98400 | F 4.1 – Modelo de datos y EF Core | Done |
| 98373 | F 4.2 – Persistencia de resultados | Done |
| 98402 | F 4.3 – Almacenamiento en Blob Storage | Done |
| 98917 | F 4.x – Calculadora de confianza agregada | Done |

#### EP 5 — Configuración y tipologías (Done; cierre por 99162)
| Feature ID | Título | State |
|---:|---|---|
| 98404 | F 5.1 – Tipología Nota Simple v1.0 | Done |
| 98359 | F 5.2 – Soporte multi-tipología basado en configuración | **In Progress** |
| 98904 | F 5.3 – Sección Prompt por tipología y registro de modelos | Done |
| 98845 | F 5.x – Versionado de tipologías por familia con versión por defecto | Done |
| 98931 | Config. dinámica de tipologías y modelos sin reinicio de Functions | Done |
| 98995 | Integración JSONEditor para edición visual de configuración | Done |
| 98996 | Nueva página Consulta de Configuración del Sistema | Done |
| 99162 | EP5 Configuración y tipologías - Cierre 100 % | Done |

#### EP 6 — Observabilidad y pruebas (Done a nivel Epic; 4 features aún abiertas)
| Feature ID | Título | State |
|---:|---|---|
| 98379 | F 6.1 – Logging y telemetría | **In Progress** |
| 98408 | F 6.2 – Pruebas end-to-end (scripts y REST Client) | **In Progress** |
| 98360 | F 6.3 – Tests unitarios y de integración | Done |
| 99065 | F6.1 – App Insights Portal + Durable Functions Monitor | **In Progress** |
| 99073 | F6.2 – Monitor Dashboard Blazor | Done |
| 99078 | F6.3 – TelemetryClient custom events + Workbook + Alertas | **In Progress** |

#### EP 7 — Protección de datos y GDPR (Removed)
| Feature ID | Título | State |
|---:|---|---|
| 98520 | F 7.1 - Sistema de clasificacion de sensibilidad de datos | **Removed** |
| 98524 | F 7.2 - Encriptacion de campos sensibles en BD | **Removed** |
| 98529 | F 7.3 - Sistema de triggers de retencion de datos | **Removed** |
| 98534 | F 7.4 - Anonimizacion y masking de datos | **Removed** |

#### EP 8 — Sistema de plugins de integración (Done)
| Feature ID | Título | State |
|---:|---|---|
| 98634 | F 8.1 - Arquitectura base del sistema de plugins | Done |
| 98635 | F 8.2 - Plugin REST generico configurable | Done |
| 98636 | F 8.3 - Plugins especificos Atlas, Catastro y GDC | Done |
| 98637 | F 8.4 - Resiliencia y observabilidad de plugins | Done |

#### EP 9 — Mantenimiento y limpieza de Blob Storage (New)
| Feature ID | Título | State |
|---:|---|---|
| 98693 | F 9.1 – Política de retención configurable por tipología | **New** |
| 98694 | F 9.2 – Motor de limpieza automática de blobs expirados | **New** |
| 98695 | F 9.3 – Inventario y reporting de ocupación | **New** |
| 98696 | F 9.4 – Observabilidad y auditoría de operaciones de limpieza | **New** |

#### EP 10 / 99063 — AssetResolver (parcial Done; AR-F1 abierta)
| Feature ID | Título | State |
|---:|---|---|
| 99091 | AR-F1 - Busqueda avanzada por direccion y normalizacion | **In Progress** |
| 99134 | Búsqueda doble origen AAII / AACC en plugin | Done |
| 99141 | EP-X: Entrada por ObjectId de GDC con pre-deduplicación por hash | Done |

#### Otras Features sin Epic explícito en taxonomía
| Feature ID | Título | State |
|---:|---|---|
| 98968 | Desktop: botón selección + desplegable tipologías publicadas | Done |
| 99018 | Normalizar y persistir Markdown | Done |
| 99180-99184 | Sub-features Wizard tipologías (99179) | Done |
| 99224 | feat: Healthcheck endpoint POST /api/healthcheck | Done |
| 99230 | DocumentIA.Batch flujo end-to-end MVP | Done |
| 99251 | [HEALTHCHECK] Mejorar healthcheck con monitorización de componentes | Done |

---

## 3. Features abiertas (estado ≠ Done / Removed)

> Total: **10** Features abiertas. AreaPath/IterationPath = `AI DocClassExt`. AssignedTo = null. ChangedDate y Tags no devueltos por la WIQL; consultar `get_work_item` para detalle (ejemplo en 99251 ya validado).

| ID    | Título                                                                       | State        | Epic ascendente (heurístico) |
|------:|------------------------------------------------------------------------------|--------------|------------------------------|
| 98359 | F 5.2 – Soporte multi-tipología basado en configuración                      | In Progress  | EP 5                         |
| 98379 | F 6.1 – Logging y telemetría                                                  | In Progress  | EP 6                         |
| 98408 | F 6.2 – Pruebas end-to-end                                                    | In Progress  | EP 6                         |
| 98693 | F 9.1 – Política de retención configurable por tipología                      | New          | EP 9                         |
| 98694 | F 9.2 – Motor de limpieza automática de blobs expirados                       | New          | EP 9                         |
| 98695 | F 9.3 – Inventario y reporting de ocupación                                   | New          | EP 9                         |
| 98696 | F 9.4 – Observabilidad y auditoría de operaciones de limpieza                 | New          | EP 9                         |
| 99065 | F6.1 – App Insights Portal + Durable Functions Monitor                        | In Progress  | EP 6                         |
| 99078 | F6.3 – TelemetryClient custom events + Workbook + Alertas                     | In Progress  | EP 6                         |
| 99091 | AR-F1 - Busqueda avanzada por direccion y normalizacion                       | In Progress  | EP10                         |

---

## 4. PBI / Bugs abiertos

### 4.1 PBI / HU abiertos (state ∉ {Done, Removed})

> Total: **31** PBI abiertos. Campo Tags no devuelto por la WIQL.

| ID    | Título                                                                                          | State         |
|------:|-------------------------------------------------------------------------------------------------|---------------|
| 98382 | HU 1 – Ingestar un documento y obtener un resultado                                             | Committed     |
| 98384 | HU 3 – Persistir resultados de procesamiento                                                    | Committed     |
| 98386 | HU 5 – Clasificar documentos de tasación (mock)                                                 | Committed     |
| 98388 | HU 7 – Validar datos extraídos                                                                  | Committed     |
| 98414 | HU 2 – Evitar reprocesar documentos duplicados                                                  | Committed     |
| 98422 | HU 4 – Guardar el binario del documento en almacenamiento                                       | Committed     |
| 98430 | HU 6 – Extraer datos clave de una Nota Simple (mock)                                            | Committed     |
| 98437 | HU 8 – Definir reglas de validación por tipología                                               | Committed     |
| 98443 | HU 9 – Configurar tipologías adicionales mediante configuración                                 | Committed     |
| 98449 | HU 10 – Disponer de tests unitarios para la lógica de negocio                                   | Committed     |
| 98454 | HU 11 – Tests de integración del pipeline completo                                              | Committed     |
| 98629 | HU 8.1 - Usar plugins de integracion para conectar con sistemas externos                        | Committed     |
| 98630 | HU 8.2 - Enriquecer datos de tasacion con informacion del Catastro                              | Committed     |
| 98631 | HU 8.3 - Enriquecer expedientes con informacion de Sareb                                        | Committed     |
| 98632 | HU 8.4 - Dar de alta documentos en el gestor documental GDC/OpenText                            | Committed     |
| 98633 | HU 8.5 - Gestionar errores de integracion sin bloquear el procesamiento                         | Committed     |
| 98697 | HU 9.1 – Definir política de retención por tipología                                            | New           |
| 98698 | HU 9.2 – Ejecutar limpieza automática de blobs expirados                                        | New           |
| 98699 | HU 9.3 – Consultar inventario y ocupación del Blob Storage                                      | New           |
| 98700 | HU 9.4 – Auditar operaciones de borrado de blobs                                                | New           |
| 98701 | HU 9.5 – Proteger blobs con hold activo frente a limpieza                                       | New           |
| 98846 | HU TV6 – Validar el versionado de tipologías con pruebas                                        | Committed     |
| 98847 | HU TV4 – Integrar la resolución de versión en el orquestador                                    | Committed     |
| 98848 | HU TV1 – Preparar backlog y rama para tipología versioning                                      | Committed     |
| 98849 | HU TV5 – Exponer familia y versión en el contrato de salida                                     | Committed     |
| 98850 | HU TV2 – Gestionar tipologías versionadas en configuración JSON                                 | Committed     |
| 98851 | HU TV3 – Resolver tipologías por familia y versión explícita                                    | Committed     |
| 98871 | HU 8.5 - Integracion endpoint GDC DEV real                                                      | **To Validate** |

### 4.2 Bugs abiertos

| ID | Título | State | Severity |
|---:|---|---|---|
| — | — | — | — |

> **Cero bugs abiertos.** Los tres bugs registrados (`98918`, `98947`, `98948`) están `Done`. Severity no devuelto por la WIQL.

---

## 5. Cambios recientes (últimos 30 días: 2026-04-01 → 2026-05-01)

### 5.1 Work items con `ChangedDate >= 2026-04-01`

> ⚠️ La WIQL no devuelve `ChangedDate` en la respuesta. Esta lista se construye combinando: (a) tareas creadas/modificadas a partir del rev>=3 dentro del listado MCP, y (b) work items que aparecen como AB# en commits de los últimos 30 días.

#### 5.1.1 Tasks/Features/PBI con actividad en abril 2026 (muestra)

| ID    | Tipo    | Título                                                                                       | State        |
|------:|---------|----------------------------------------------------------------------------------------------|--------------|
| 99224 | Feature | feat: Healthcheck endpoint POST /api/healthcheck                                             | Done         |
| 99225 | Task    | Escribir test unitario para HealthcheckFunction                                              | Done         |
| 99226 | Task    | Definir contrato y ubicación del endpoint healthcheck                                        | Done         |
| 99228 | Task    | Implementar HealthcheckFunction Azure Function HTTP                                          | Done         |
| 99229 | Epic    | [DOCUMENTIA.BATCH] Aplicacion Windows portable …                                             | Done         |
| 99230 | Feature | [DOCUMENTIA.BATCH.MVP] Flujo end-to-end                                                      | Done         |
| 99231-99236 | PBI | HU operador batch (drag&drop, configuracion, exportacion, modal resumen, trazabilidad)       | Done         |
| 99237 | Task    | Integrar editor de prompts y toggle de prompting on/off                                      | Done         |
| 99238-99249 | Task | Tareas DocumentIA.Batch (UI, scheduler, exportadores, KPIs, reintento, trazabilidad)         | Done         |
| 99251 | Feature | [HEALTHCHECK] Mejorar healthcheck con monitorización de componentes del sistema              | Done         |
| 99252-99255 | Task | Sub-tareas SystemHealthService + tests + DI                                                  | Done         |
| 99162 | Feature | EP5 Configuración y tipologías - Cierre 100 %                                                | Done         |
| 99179 | Epic    | [ADMIN TIPOLOGIAS] Wizard de Alta de Tipologías Admin                                        | Done         |
| 99180-99205 | varios | Sub-features y tareas wizard de tipologías                                                  | Done         |
| 99122-99129 | varios | [PROMPTS] Prompt ad hoc por petición + 7 tareas asociadas                                   | Done         |
| 99141-99149 | varios | EP-X ObjectId GDC + 8 user stories                                                          | Done         |
| 99160 | Task    | fix(functions): mostrar Skipped en GDC cuando SkipGDCUpload activo                           | Done         |
| 99089 | Epic    | EP10 - Resolucion de Activo por Direccion en AssetResolver                                   | In Progress  |
| 99091 | Feature | AR-F1 - Busqueda avanzada por direccion y normalizacion                                      | In Progress  |
| 99101 | Task    | AR-10 - Evaluacion de rendimiento y calidad de matching                                      | In Progress  |
| 99103 | Task    | AR-12 - Hardening operativo y telemetria                                                     | In Progress  |
| 99066-99070 | Task | Sub-tareas EP6 F6.1 (App Insights, KQL, Live Metrics, Durable Monitor)                       | In Progress  |
| 99082-99083 | Task | Workbook + Alert Rule de errores (EP6 F6.3)                                                  | To Do        |

### 5.2 Commits últimos 30 días (resumen — ver 6.2 para mapeo AB#)

```
2026-04-30  a3f40d2  feat(healthcheck,gdc,desktop): merge feature/healthcheck-components AB#99251 99252 99253 99254 99255
2026-04-30  9605ffc  feat(healthcheck,gdc,desktop): componentes healthcheck, fix GDC endpoint y mejoras frontend
2026-04-30  a41fb9e  feat: healthcheck mejorado con probes de componentes AB#99251 99252 99253 99254 99255
2026-04-30  d770101  fix(redeploy): corrige reutilizacion de duplicados y configura AssetResolver
2026-04-30  bf846e1  chore(assetresolver): actualiza secret Key Vault de AssetResolver
2026-04-29  3e545f9  docs(batch): documentar plan DocumentIA.Batch AB#99237
2026-04-29  cd082cb  feat: merge healthcheck endpoint to develop AB#99224
2026-04-29  a4ad7f4  feat: add healthcheck endpoint AB#99228
2026-04-29  f481714  test: add healthcheck function tests AB#99225
2026-04-29  4ede77a  chore: define healthcheck endpoint contract AB#99226
2026-04-28  d52e6a2  feat(COMPLETAR_GDC_HTTP_BASIC_USERNAME): wizard tipologias con validaciones reforzadas AB#99179
2026-04-24  9315d35  EP5: cierre funcional A-1/A-2/A-3 (audit, import-export, version diff+filtro)
2026-04-24  608d413  fix(functions): preservar seguimiento y mostrar Skipped en GDC AB#99141 99160
2026-04-24  065c564  docs: actualiza contrato y manuales para objectIdGDC AB#99141
2026-04-24  cfbe50d  feat(frontend): adapta desktop al nuevo contrato objectIdGDC AB#99141
2026-04-24  b690891  feat(functions): soporta objectIdGDC con pre-dedupe MD5 AB#99141
2026-04-23  41ac175  merge(assetresolver): integra feature/assetresolver-post-99122 AB#99122
2026-04-23  8706444  feat(assetresolver): habilita busqueda dual AAII/AACC AB#99122
2026-04-23  29e64a9  feat(assetresolver): formaliza precedencia y cubre input orquestador AB#99122
2026-04-21  c4f695a  docs+scripts: añade paquete de evidencia Tanda C para 99101/99103
2026-04-21  d39d13d  feat(assetresolver): trasladar cambios locales post-push AB#99122
2026-04-21  5e859c6  test(prompt-adhoc): add E2E PowerShell test script AB#99128
2026-04-21  89f3d21  docs(prompt-adhoc): document instrucciones.prompt fields AB#99127
2026-04-21  91d62c2  test(prompt-adhoc): add unit tests AB#99125
2026-04-21  cc911af  feat(prompt-adhoc): implement ResolvePromptConfig AB#99126
2026-04-21  1a04187  feat(prompt-adhoc): propagate PromptInstrucciones AB#99124
2026-04-21  5185564  feat(prompt-adhoc): add PromptInstrucciones contract and HTTP validator AB#99123 99129
2026-04-16  3ad00a1  Merge feature/ep6-telemetry-workbook-alertas (EP6 Fase C + Monitor Dashboard)
2026-04-15  bbe74ca  feat(EP6-C): TelemetryClient en PersistirActivity #99079 #99080
2026-04-15  ab04419  feat(ep6): add Monitor dashboard #99074 #99075 #99076 #99077
2026-04-15  3f9db33  docs(ep6): add monitoring/observability section #99071 #99072
2026-04-10  varios   pipeline azure-pipelines.yml + scripts/generate-config (sin AB#)
2026-04-09  8c0bc7b  merge: feature/99018-normalizar-markdown into develop (sin AB# pero referencia 99018)
2026-04-08  1722da0  feat: normalización markdown comprimido + fixes (sin AB#)
2026-04-08  0a7ac8e  Merge feature/modelos-clasificacion-bbdd (sin AB#)
2026-04-07  c968d67  feat: migración completa de proveedores IA a configuración desde BBDD (sin AB#)
```

> Total commits visibles en ventana 30d: ≈ 90. Commits con AB# referenciado: ≈ 30. Commits sin AB#: ≈ 60 (la mayoría son `chore`/`docs`/`fix pipeline` y merges de mantenimiento; ver Apartado 8).

---

## 6. Cruce con commits y referencias

### 6.1 Resultado del cruce (32 IDs solicitados)

> Todos los IDs solicitados existen en Azure DevOps. Para cada uno se reporta tipo, estado, padre heurístico y referencia AB# encontrada en `git log` (90 días).

| ID    | Tipo    | State        | Padre heurístico                 | Referencia AB# en commits (90d)                   |
|------:|---------|--------------|----------------------------------|---------------------------------------------------|
| 99071 | Task    | Done         | F6.1 Portal AppInsights (99065)  | `3f9db33` (#99071 #99072)                          |
| 99072 | Task    | Done         | F6.1 Portal AppInsights (99065)  | `3f9db33`                                          |
| 99074 | Task    | Done         | F6.2 Monitor Dashboard (99073)   | `ab04419` (#99074 #99075 #99076 #99077)            |
| 99075 | Task    | Done         | F6.2 Monitor Dashboard (99073)   | `ab04419`                                          |
| 99076 | Task    | Done         | F6.2 Monitor Dashboard (99073)   | `ab04419`                                          |
| 99077 | Task    | Done         | F6.2 Monitor Dashboard (99073)   | `ab04419`                                          |
| 99079 | Task    | Done         | F6.3 TelemetryClient (99078)     | `bbe74ca` (#99079 #99080)                          |
| 99080 | Task    | Done         | F6.3 TelemetryClient (99078)     | `bbe74ca`                                          |
| 99101 | Task    | **In Progress** | EP10 / AR-F1 (99089/99091)    | `c4f695a` (paquete evidencia Tanda C 99101/99103)  |
| 99103 | Task    | **In Progress** | EP10 / AR-F1 (99089/99091)    | `c4f695a`                                          |
| 99122 | Epic    | Done         | (raíz)                           | `41ac175 8706444 29e64a9 d39d13d` (AB#99122)       |
| 99123 | Task    | Done         | 99122 PROMPTS                    | `5185564` (AB#99123 99129)                         |
| 99124 | Task    | Done         | 99122 PROMPTS                    | `1a04187` (AB#99124)                               |
| 99125 | Task    | Done         | 99122 PROMPTS                    | `91d62c2` (AB#99125)                               |
| 99126 | Task    | Done         | 99122 PROMPTS                    | `cc911af` (AB#99126)                               |
| 99127 | Task    | Done         | 99122 PROMPTS                    | `89f3d21` (AB#99127)                               |
| 99128 | Task    | Done         | 99122 PROMPTS                    | `5e859c6` (AB#99128)                               |
| 99129 | Task    | Done         | 99122 PROMPTS                    | `5185564` (AB#99123 99129)                         |
| 99141 | Feature | Done         | EP-X ObjectId GDC (raíz)         | `30dd9c6 608d413 065c564 cfbe50d b690891` (AB#99141) |
| 99160 | Task    | Done         | 99141 ObjectId GDC               | `30dd9c6 608d413` (AB#99160)                       |
| 99179 | Epic    | Done         | (raíz)                           | `d52e6a2` (AB#99179)                               |
| 99224 | Feature | Done         | (raíz)                           | `cd082cb` (AB#99224)                               |
| 99225 | Task    | Done         | 99224 Healthcheck                | `f481714` (AB#99225)                               |
| 99226 | Task    | Done         | 99224 Healthcheck                | `4ede77a` (AB#99226)                               |
| 99228 | Task    | Done         | 99224 Healthcheck                | `a4ad7f4` (AB#99228)                               |
| 99237 | Task    | Done         | 99229 DocumentIA.Batch           | `3e545f9` (AB#99237)                               |
| 99251 | Feature | Done         | (raíz)                           | `a3f40d2 a41fb9e` (AB#99251)                       |
| 99252 | Task    | Done         | 99251 Healthcheck componentes    | `a3f40d2 a41fb9e` (AB#99252)                       |
| 99253 | Task    | Done         | 99251 Healthcheck componentes    | `a3f40d2 a41fb9e` (AB#99253)                       |
| 99254 | Task    | Done         | 99251 Healthcheck componentes    | `a3f40d2 a41fb9e` (AB#99254)                       |
| 99255 | Task    | Done         | 99251 Healthcheck componentes    | `a3f40d2 a41fb9e` (AB#99255)                       |

### 6.2 Resumen de referencias AB# extraídas del historial git (90d)

IDs únicos encontrados como AB#: **99018**, **99071**, **99072**, **99074**, **99075**, **99076**, **99077**, **99079**, **99080**, **99122**, **99123**, **99124**, **99125**, **99126**, **99127**, **99128**, **99129**, **99141**, **99160**, **99179**, **99224**, **99225**, **99226**, **99228**, **99237**, **99251**, **99252**, **99253**, **99254**, **99255**.

Cobertura del cruce solicitado: **30/32** con commit AB# directo (93,75 %). Los 2 IDs solicitados sin commit AB# directo (99101, 99103) sí aparecen referenciados textualmente en `c4f695a` ("paquete de evidencia Tanda C para 99101/99103") aunque no como AB#.

---

## 7. Work items bloqueantes / críticos

| ID    | Tipo    | Título                                                                       | State         | Estado / Bloqueo                                                          |
|------:|---------|------------------------------------------------------------------------------|---------------|---------------------------------------------------------------------------|
| 99101 | Task    | AR-10 - Evaluacion de rendimiento y calidad de matching                      | In Progress   | **Tanda C abierta**: pendiente p95/p99 + métricas precision/recall.       |
| 99103 | Task    | AR-12 - Hardening operativo y telemetria                                     | In Progress   | **Tanda C abierta**: pendiente telemetría matching + alertas.             |
| 99089 | Epic    | EP10 - Resolucion de Activo por Direccion en AssetResolver                   | In Progress   | Bloqueado parcialmente por 99101/99103 (cierre Tanda C).                  |
| 99091 | Feature | AR-F1 - Busqueda avanzada por direccion y normalizacion                      | In Progress   | Bloqueado parcialmente por 99101/99103.                                   |
| 98692 | Epic    | EP 9 – Mantenimiento y limpieza de Blob Storage                              | New           | 80 % de avance reportado en roadmap, pero work items en `New` en ADO.     |
| 98693-98696 | Feature | F 9.1 / F 9.2 / F 9.3 / F 9.4 (EP9)                                       | New           | Backlog completo (98697-98716) en `To Do`. Necesario antes de prod.       |
| 98379 | Feature | F 6.1 – Logging y telemetría                                                  | In Progress   | Coexiste con 99065/99078 (mismo dominio); riesgo de duplicidad.           |
| 98408 | Feature | F 6.2 – Pruebas end-to-end (scripts y REST Client)                            | In Progress   | Coexiste con 99073 (Monitor Dashboard); revisar solapamiento.             |
| 98871 | PBI     | HU 8.5 - Integracion endpoint GDC DEV real                                   | To Validate   | Pendiente verificación funcional contra entorno DEV real.                 |
| 99066-99070 | Task | Sub-tareas EP6 F6.1 (App Insights/KQL/Durable Monitor)                       | In Progress   | Bloquean cierre formal de F6.1 (99065) y por tanto EP6 al 100 %.          |
| 99082, 99083 | Task | Workbook 4 tiles + Alert Rule errores >10 %                                | To Do         | Bloquean cierre F6.3 (99078) y entrega EP6 fase C.                        |

---

## 8. Hallazgos

### 8.1 Work items cerrados sin commit AB# detectable (90 días)

> Lista completa exigiría cruce contra 264 Tasks. Incluyo los detectados en la muestra (~80 tareas Done con `ChangedDate >= 2026-04-01`):

| ID    | Tipo    | Título                                                          | Observación                                            |
|------:|---------|-----------------------------------------------------------------|--------------------------------------------------------|
| 99018 | Feature | Normalizar y persistir Markdown                                 | Mencionado en branch `feature/99018-…` (8c0bc7b) pero sin AB#. |
| 99019-99025 | Task | Sub-tareas Markdown comprimido + EF migration                   | No AB#; cerrados con commits genéricos `1722da0`, `8c0bc7b`. |
| 98931 | Feature | Config. dinámica de tipologías sin reinicio                     | Sin AB#; agrupado en `c968d67` (feat genérico).         |
| 98995, 98996 | Feature | JSONEditor + Consulta Configuración                          | Sin AB#; commits `f55f40c`, `35eeb25`.                  |
| 98904 | Feature | F 5.3 – Sección Prompt por tipología                            | Sin AB#; commits envueltos en merge feature/modelos-clasificacion-bbdd. |
| 98917 | Feature | F 4.x – Calculadora de confianza agregada                       | Sin AB#; cerrado por commits genéricos abril.           |
| 99053-99062 | Task | AssetResolver IDUFIR/RefCat (precursor 99063)                   | Sin AB# en commits `3a04435`, `3d67e39`.                |
| 99092-99100, 99102 | Task | AR-01..AR-09 + AR-11 (AssetResolver dirección)                  | Sin AB# en commit `0430595` (Implementado AssetResolver Direccion Fuzzy y tipificada). |
| 99105-99115 | Task | Test Cases Removed (RUN:20260420-inicial)                       | Removidos por inválidos; sin commit asociado.           |
| 99135-99140, 99166-99177 | Task | Doble origen AAII/AACC + Wizard tipologías                | Sin AB# en commits genéricos.                           |
| 99192-99205 | Task | Sub-tareas wizard tipologías                                    | Cerradas en `d52e6a2` (sólo AB#99179, no por task).     |
| 99238-99249 | Task | Sub-tareas DocumentIA.Batch                                     | Cerradas con AB#99237 únicamente, no por task.          |

> **Recomendación operativa (no aplicada por ser SOLO LECTURA):** estandarizar plantilla de commit `tipo(scope): mensaje AB#<ID>` para cada task individual cerrada, no solo para la feature/epic raíz. Esto permitirá cruces 1:1 en futuras auditorías.

### 8.2 Commits AB# huérfanos (sin ID válido en ADO)

Tras inspeccionar todas las menciones `AB#\d+` en commits 90 días, **no se han detectado commits AB# huérfanos**: todos los IDs referenciados (99018, 99071-99080, 99122-99129, 99141, 99160, 99179, 99224-99228, 99237, 99251-99255) corresponden a work items existentes en el proyecto.

### 8.3 Discrepancias entre `docs/07_ROADMAP_PENDIENTES.md` y estado real en ADO

| Área              | Roadmap (07_ROADMAP_PENDIENTES.md)                      | ADO (estado real)                                          | Gap |
|-------------------|---------------------------------------------------------|-------------------------------------------------------------|-----|
| EP 5              | Marcado como "In Progress 80 %" en sección global, "Done" en bloque Tanda C | Epic 98375 = Done; cierre formalizado por 99162 = Done.    | Roadmap inconsistente consigo mismo; ADO indica Done definitivo. |
| EP 6              | "65 % en progreso"                                      | Epic 98406 = Done; pero F6.1 (99065), F6.3 (99078), F 6.1 (98379), F 6.2 (98408) **siguen In Progress**, y 99082/99083 en `To Do`. | Epic cerrado prematuramente; sub-features abiertas. |
| EP 9              | "80 % en progreso"                                      | Epic 98692 = `New` (sin transición a In Progress). 5 HU + 14 tasks en `To Do`. | Roadmap reporta avance que no se refleja en ADO. |
| EP 10             | "Done"                                                  | Epic 99089 = `In Progress`; 99101/99103 abiertos (Tanda C). | Roadmap optimista; ADO indica trabajo Tanda C pendiente. |
| EP 7 (GDPR)       | "Planned 0 %"                                           | Epic 98519 y árbol EP7 en `Removed` (actualización 2026-05-26). | Ya no aplica al backlog activo del MVP. |
| EP 8 (Plugins)    | "Done"                                                  | Epic 98628 = Done; todas las features Done.                 | Coherente. |
| HU MVP base (HU 1-11) | "Funcionalmente cumplidas y desplegadas en MVP"     | PBIs `Committed` no transicionados a `Done` en ADO.        | Limpieza pendiente: transicionar HU 1-11 a `Done` o `Pdte. Despliegue`. |
| HU TV1-TV6        | "Tipología versioning Done"                             | PBIs `Committed`.                                           | Idem: faltan transiciones de cierre. |
| HU 8.1-8.5        | "EP8 Done"                                              | PBIs `Committed` (excepto 98871 To Validate).               | Idem. |

> **Riesgo de gobierno:** ~25 PBI permanecen en estado `Committed` en ADO aunque el roadmap los considere cerrados. Esto distorsiona métricas de velocidad y burndown.

### 8.4 Otros hallazgos

- **Duplicado de Epic** `99090` (Removed) frente a `99089` (In Progress): mismo título "EP10 - Resolucion de Activo por Direccion en AssetResolver". Limpieza correcta vía Removed.
- **Test Cases removidos masivamente** (`99105`-`99115`): 11 escenarios de prueba `[RUN:20260420-inicial]` archivados. Sin impacto operativo.
- **Cobertura de tags** muy baja: los tags están poblados de forma no homogénea (`DocumentIA;EPx;…`) y muchos work items leídos en detalle muestran `System.Tags = null` o vacío.
- **AssignedTo nunca poblado** en la muestra leída (8 work items en detalle). Para una auditoría de responsabilidades reales, recomendable consultar `System.History` de cada item, no devuelto en este informe.

---

## 9. Apéndice — Consultas WIQL ejecutadas

### 9.1 Listado completo de Epics
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Epic'
ORDER BY [System.Id]
```

### 9.2 Listado completo de Features
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Feature'
ORDER BY [System.Id]
```

### 9.3 Listado completo de PBI / HU
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Product Backlog Item'
ORDER BY [System.Id]
```

### 9.4 Listado completo de Bugs
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Bug'
ORDER BY [System.Id]
```

### 9.5 Tasks con cambios desde 2026-04-01
```sql
SELECT [System.Id], [System.Title], [System.State], [System.ChangedDate], [System.AreaPath], [System.IterationPath], [System.Tags], [Microsoft.VSTS.Common.Priority]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Task'
  AND [System.ChangedDate] >= '2026-04-01T00:00:00Z'
ORDER BY [System.ChangedDate] DESC
```

### 9.6 Tasks abiertas (To Do + In Progress)
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Task'
  AND [System.State] IN ('To Do', 'In Progress')
ORDER BY [System.State], [System.Id]
```

### 9.7 Tasks completas (paginar — excede límite 200)
```sql
SELECT [System.Id], [System.Title], [System.State]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.WorkItemType] = 'Task'
  AND [System.Id] < 99000
ORDER BY [System.Id]
```
> Repetir con segundo bloque `[System.Id] >= 99000` para obtener el resto. La consulta unificada falla con `VS403474: The number of work items returned exceeds the size limit of 200`.

### 9.8 Cruce con IDs solicitados (32 IDs)
```sql
SELECT [System.Id], [System.WorkItemType], [System.State], [System.Title]
FROM WorkItems
WHERE [System.TeamProject] = 'AI DocClassExt'
  AND [System.Id] IN (
    99071, 99072, 99074, 99075, 99076, 99077, 99079, 99080,
    99101, 99103,
    99122, 99123, 99124, 99125, 99126, 99127, 99128, 99129,
    99141, 99160, 99179,
    99224, 99225, 99226, 99228, 99237,
    99251, 99252, 99253, 99254, 99255
  )
ORDER BY [System.Id]
```

### 9.9 Comando git utilizado para extracción de commits
```powershell
git log --all --since="90 days ago" --pretty=format:"%H|%ad|%s" --date=short
```

---

**Fin del inventario — generado en modo SOLO LECTURA. Ningún work item ha sido creado, modificado, comentado, vinculado ni transicionado durante esta auditoría.**
