# Guia operativa - Bateria E2E de clasificacion con Azure DevOps

Fecha: 2026-05-26
Estado: Activa
Ambito: ejecucion local de bateria E2E de clasificacion y publicacion de resultados vinculados en ADO Test Plans.

## 1. Objetivo

Documentar de forma operativa la bateria E2E de clasificacion para:

- Ejecutarla en local sin perder contexto.
- Entender cobertura funcional por grupos.
- Publicar resultados en ADO de forma correctamente vinculada.
- Facilitar retomado y troubleshooting en sesiones futuras.

## 2. Artefactos clave

- Script principal: `tests/api-tests/test-classification-process.ps1`
- Casos: `tests/api-tests/classification-process-cases.json`
- Artefactos de salida: `tests/api-tests/artifacts/classification-process/`

## 3. Cobertura funcional

La bateria cubre 23 casos distribuidos en 8 grupos:

- Grupo A: flujos por proveedor (`auto`, `di`, `gpt`).
- Grupo B: niveles de clasificacion GPT (`TDN1`, `TDN1_TDN2`).
- Grupo C: markdown preprocesado (inyectado vs generado).
- Grupo D: `classificationOnly` y recorte de paginas.
- Grupo E: deduplicacion (`skipDuplicateCheck`, `forceReprocess`, clave por nivel).
- Grupo F: umbral de confianza.
- Grupo G: validacion de contrato HTTP 4xx.
- Grupo H: calidad del output (tipologia conocida/virtual).

## 4. Requisitos previos

Antes de ejecutar:

1. Azure Functions host activo en `http://localhost:7071`.
2. Endpoint `IngestDocument` disponible.
3. Documentos de prueba accesibles segun rutas del JSON de casos.
4. Si se publica en ADO: PAT con permisos de Test (Read/Write).

## 5. Ejecucion local

Ejemplo base:

```powershell
Set-Location "c:\temp\MVP\documento-ia-clasificacion-mvp\tests\api-tests"
.\test-classification-process.ps1
```

Ejemplo por grupos:

```powershell
.\test-classification-process.ps1 -Groups A,B,G
```

Ejemplo estricto (falla si hay FAIL o SKIP):

```powershell
.\test-classification-process.ps1 -Strict
```

## 6. Publicacion en ADO

### 6.1 Parametros de publicacion

```powershell
.\test-classification-process.ps1 `
  -PublishToAdo `
  -AdoOrg "https://sareb.visualstudio.com" `
  -AdoProject "AI DocClassExt" `
  -AdoPat $env:ADO_PAT `
  -AdoTestPlanId 99581
```

### 6.2 Mecanica correcta de vinculacion

La publicacion valida en ADO se basa en:

1. Resolver mapping estable `caso -> Test Case WI`.
2. Obtener `testPointId` reales por suite del plan.
3. Crear Test Run planificado con `pointIds`.
4. Actualizar resultados precreados del run mediante `PATCH`.

Esta mecanica evita resultados sueltos y garantiza trazabilidad en jerarquia Suite -> Test Case.

## 7. Estado ADO de referencia (validado)

Referencia operativa actual:

- Test Plan operativo: `99581`.
- Suites activas de clasificacion: `99583` a `99590`.
- Run de referencia con vinculacion correcta: `1076772`.

Verificacion del run 1076772:

- Resultados totales: 23.
- Completed: 23.
- Passed: 23.
- Con `testCaseId` real: 23/23.
- Con `testPointId` real: 23/23.

## 8. Validaciones de salida esperadas

En ejecucion local:

- Resumen final con Total/PASS/FAIL/SKIP.
- Tabla por caso con estado y metadatos relevantes.
- CSV de resumen en `artifacts/classification-process`.
- JSON por caso para auditoria y diagnostico.

En ADO:

- Run en estado `Completed`.
- 23 resultados asociados a test points del plan.
- Navegacion por suite/case con outcome visible.

## 9. Troubleshooting rapido

### Error de conexion a localhost:7071

Sintoma:

- `No se puede establecer una conexion ... localhost:7071`.

Accion:

- Levantar Functions host y reintentar.

### Publicacion ADO 401

Sintoma:

- `Unauthorized` al crear run o publicar resultados.

Accion:

- Revisar PAT y permisos Test Read/Write.

### Publicacion ADO 400 al crear resultados

Sintoma:

- `Bad Request` al enviar resultados.

Causa tipica:

- Intentar `POST` de resultados sueltos en run planificado.

Accion:

- Usar flujo correcto: run con `pointIds` + `PATCH` de resultados precreados.

### Resultados no vinculados a suites/cases

Sintoma:

- `testCaseId` o `testPointId` a cero o vacio.

Accion:

- Verificar mapping de casos y resolucion de test points del plan.

## 10. Criterio de exito de la bateria

Se considera ejecucion cerrada correctamente cuando:

1. La bateria termina sin FAIL/SKIP en escenario objetivo.
2. Se generan artefactos JSON y CSV.
3. El run ADO queda en `Completed`.
4. Todos los resultados publicados tienen `testCaseId` y `testPointId` reales.

## 11. Checklist de retomado

Antes de retomar en otra sesion:

- Confirmar que `tests/api-tests/test-classification-process.ps1` mantiene el flujo de publicacion actual.
- Confirmar que el plan 99581 y suites 99583-99590 siguen vigentes.
- Ejecutar una validacion corta por grupos (`A,G`) antes de la bateria completa.
- Revisar rapidamente el ultimo run para comprobar vinculacion.

## 12. Referencias

- Plan maestro de expansion: `docs/16_PLAN_EXPANSION_BATERIAS_E2E_ADO_2026-05-26.md`
- Script de bateria: `tests/api-tests/test-classification-process.ps1`
- Casos de bateria: `tests/api-tests/classification-process-cases.json`