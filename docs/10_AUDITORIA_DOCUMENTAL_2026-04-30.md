# 10. Auditoria Documental - 2026-04-30

> Auditoria de alineacion documentacion <-> codigo <-> infraestructura <-> work items.
> Modo: solo lectura. Este documento NO aplica cambios sobre repo, pipelines, configuracion, recursos ni work items.
> Complementa a `docs/09_AUDITORIA_CONFIGURACION_2026-04-30.md` (foco configuracion runtime/Azure).

---

## Nota sobre evidencias usadas

- Repositorio local en rama `develop` (sincronizada con `origin/develop`, HEAD `a3f40d2`).
- `git log` ultimos 60 dias, ramas activas locales y remotas, `git status -sb`.
- Ficheros: `README.md`, `azure-pipelines.yml`, `docker-compose.yml`, `.env.example`, todos los `.md` bajo `docs/`.
- Inventario de proyectos .NET bajo `src/backend`, `src/frontend`, `src/plugins`.
- Auditoria previa de configuracion del mismo dia: `docs/09_AUDITORIA_CONFIGURACION_2026-04-30.md` (untracked en el momento de redactar).
- Roadmap: `docs/07_ROADMAP_PENDIENTES.md`.
- **Limitacion explicita**: el subagente `azure-devops-work-items` fallo en la sesion (error interno del agente). No se pudo consultar el estado real de los Work Items en `https://sareb.visualstudio.com/AI%20DocClassExt`. Toda referencia a WI en este informe se basa en menciones `AB#NNNNN` en commits y documentacion, **no en el estado real en ADO**. Esto esta senalado como riesgo de auditoria y se propone como accion inmediata (ver seccion 6 y prompt anexo).

---

## 1. Executive Summary

| Dimension | Valoracion | Comentario |
|---|---|---|
| Alineacion documental general | **Media-Baja** | Existen 9 documentos numerados maestros y manuales bien estructurados, pero conviven con desviaciones tecnicas observables (stack, infra, pipeline). |
| Alineacion infra documentada vs real | **Baja** | `docs/INFRAESTRUCTURA_AZURE.md` titula `rg-documentia-mvp`; produccion real es `SRBRGDOCSAIPROD`. `README.md` declara `.NET 8`; pipeline usa SDK `9.x`. |
| Alineacion CI/CD vs documentacion | **Media** | Pipeline activo hace build+publish+deploy de 3 apps; stage `RunMigrations` esta `condition: false`. La seccion 7.3.2 del roadmap afirma que los tests no se ejecutan en CI, aunque el YAML actual si tiene una task `Test`. Inconsistencia interna. |
| Cobertura funcional vs WI | **No verificable** | Subagente ADO inoperativo. Solo trazabilidad por menciones `AB#` en commits. |
| Seguridad y secretos | **Media-Alta de riesgo** | Auditoria 09 ya detecto: API keys literales en seeds/artefactos, `FunctionsAdminApi__FunctionKey` literal en App Settings, `httpsOnly=false`, `publicNetworkAccess=Enabled`, MI con `User Access Administrator`. |
| Calidad global de docs | **Media-Alta** | Buenos contratos, manuales y especificaciones. Alta deuda en sincronizacion de fechas, versiones de epics y nombres de recursos. |
| Riesgo principal | **Drift no controlado** entre docs <-> codigo <-> Azure | Ya hay un plan en doc 09; no se ha ejecutado fase de correccion. |

**Veredicto auditor**: el proyecto tiene una documentacion sustancial pero presenta drift sistematico en cuatro vectores (stack runtime, infraestructura, pipeline, work items). La documentacion NO puede considerarse fuente unica de verdad hasta cerrar Fase 1-2 del plan ya propuesto en doc 09 + verificacion oficial de WI.

---

## 2. Documentation Gap Analysis

| Documento | Estado actual | Cobertura | Desalineaciones detectadas | Prioridad | Accion recomendada |
|---|---|---|---|---|---|
| `README.md` | Desactualizado | Setup, stack, deploy | Stack declara `.NET 8.0`, pipeline usa `9.x`. RG `rg-documentia-mvp` no existe (real: `SRBRGDOCSAIPROD`). Comando deploy con `infrastructure/bicep/main.bicep` no existe. Seccion notas 2026-04-15 si actual. | **Critica** | Reescribir secciones "Arquitectura", "Estructura", "Deploy". Anadir referencia obligatoria a docs 04/05/08. |
| `.env.example` | Desalineado | Variables locales | Usa nombres legacy `AZURE_AI_*`, `AZURE_OPENAI_KEY`. Produccion usa `Classification__*` / `Extraction__*`. | Alta | Regenerar plantilla con claves reales documentadas en doc 04 sec 4.4. |
| `docs/INFRAESTRUCTURA_AZURE.md` | Inconsistente | Diagrama Azure | Header dice RG `rg-documentia-mvp`; el diagrama interno usa nombres reales `srb*prodocai`. Mezcla nomenclaturas. | **Critica** | Corregir RG en cabecera, alinear con diagrama. |
| `docs/01_ARQUITECTURA_SISTEMA.md` | Parcialmente alineado | Arquitectura, stack, flujo | Stack table dice `.NET 8`, pipeline ya en `9.x`. Diagrama lista 12 activities; el codigo tiene **17** activities (incluye `ExtraerMarkdownLayoutActivity`, `ObtenerActivoActivity`, `ObtenerDocumentoGDCActivity`, `ObtenerMetadatosDocumentoGDCActivity`, `ObtenerUltimaEjecucionDuplicadoActivity`, `VerificarDuplicadoPorMD5Activity`). | **Critica** | Actualizar tabla de stack y diagrama del orquestador. |
| `docs/02_ANALISIS_FUNCIONAL.md` | Sin verificar drift | Funcional general | Fecha no contrastada con ultimas features (healthcheck, batch, prompt-adhoc, objectIdGDC, AAII/AACC dual). | Alta | Validar y anadir secciones de las features post-2026-04-13. |
| `docs/03_DISENO_TECNICO_DETALLADO.md` | A verificar | Diseno tecnico (48 KB) | Riesgo de quedar atras sobre healthcheck por componentes y prompt ad-hoc. | Alta | Diff tecnico contra activities y triggers reales. |
| `docs/04_MANUAL_EXPLOTACION.md` | Parcial | Operacion | Declara Azure SQL "(pendiente)" - ya existe `srbsqlprodocai`. Migraciones: dice arranque, pipeline tiene stage off. | **Critica** | Eliminar "pendiente" en SQL, documentar estrategia real de migraciones. |
| `docs/05_MANUAL_USO_CONFIGURACION.md` | Mayoritariamente alineado | Uso, config, contratos | Riesgo de no reflejar contrato `objectIdGDC` y `PromptInstrucciones`. | Media | Verificar secciones de contrato vs CONTRATO_API_HTTP. |
| `docs/06_PLAN_PRUEBAS.md` | A revisar | Plan de pruebas | El roadmap (7.3.2) afirma que el pipeline no ejecuta tests; el YAML actual incluye task `Test`. Contradiccion interna. | Alta | Reconciliar 06 + 07.3.2 + YAML. |
| `docs/07_ROADMAP_PENDIENTES.md` | Inconsistencias internas | Roadmap | Filas duplicadas para EP3 (85% y 88%) y EP9 (75% y 80%). Estado dice "EP10 DONE 100%" - coherente con ramas, pero EP6 % desactualizado. | Alta | Deduplicar filas, recalcular % con evidencia de WI. |
| `docs/08_CHECKLISTS_DESPLIEGUE.md` | Parcial | Checklists | Fila 3.9 SQL "PENDIENTE DE CREAR" y 3.10 Web App "PENDIENTE" estan desfasadas (ambos creados). | Alta | Marcar DONE y datar. |
| `docs/09_AUDITORIA_CONFIGURACION_2026-04-30.md` | **No commiteado** (untracked) | Auditoria config | Vigente y bien estructurada. Riesgo: no versionada -> puede perderse. | **Critica** | Decidir versionado (commit a develop o branch dedicada) via Git Governance Agent. |
| `docs/ANALISIS_FUNCIONAL_EP9_MANTENIMIENTO_BLOB.md` | A verificar | EP8/EP9 | Roadmap habla de EP9 GDC e EP8 Mantenimiento Blob. Riesgo de mezcla de naming. | Media | Validar nomenclatura EP8 vs EP9. |
| `docs/contratos/CONTRATO_API_HTTP.md` | A verificar | Contrato | Debe reflejar `PromptInstrucciones`, `objectIdGDC`, healthcheck. | Alta | Diff vs DTOs reales en `DocumentIA.Core`/`Functions`. |
| `docs/manuales/` (4 ficheros) | Mixto | Configuracion, deduplicacion, plugins, validaciones | OK como referencia tecnica. Validar versiones tras EP5 cierre A-1/A-2/A-3. | Media | Confirmar fecha de revision. |
| `docs/especificaciones/ESPECIFICACION_PLUGIN_ASSETRESOLVER.md` | Probablemente actual | Plugin AssetResolver | Refleja AAII/AACC dual y direccion fuzzy. | Baja | Confirmar contra rama `feature/AssetResolver-adress-search`. |
| `docs/superpowers/plans/2026-04-29-documentIAbatch-plan.md` | Conflicto | Plan Batch | Plan dice "no anadir a monorepo"; sin embargo existe `src/frontend/DocumentIA.Batch` **untracked** en el monorepo. | **Critica** | Decidir gobernanza: o se elimina del monorepo o se actualiza el plan. |
| `docs/superpowers/plans/2026-04-29-healthcheck-endpoint.md` | Implementado, plan obsoleto | Healthcheck | Implementado y mejorado con probes (`HealthcheckFunction`, `ISystemHealthService`, `ComponentsHealthSnapshot`). Plan original solo cubria version minima. | Media | Cerrar el plan o transformarlo en doc de diseno. |
| `docs/not in use/` | Stub | - | Ficheros con 358-362 bytes (stubs). README los referencia: `docs/manuales/MANUAL_ACTIVITIES_AZURE_FUNCTIONS.md` no existe; el real esta en `not in use/`. **README enlaza a ficheros inexistentes.** | **Critica** | Reparar enlaces en `README.md`. |
| `docs/auxiliares/` | Heterogeneo | SQL, plan tipologias, prompts | Muestrarios - no debe usarse como referencia operativa. | Baja | Etiquetar como "borradores/auxiliar". |
| `.github/workflows/infrastructure.yml` (referencia indirecta) | Posiblemente roto | CI/CD GitHub | Doc 09 lo marca apuntando a infra inexistente. No se valida en este analisis. | Alta | Confirmar existencia y eliminar/repararlo. |
| Ausentes / faltantes | - | - | (1) Doc operativo de **DocumentIA.Batch** dentro del monorepo si se mantiene. (2) Manual de **healthcheck por componentes** y consumo. (3) ADRs (no hay carpeta `docs/adr`). (4) Diagrama actualizado del orquestador con 17 activities. (5) Catalogo de App Settings vivo (auto-generado). | Alta | Crear nuevos documentos segun roadmap sec 5. |

---

## 3. WorkItem vs Delivery Analysis

> Sin acceso confiable a Azure DevOps en esta sesion. Tabla construida a partir de menciones `AB#` en commits + ramas + roadmap. **No representa el estado oficial en ADO**. Ver prompt anexo para reintentar inventario en otra sesion.

| WI / Grupo | Tipo inferido | Implementacion observable en repo | Documentacion reflejada | Estado deducido | Discrepancia |
|---|---|---|---|---|---|
| AB#99224, 99225, 99226, 99228 | Healthcheck endpoint v1 | `HealthcheckFunction` + tests OK | Plan superpowers, no doc operativa | Implementado | Doc operativa faltante |
| AB#99251-99255 | Healthcheck componentes + GDC fix + frontend | Merge a develop el 2026-04-30 | Sin doc especifica | Implementado | Doc faltante; activities/triggers actualizados sin reflejar en `01_ARQUITECTURA` |
| AB#99237 | DocumentIA.Batch | Plan + carpeta untracked en monorepo | Plan superpowers | **Inconsistente** | Plan dice "fuera del monorepo" |
| AB#99179 | Wizard tipologias Admin | Implementado y testeado | EP5 (roadmap) | Implementado | Roadmap aun muestra "80%" |
| AB#99141, 99160 | objectIdGDC + pre-dedupe MD5 | Implementado en functions/desktop | Contrato y manuales actualizados (commit `065c564`) | Implementado | Verificar `CONTRATO_API_HTTP.md` |
| AB#99122-99129 | Prompt ad-hoc + AssetResolver dual | Implementado, 443 tests | Roadmap EP5/EP10 | Implementado | EP10 marcado DONE 100% - coherente |
| AB#99101, 99103 | Tanda C (evidencia) | Solo paquete docs+scripts | - | Documental | Sin evidencia de delivery de codigo |
| AB#99071-99080 | EP6 Fase A/B/C: Monitor + telemetria | Implementado (`MonitorService`, `EjecucionesAdminFunction`, telemetria) | EP6 65% en roadmap | **Roadmap subestima %** | Recalcular |
| Items planificados sin trazo (EP7 GDPR, EP8 Blob lifecycle, V-1/V-2 cross-field, G-1/G-2/G-3 GDC) | Features/Stories | Sin commits | Documentados como pending | No iniciados | Coherente |
| Tests T-1..T-6 | Tasks calidad | Estado real desconocido (~231 tests reportados; un commit dice 443 tests pass) | Roadmap dice pending | **Inconsistente** | Recontar y actualizar |

**Hallazgo critico**: roadmap reporta `~231 tests` (seccion 7.3) y un commit dice `443 tests pass`. Esta divergencia debe verificarse antes de cualquier afirmacion de "DONE".

---

## 4. Configuration & Deployment Alignment

| Vector | Local (repo) | Dev/Local runtime | Azure Prod | Pipeline | Drift |
|---|---|---|---|---|---|
| Stack .NET | README dice 8.0 | csproj actual (no validado en este informe) | Function App `dotnet-isolated` | Pipeline `UseDotNet@2 -> 9.x` | **Critico**: README <-> pipeline |
| Resource Group | README/INFRA dice `rg-documentia-mvp` | n/a | Real: `SRBRGDOCSAIPROD` | Pipeline usa `SRBRGDOCSAIPROD` | **Critico** |
| SQL DB | docker-compose SQL local | Funciona | `srbsqlprodocai/DocumentIA` online | Stage migrations OFF | **Alto**: docs marcan SQL "pendiente" |
| Migraciones EF | `RunDatabaseMigrationsOnStartup` | true en local | `false` en Function App | Stage `RunMigrations` `condition: false`; intent en pipeline=`true` | **Alto**: tres fuentes desalineadas |
| AssetResolver settings en Function App | Codigo consume `AssetResolver__BaseUrl/ApiKey` | OK | Faltan en App Settings (doc 09) | Pipeline intenta setearlos | **Critico** |
| `FunctionsAdminApi__FunctionKey` | Literal en `Admin/appsettings.json` | Literal | Literal en App Settings | - | **Alto seguridad** |
| API Keys en seeds | Literales versionados (`config/`, `scripts/seeds/`, `publish/`, `bin/`) | - | - | - | **Critico seguridad** |
| Network public access | - | - | `httpsOnly=false`, `publicNetworkAccess=Enabled` en 3 apps | - | **Alto** |
| MI RBAC Function App | - | - | `User Access Administrator` sobre KV/Storage (excesivo) | - | **Alto** |
| Storage Functions hub | - | Azurite | `srbstgproapppdocai` con public network enabled | - | **Medio** |
| Tests en CI | Roadmap dice "no se ejecutan" | dotnet test local OK | - | YAML **si** tiene task `Test` | **Alto**: documentacion contradice pipeline |
| Module DocumentIA.Batch | Carpeta `untracked` en monorepo | App WPF compilable | n/a | Pipeline no la incluye | **Alto**: gobernanza ambigua |
| `.github/workflows/infrastructure.yml` | Mencionado como roto | - | - | Posible workflow GH paralelo | **Medio**: confirmar |
| Diagrama infra (`INFRAESTRUCTURA_AZURE.md`) | RG cabecera vs cuerpo del diagrama | - | - | - | **Alto**: inconsistencia interna |

---

## 5. Recommended Documentation Roadmap

### 5.1 Quick wins (1-2 dias, sin riesgo)

1. Reparar enlaces rotos en `README.md`: `docs/manuales/MANUAL_ACTIVITIES_AZURE_FUNCTIONS.md` apunta a fichero inexistente; el real esta en `docs/not in use/`. Decidir si recuperar o eliminar la referencia.
2. Corregir RG en `docs/INFRAESTRUCTURA_AZURE.md` (`rg-documentia-mvp` -> `SRBRGDOCSAIPROD`).
3. Actualizar stack en `docs/01_ARQUITECTURA_SISTEMA.md` y `README.md` a la version real (.NET 8 vs 9; verificar `.csproj` antes).
4. Eliminar afirmaciones obsoletas en `docs/04_MANUAL_EXPLOTACION.md` y `docs/08_CHECKLISTS_DESPLIEGUE.md`: SQL pendiente, Web App pendiente, Bicep inexistente.
5. Deduplicar filas en `docs/07_ROADMAP_PENDIENTES.md` (EP3 y EP9 aparecen dos veces).
6. Versionar la auditoria 09 y este documento 10 via Git Governance Agent.
7. Decision gobernanza `DocumentIA.Batch`: o se mueve a su repo dedicado y se borra la carpeta del monorepo, o se incorpora oficialmente y se actualiza README + pipeline.

### 5.2 Mid-term (1-2 sprints)

1. Reconciliar Plan de Pruebas: alinear `docs/06_PLAN_PRUEBAS.md`, seccion 7.3.2 de `07_ROADMAP_PENDIENTES.md`, conteo real de tests y task `Test` del pipeline. _(PENDIENTE)_
2. Diff `CONTRATO_API_HTTP.md` vs DTOs: confirmar `PromptInstrucciones`, `objectIdGDC`, healthcheck, AAII/AACC dual. _(DONE 2026-05-01: añadida sección healthcheck; resto ya documentado)_
3. Redisenar diagrama orquestador con las 17 activities reales. _(DONE 2026-05-01: actualizado `01_ARQUITECTURA_SISTEMA.md`)_
4. Crear manual de healthcheck por componentes (probes, payload, contrato, consumo Admin/Desktop). _(PENDIENTE)_
5. Crear catalogo vivo de App Settings generado automaticamente desde codigo (quien lee que) con script en `scripts/`. _(PENDIENTE - decisión usuario)_
6. Plantilla `.env.example` regenerada desde el listado real de secretos KV. _(DONE 2026-05-01)_
7. Verificacion oficial de WI en ADO (subagente) y reescritura de tabla de Epics con datos reales. _(DONE 2026-05-01: doc 11 + alineación EP7-EP10 en doc 07)_

### 5.3 Structural governance

1. Introducir ADRs en `docs/adr/0001-...md` con plantilla MADR. Decisiones a registrar: estrategia migraciones, politica de secretos, politica de network access, politica de tests en CI, gobierno de modulos satelite (Batch).
2. Pipeline post-deploy verification: anadir paso que verifique presencia de App Settings criticos sin imprimir valores (recomendado en doc 09 sec Fase 4.5).
3. Owners por documento: anadir `<!-- OWNER: ... -->` en cabecera de cada doc maestro.
4. Politica de versionado: cabecera con `Ultima actualizacion` + `Version` + `Commit ref` y enforcement por PR template.
5. Linter de docs (vale.sh, markdownlint, link-checker) en pipeline en stage paralelo no bloqueante inicialmente.
6. Inventario automatizado de configuracion (script + workflow nightly) que produzca un diff entre seeds, App Settings y KV.
7. Wiki ADO sincronizado o decision explicita de "monorepo es la fuente unica" para evitar duplicidad.

---

## 6. Editing Rules - Pre-condiciones obligatorias antes de tocar documentacion

No se debe editar ningun documento del repo hasta que se cumplan estas validaciones:

1. **Resolver el bloqueo de Work Items**: reintentar `azure-devops-work-items` o ejecutar consultas equivalentes; sin estado real de WI no se puede actualizar fiablemente roadmap, EPs o checklists.
2. **Confirmar version .NET real** leyendo `*.csproj` (`TargetFramework`) - no se ha verificado en este informe, solo se han observado declaraciones contradictorias entre README (8.0) y pipeline (9.x).
3. **Confirmar contenido SQL dinamico** (`ModeloConfigs`, `TipologiaConfigs`, `PluginTipologiaConfigs`) - bloqueo MFA documentado en doc 09.
4. **Decision sobre `RunDatabaseMigrationsOnStartup`** - pipeline, App Settings y docs deben converger en un unico valor.
5. **Decision sobre `DocumentIA.Batch`** - definir el rol de la carpeta `untracked` antes de documentar nada.
6. **Validar inexistencia/rotura** de `.github/workflows/infrastructure.yml`.
7. **Confirmar presencia o no** de la seccion de tests en CI (mirar `azure-pipelines.yml` actual completo y aceptar lo que este en `develop`).
8. **Cruzar `CONTRATO_API_HTTP.md` con DTOs**: hacer un diff antes de declarar el contrato como autoritativo.
9. **Politica de secretos**: confirmar si se rotan/migran las API keys literales versionadas antes de tocar docs operativas (de lo contrario, documentar drift seria documentar deuda activa).
10. **Aprobacion explicita del owner** del proyecto sobre el plan documental propuesto en sec 5 antes de cualquier `git commit` (regla de usuario: usar siempre el agente "Git Governance Agent").

---

## 7. Proximo paso sugerido

1. Reintentar inventario de WI con el subagente ADO (ver prompt anexo en seccion 8).
2. Aplicar Quick wins sec 5.1 en una rama `chore/docs-audit-2026-04-30` (via Git Governance Agent).
3. Versionar `docs/09_AUDITORIA_CONFIGURACION_2026-04-30.md` y este documento.
4. Programar Mid-term sec 5.2 como sprint dedicado de governance.

---

## 8. Anexo - Prompt para inventario de Work Items en otra sesion

> Ejecutar en una sesion nueva con el agente `azure-devops-work-items` activo. Persistir el resultado en `docs/11_INVENTARIO_WORKITEMS_<YYYY-MM-DD>.md`.

```
Actua como auditor de Azure DevOps en modo solo lectura sobre el proyecto "AI DocClassExt"
de la organizacion https://sareb.visualstudio.com.

OBJETIVO
Producir un inventario de Work Items que sirva como evidencia para una auditoria documental.
NO modificar nada. Si un ID no existe o no hay permiso, indicarlo expresamente.

ENTREGABLE
Un unico documento Markdown listo para guardar como
docs/11_INVENTARIO_WORKITEMS_<YYYY-MM-DD>.md con las siguientes secciones:

1. Resumen ejecutivo
   - Conteo total de WI por WorkItemType (Epic, Feature, User Story, Bug, Task).
   - Conteo total por State.
   - Top 10 areas e iteraciones mas activas.
   - Bloqueantes / Severidad alta abiertos.

2. Epics
   Tabla con: ID, Titulo, State, AreaPath, IterationPath, AssignedTo, Tags, %hijos cerrados.
   Para cada Epic, sub-tabla de Features hijas con su State y % hijos cerrados.

3. Features abiertas (State != Closed/Done/Removed)
   ID, Epic padre, Titulo, State, AssignedTo, IterationPath, Tags, ultima modificacion.

4. User Stories y Bugs abiertos
   ID, Tipo, Titulo, State, Priority, Severity (bugs), AssignedTo, Iteration, Tags, ultima modificacion.
   Ordenar por Priority asc y luego ChangedDate desc.

5. Cambios recientes (ultimos 30 dias)
   ID, Tipo, Titulo, State anterior -> State actual (si se puede), ChangedDate, ChangedBy.

6. Cruce con commits del repo
   Verificar los siguientes IDs referenciados en commits y reportar para cada uno:
   tipo, titulo, state, parent (Feature/Epic), si esta cerrado o no, area, iteration.
   IDs a cruzar:
   99251, 99252, 99253, 99254, 99255,
   99237,
   99224, 99225, 99226, 99228,
   99179,
   99141, 99160,
   99122, 99123, 99124, 99125, 99126, 99127, 99128, 99129,
   99101, 99103,
   99071, 99072, 99074, 99075, 99076, 99077, 99079, 99080.

7. WI bloqueantes o criticos
   Listado de WI con tag/blocked/severity 1 o priority 1 que sigan abiertos.

8. Hallazgos de auditoria
   - WI cerrados sin commits asociados detectables.
   - Commits sin WI referenciado.
   - Epics con discrepancia entre %hijos y descripcion del estado en docs/07_ROADMAP_PENDIENTES.md.
   - WI marcados Done con dependencias abiertas.

9. Apendice - Consultas WIQL utilizadas
   Pegar literal cada consulta WIQL ejecutada para reproducibilidad.

REGLAS
- Solo lectura. Nada de updates, comentarios, links nuevos, transiciones de estado.
- No inventar campos. Si un campo no existe en el proyecto, indicarlo.
- Truncar descripciones largas a 200 caracteres con elipsis.
- Si la consulta devuelve >200 elementos, paginar y reportar el conteo total exacto.
- Devolver SOLO el Markdown final, sin texto conversacional adicional.
```

---

## 9. Evidencias de solo lectura usadas

- `git log` 60 dias, `git status -sb`, `git branch -a --sort=-committerdate`.
- Lectura selectiva de `README.md`, `azure-pipelines.yml`, `docker-compose.yml`, `.env.example`.
- Recorrido completo de `docs/**/*.md` con `Get-ChildItem -Recurse`.
- Inventario de proyectos en `src/backend`, `src/frontend`, `src/plugins`.
- Lectura de `docs/09_AUDITORIA_CONFIGURACION_2026-04-30.md` para evitar duplicar diagnostico.
- Comprobacion de existencia de `HealthcheckFunction` y sus tests.
- Listado de activities en `src/backend/DocumentIA.Functions/Activities/`.

## 10. Limitaciones declaradas

- No se ejecuto consulta a Azure DevOps (subagente `azure-devops-work-items` con error en sesion).
- No se valido `TargetFramework` real en cada `.csproj`.
- No se ejecuto suite de tests para verificar el conteo (231 vs 443).
- No se valido contenido vivo de tablas SQL dinamicas (mismo bloqueo MFA que doc 09).
- No se reviso linea a linea `azure-pipelines.yml` (266 lineas, lectura parcial).
- No se reviso linea a linea `docs/02_ANALISIS_FUNCIONAL.md` ni `docs/03_DISENO_TECNICO_DETALLADO.md`.
