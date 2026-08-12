# Dashboard ADO "Seguimiento DocumentIA" — diseño

**Fecha:** 2026-08-12
**Contexto:** org `sareb`, proyecto `AI DocClassExt`. El equipo no trabaja por sprints: las tareas se definen y ejecutan según interés. Se necesita un dashboard de seguimiento de work items con métricas y KPIs por feature, estado y fechas.

## Decisiones de partida (validadas con el usuario)

- **Audiencia mixta**: zona superior de KPIs/tendencias para reporting a dirección; zona inferior operativa para el día a día.
- **Eje temporal**: ventanas móviles (7/30 días) sobre `CreatedDate`/`ClosedDate`/`ChangedDate`, que ADO rellena solo. No se exige disciplina nueva de fechas (no se usan Start/Target Date).
- **Bloques de métricas**: flujo de trabajo, avance por feature, estados/WIP y calidad (bugs).
- **Materialización**: dashboard nativo de ADO creado por REST API con script versionado en el repo. Reproducible e idempotente.

## Datos reales que condicionan el diseño (inventario 2026-08-12)

- Jerarquía real: `Epic → Feature → Product Backlog Item ("HU n") → Task/Bug`. El tipo nativo "User Story" no se usa.
- Proceso Scrum con dos estados propios en PBI/Bug: **"Pdte. Despliegue"** y "To Validate".
- 929 work items de planificación; casi todo `Done`. Solo ~2 Features activas simultáneamente.
- **No se usan iteraciones ni sub-áreas**: todo cuelga de la raíz. Quedan descartados los widgets ligados a sprint (sprint burndown, velocity); los widgets Analytics por rango de fechas (burnup, lead/cycle time, CFD) sí funcionan.
- Fechas pobladas: `CreatedDate` siempre; `ClosedDate`/`StateChangeDate` en cerrados. `StartDate`/`TargetDate`/`Effort` no se usan.

## Arquitectura

Dos artefactos en ADO, creados y mantenidos por un script:

1. **Carpeta de queries compartidas** `Shared Queries/Dashboard/` — toda la lógica de negocio vive en las queries. Ajustar una métrica = editar una query; los widgets no se tocan.
2. **Dashboard "Seguimiento DocumentIA"** del equipo por defecto del proyecto (los dashboards cuelgan de un equipo; el script resuelve el equipo por API).

### Queries compartidas

| Query | Tipo | Lógica (WIQL) |
|---|---|---|
| `KPI - Abiertos` | flat | Tipo en (Epic, Feature, PBI, Task, Bug) y estado no en (Done, Removed) |
| `KPI - Creados 7d` | flat | Mismos tipos, `CreatedDate >= @Today - 7` |
| `KPI - Cerrados 7d` | flat | Mismos tipos, estado = Done y `ClosedDate >= @Today - 7` |
| `KPI - Bugs abiertos` | flat | Bug, estado no en (Done, Removed) |
| `KPI - Pdte Despliegue` | flat | Estado = 'Pdte. Despliegue' (PBI y Bug) |
| `Features - Activas con hijos` | árbol | Feature en (New, In Progress) + descendientes recursivos |
| `Chart - PBIs abiertos por estado` | flat | PBI, estado no en (Done, Removed) |
| `Chart - Tasks abiertas por estado` | flat | Task, estado no en (Done, Removed) |
| `Chart - Bugs por estado` | flat | Bug, estado ≠ Removed (volumen pequeño, se incluye Done) |
| `Operativa - En curso` | flat | Estado en (In Progress, Committed) |
| `Operativa - Estancados` | flat | Estado en (Approved, Committed, In Progress, To Validate, Pdte. Despliegue) y `ChangedDate < @Today - 14` |
| `Operativa - Sin asignar` | flat | PBI/Task/Bug, estado no en (Done, Removed), AssignedTo vacío |

### Disposición del dashboard

**Fila 1 — KPIs (query tiles 1×1, con reglas de color):**
Abiertos · Creados 7d · Cerrados 7d · Bugs abiertos · Pdte. Despliegue.
Reglas de color orientativas: Bugs abiertos >5 rojo; Pdte. Despliegue >3 ámbar (ajustables en la UI).

**Fila 2 — Tendencias (widgets Analytics, sin sprints):**
- **Burnup 30 días** por rango de fechas sobre el backlog de PBIs (no requiere iteraciones).
- **Lead Time** y **Cycle Time** rolling 30 días (PBIs y Bugs).
- **Cumulative Flow Diagram** del board de PBIs (detecta acumulación de WIP; las columnas del board mapean a estados aunque el board no se use activamente).

**Fila 3 — Avance por feature:**
- Widget de resultados con `Features - Activas con hijos` (ancho): cada feature abierta con sus PBIs/Tasks y estados. A la escala actual (~2 features activas) sustituye con ventaja a cualquier gráfico agregado.
- Charts de PBIs abiertos por estado y Tasks abiertas por estado (foto actual, tipo pie/bar).

**Fila 4 — Operativa diaria (query results + chart):**
En curso · Estancados (14 días sin cambios) · Sin asignar · Bugs por estado.

### Limitaciones conocidas (aceptadas)

- No existe gráfico nativo de "% completado por feature" (los charts de ADO no agrupan por padre). Se cubre con la query en árbol. Si el nº de features abiertas simultáneas crece (>8-10), reevaluar Power BI sobre Analytics/OData.
- Los charts "por estado" son foto actual, no tendencia; las tendencias las dan los widgets Analytics de la fila 2.
- Test Cases quedan fuera (se gestionan por Test Plans, no por WIQL).

## Implementación

**Script:** `scripts/ado/create-dashboard.sh` (bash + `az rest`, autenticación Entra ya validada contra `sareb.visualstudio.com`; sin PAT).

Comportamiento:
1. Resuelve proyecto y equipo por defecto (`GET _apis/projects`, `_apis/teams`).
2. Crea/actualiza la carpeta `Shared Queries/Dashboard` y las 12 queries (`POST/PATCH _apis/wit/queries`). Idempotente: busca por ruta antes de crear.
3. Resuelve los contribution IDs reales de los widgets vía `GET _apis/dashboard/widgettypes` (no se hardcodean los de Analytics, cuya nomenclatura varía).
4. Crea/actualiza el dashboard `Seguimiento DocumentIA` (`_apis/dashboard/dashboards`) con los widgets posicionados, enlazando cada widget a su query por ID.

**Riesgo conocido:** los `settings` JSON de los widgets Analytics (burnup, lead/cycle, CFD) no están documentados oficialmente. Plan B: si algún widget Analytics no queda bien configurado por API, el script lo deja creado sin configurar y se ajusta a mano en la UI (~5 min, una sola vez). Los widgets de query (tiles, charts, results) sí tienen settings estables.

**Verificación:**
- Re-ejecución del script sin duplicados (idempotencia).
- `GET` del dashboard confirmando nº de widgets y queries enlazadas.
- Validación visual del usuario abriendo la URL del dashboard.

**Gobernanza:** antes de implementar se crean los work items en ADO (Feature + Tasks) y el commit del script referencia `AB#` real, conforme a la norma del proyecto.
