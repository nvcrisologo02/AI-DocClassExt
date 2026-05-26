---
applyTo: "**"
description: "Reglas obligatorias para gestionar Azure DevOps Test Plans reales mediante REST API y evitar confundirlos con Work Items."
---

# Azure DevOps Test Plans

Para cualquier tarea relacionada con Azure DevOps Test Plans, suites o casos E2E:

- No confundas Work Items tipo `Test Plan`, `Test Suite` o `Test Case` con entidades reales del modulo Test Plans.
- Un Test Plan canonico debe existir en la REST API de Test Plans y abrir en la URL `_testPlans?planId=<id>`.
- Crea planes y suites con Azure DevOps Test Plans REST API, no con `az devops` CLI ni con YAML pipelines.
- Crea Test Cases con Work Items REST API como tipo `Test Case`.
- Asocia Test Cases a suites usando la API REST de Test Plans/Test Management correspondiente; no basta con crear el Work Item.
- No des por valido ningun plan hasta validar por API:
  - `GET https://dev.azure.com/{organization}/{project}/_apis/testplan/plans/{planId}?api-version=7.0`
  - root suite existente.
  - suites hijas esperadas existentes.
  - cada suite con al menos un Test Case asociado.
  - URL canonica navegable: `https://sareb.visualstudio.com/AI%20DocClassExt/_testPlans?planId=<planId>&suiteId=<rootSuiteId>`.
- Si aparece un ID que existe como Work Item pero falla en `_testPlans`, tratese como duplicado/no canonico y no como Test Plan real.
- Ante duplicados, limpiar de forma conservadora: marcar Work Items obsoletos como `Inactive`, `Completed` o `Closed` con comentario explicativo; no borrar fisicamente salvo instruccion explicita.

Endpoints base correctos:

- Crear Test Plan: `POST https://dev.azure.com/{organization}/{project}/_apis/testplan/plans?api-version=7.0`
- Crear Test Suite: `POST https://dev.azure.com/{organization}/{project}/_apis/testplan/suites/{planId}?api-version=7.0`
- Crear Test Case WI: `POST https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/$Test%20Case?api-version=7.0`
- Asociar Test Case a Suite: `POST https://dev.azure.com/{organization}/{project}/_apis/test/Plans/{planId}/suites/{suiteId}/testcases/{testCaseId}` con `Accept: application/json; api-version=7.0` y `Content-Type: application/json`.

Notas PowerShell/REST:

- En URLs con variables seguidas de `?api-version`, usar `${id}?api-version=7.0` para evitar interpolacion incorrecta.
- En JSON Patch de un solo elemento, enviar un array JSON explicito (`[{...}]`) para evitar que PowerShell colapse el array a objeto.
- Si el POST de asociacion falla por `Content-Type`, reenviar con `Content-Type: application/json` y body `{}`.
- Para validar casos asociados por suite, usar `GET https://dev.azure.com/{organization}/{project}/_apis/test/Plans/{planId}/suites/{suiteId}/testcases` con `Accept: application/json; api-version=7.0`.

Definition of Done para altas de Test Plans:

1. Plan real creado por REST API de Test Plans.
2. Root suite identificado.
3. Suites funcionales creadas bajo el plan real.
4. Test Cases creados como Work Items y asociados a sus suites.
5. Conteos verificados por REST API.
6. URL canonica probada/documentada.
7. Documento de trazabilidad actualizado.
