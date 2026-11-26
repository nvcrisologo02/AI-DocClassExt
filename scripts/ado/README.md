# Dashboard "Seguimiento DocumentIA" — gestión por script

## Qué hace `create-dashboard.sh`

Crea o actualiza (idempotente) en ADO — org `sareb`, proyecto `AI DocClassExt`, equipo por defecto:

- La carpeta `Shared Queries/Dashboard` con 12 queries (KPIs, charts, features activas, operativa).
- El dashboard **Seguimiento DocumentIA** con 16 widgets en 4 zonas: KPIs, tendencias Analytics, avance por feature y operativa diaria.

## Requisitos y uso

- Azure CLI con sesión iniciada (`az login`) y python 3 en PATH. Sin PAT.
- `bash scripts/ado/create-dashboard.sh` — re-ejecutable: actualiza sin duplicar y **sin perder** la configuración manual de los widgets Analytics (se preserva casando por nombre de widget; si se renombra un widget en la UI, el siguiente run lo recreará según el script).
- Para cambiar una métrica: editar el WIQL de la query correspondiente en el script y re-ejecutar (o editar la query en la UI si es un ajuste puntual; el siguiente run del script la pisará).

## Configuración manual one-time (widgets Analytics)

Los settings de los widgets Analytics no son configurables por REST de forma fiable; tras el primer run, configurarlos una vez en la UI (lápiz → Configure):

1. **Burnup 30d**: Team: AI DocClassExt Team · Work items: Backlog → Backlog items · Sin filtros · Burndown on: Count of Work Items · Time period: Start date = inicio del periodo a seguir (p. ej. 1 del mes en curso), End date = fecha objetivo (p. ej. fin del trimestre) · Plot burndown by: Date, intervalo Weeks · Advanced: Show burnup + Show total scope. **Ojo**: este widget no admite ventana rolling (fechas fijas); revisar las fechas al cambiar de periodo (p. ej. cada trimestre).
2. **Lead Time**: Backlog items · Swimlanes: todas · Rolling period: 30 días.
3. **Cycle Time**: Backlog items · Swimlanes: todas · Rolling period: 30 días.
4. **CFD**: Backlog items · todas las columnas del board · Rolling period: 30 días.

Opcional recomendado: en los tiles, añadir reglas de color (Configure → Conditional formatting), p. ej. "Bugs abiertos" rojo si >5 y "Pdte. Despliegue" ámbar si >3.

## Notas de diseño

- Sin sprints: todas las métricas usan ventanas móviles sobre CreatedDate/ClosedDate/ChangedDate.
- "Estancados" = estados activos sin cambios en 14 días.
- La descripción del dashboard tiene un máximo de 128 caracteres (límite de la API: `VS403360`).
- Spec: `docs/superpowers/specs/2026-08-12-ado-dashboard-seguimiento-design.md`.

## Get-PipelinePreview.ps1

Obtiene el YAML expandido (con templates resueltos) de un pipeline de Azure DevOps mediante la API de preview (`POST .../_apis/pipelines/<id>/preview`, `previewRun: true`), para comparar el YAML final antes y después de refactorizar un pipeline sobre templates. **No ejecuta nada**: solo devuelve el YAML. La rama debe estar subida al remoto.

Token: `az account get-access-token` por defecto; `-AccessToken` permite inyectarlo (tests o `System.AccessToken`). Con `-WhatIf` imprime URL y cuerpo sin llamar. Salida 0 si va bien; 2 si falla la API (un 403 puede ser falta de permiso de preview sobre el pipeline), falta `finalYaml` o no hay token.

```bash
pwsh -NoProfile -Command "& ./scripts/ado/Get-PipelinePreview.ps1 -PipelineId 807 -Branch feature/101570-pipeline-release-hito1 -TemplateParameters @{ targetEnvironment = 'dev' } -OutFile docs/auxiliares/temps/2026-11-27/preview-antes/807-dev.yml"
```

Tests: `pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.9.0; Invoke-Pester -Path ./scripts/ado/tests/Get-PipelinePreview.Tests.ps1 -Output Detailed"`.
