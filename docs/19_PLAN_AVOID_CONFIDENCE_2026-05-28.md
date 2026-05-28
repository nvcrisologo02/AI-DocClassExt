# Plan de implementacion - avoidConfidence en campos de tipologia

Fecha: 2026-05-28
Estado: Implementado y verificado en rama `feature/99676-avoid-confidence`
Work Items: Feature 99675, User Story 99676, Tasks 99677-99682

## 1. Objetivo

Añadir una propiedad opcional `avoidConfidence` (booleano) al modelo de campo `FieldValidationConfig`.
Los campos marcados con `avoidConfidence: true` se excluyen del calculo del score de confianza de extraccion
y de la lista `CamposBajaConfianza`, pero siguen participando en completitud y en `DatosExtraidos`.

El objetivo es evitar que campos cuya confianza CU es sistematicamente baja (aunque el valor extraido sea
siempre correcto) penalicen artificialmente el score global y disparen fallbacks innecesarios a GPT.

## 2. Especificacion funcional de referencia

- Regla 1: El campo es invisible para el calculo del score de confianza (no entra ni alto ni bajo).
- Regla 2: El campo no interviene en la decision de fallback (ExtracUmbralFallback).
- Regla 3: El campo NO aparece en CamposBajaConfianza aunque su confianza CU sea inferior al umbral.
- Regla 4: El campo SI cuenta para completitud. Si es required y no esta presente, penaliza igual.
- Regla 5: El valor extraido llega a DatosExtraidos con normalidad.
- Regla 6: ConfianzaPorCampo en MetricasDebug se mantiene completo (sin filtrar) para trazabilidad.
           Se recomienda añadir CamposExcluidosConfianza en MetricasDebug para auditoria.

Ejemplo de configuracion en JSON de tipologia:

```json
{
  "name": "ReferenciaRegistral",
  "required": false,
  "avoidConfidence": true
}
```

La ausencia de la propiedad equivale a false. Retrocompatibilidad total sin migracion.

## 3. Decision de diseño

`ConfidenceCalculator.ExtracCU` NO cambia su firma. El filtrado de campos ocurre en los callers
(proveedores de extraccion) antes de pasar `fieldConfs`, manteniendo el calculador puro y sin
dependencias de configuracion de campo.

Implementacion: el filtrado se centraliza en `ConfidenceFieldFilter` para evitar duplicar la logica
en cada proveedor. Los callers siguen siendo responsables de decidir que campos entran en el calculo.

## 4. Fases de implementacion

### Phase 1 — Modelo de campo (prerequisito de todo)

**Paso 1:** `src/backend/DocumentIA.Core/Configuration/TipologiaValidationConfig.cs`
- Clase: `FieldValidationConfig`
- Añadir: `public bool AvoidConfidence { get; set; } = false;`
- Default `false` garantiza retrocompatibilidad total sin migracion de datos.

### Phase 2 — AzureContentUnderstandingProvider (bloquea Phase 4)

**Paso 2:** Calcular set de exclusion justo despues de construir `confidenceMap` (linea ~111):
```csharp
var avoidConfidenceFields = tipologiaConfig.Fields
    .Where(f => f.AvoidConfidence)
    .Select(f => f.Name)
    .ToHashSet(StringComparer.OrdinalIgnoreCase);
```

**Paso 3:** Filtrar `fieldConfs` (linea 111) excluyendo los campos del set antes de pasar a `ExtracCU`:
```csharp
var fieldConfs = confidenceMap
    ?.Where(kvp => !avoidConfidenceFields.Contains(kvp.Key))
    .Select(kvp => (double?)kvp.Value)
    .ToList();
```

**Paso 4:** Mantener `metricasDebug.ConfianzaPorCampo` con el `confidenceMap` COMPLETO sin filtrar (Regla 6).

**Paso 5:** Filtrar `CamposBajaConfianza` (lineas 128-131):
```csharp
metricasDebug.CamposBajaConfianza = metricasDebug.ConfianzaPorCampo
    .Where(kvp => kvp.Value < umbralDuda && !avoidConfidenceFields.Contains(kvp.Key))
    .Select(kvp => kvp.Key)
    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
    .ToList();
```

**Paso 6 (opcional):** Poblar `metricasDebug.CamposExcluidosConfianza`:
```csharp
metricasDebug.CamposExcluidosConfianza = avoidConfidenceFields
    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
    .ToList();
```

### Phase 3 — GptFallbackExtraerDataProvider (paralelo con Phase 2)

**Paso 7:** Mismo patron que Phase 2:
- Set de exclusion `avoidConfidenceFields`
- Filtrar `fieldConfs` en la llamada a `ExtracCU` (linea ~509)
- Filtrar `CamposBajaConfianza` (linea 523)

`tipologiaConfig` ya esta disponible en ese scope (confirmado en codigo).

### Phase 3b — AzureDocumentIntelligenceExtraerDataProvider (paralelo con Phase 2)

**Paso 8:** Este proveedor tambien llama a `ExtracCU` (linea 159) con `fieldConfs` sin filtrar,
pero NO construye `CamposBajaConfianza` explicitamente. Solo requiere:
- Filtrar `fieldConfs` con el set de exclusion antes de pasar a `ExtracCU`.
- No hay asignacion de `CamposBajaConfianza` que modificar.

### Phase 4 — MetricasDebug (depende de Phase 2+3b, opcional)

**Paso 9:** `src/backend/DocumentIA.Core/Models/ExtraccionModels.cs`
- Clase: `ConfidenceMetricasExtraccion`
- Añadir: `public List<string>? CamposExcluidosConfianza { get; set; }`
- Permite auditoria directa de que campos fueron excluidos del score.

### Phase 5 — Tests unitarios (paralelo con Phase 2+3)

**Paso 10:** `src/backend/DocumentIA.Tests.Unit/Services/ConfidenceCalculatorTests.cs` — nuevos casos de aceptacion:
- Campo con `avoidConfidence=true` y confianza CU 0.1: el score global NO se reduce
- Campo con `avoidConfidence=true` y `required=true` ausente: sigue penalizando `ratioRequeridos` (completitud)
- Campo con `avoidConfidence=true`: NO aparece en `CamposBajaConfianza` aunque este bajo el umbral
- Campo sin la propiedad (default false): comportamiento identico al actual
- Valor del campo con `avoidConfidence=true` llega a `DatosExtraidos` normalmente

### Phase 6 — Work Items en Azure DevOps

**Paso 11:** Crear los WI en proyecto **AI DocClassExt** (org: https://sareb.visualstudio.com):
- **Feature**: "avoidConfidence en campos de tipologia"
- **User Story**: hija de la Feature
- **Tasks**: una por cada step de implementacion (ver seccion 5)

## 5. Archivos afectados

| Archivo | Cambio |
|---------|--------|
| `src/backend/DocumentIA.Core/Configuration/TipologiaValidationConfig.cs` | Añadir `AvoidConfidence` a `FieldValidationConfig` |
| `src/backend/DocumentIA.Core/Services/ConfidenceFieldFilter.cs` | Helper puro para resolver campos excluidos, filtrar confianzas y filtrar campos de baja confianza |
| `src/backend/DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs` | Filtrar `fieldConfs` y `CamposBajaConfianza` (lineas 108-131) |
| `src/backend/DocumentIA.Functions/Services/GptFallbackExtraerDataProvider.cs` | Mismo patron (lineas 505-530) |
| `src/backend/DocumentIA.Functions/Services/AzureDocumentIntelligenceExtraerDataProvider.cs` | Filtrar `fieldConfs` (linea 159) |
| `src/backend/DocumentIA.Core/Models/ExtraccionModels.cs` | `CamposExcluidosConfianza` en `ConfidenceMetricasExtraccion` (opcional) |
| `src/backend/DocumentIA.Tests.Unit/Services/ConfidenceFieldFilterTests.cs` | Tests unitarios de filtrado, baja confianza y required/completitud |
| `src/backend/DocumentIA.Tests.Unit/Configuration/TipologiaValidationConfigTests.cs` | Tests de default y deserializacion de `AvoidConfidence` |
| `src/backend/DocumentIA.Functions/config/tipologias/*.validation.json` | Campos especificos a determinar |
| `scripts/seeds/20260410-080308/config/tipologias/*.validation.json` | Mismos cambios en seeds (a determinar) |

## 6. Verificacion

1. `dotnet build` limpio en `src/backend/DocumentIA.Functions`
2. `dotnet test` en `DocumentIA.Tests.Unit` — todos los tests pasan
3. Test manual con documento cuyo campo `avoidConfidence=true` tenga confianza CU baja: verificar que el score global no baja y el campo no aparece en `CamposConDuda` del output
4. Test de regresion: procesar documento sin ningun campo `avoidConfidence` — output identico al actual

## 7. Decisiones registradas

- `ConfidenceCalculator.ExtracCU` no cambia su firma. El filtrado ocurre en los callers.
- `ConfianzaPorCampo` en MetricasDebug se mantiene sin filtrar (Regla 6 de la spec).
- La ausencia de `avoidConfidence` equivale a `false` (default value en C#).
- `CamposExcluidosConfianza` es opcional segun spec ("Se recomienda") — incluido en el plan.
- Los JSON de tipologias se actualizaran cuando se identifiquen los campos concretos.

## 8. Estado de implementacion

- Rama creada: `feature/99676-avoid-confidence`.
- User Story 99676 cerrada en estado `Done`.
- Feature 99675 cerrada en estado `Done`.
- Tasks 99677-99682 cerradas en estado `Done`.
- Verificacion ejecutada: build de Functions correcto, tests focalizados 36/36, suite unitaria completa 633/633.
- No se han marcado campos concretos en los JSON de tipologias; la funcionalidad queda disponible para configuracion futura.
- `ConfianzaPorCampo` se conserva completo y `CamposExcluidosConfianza` expone los campos excluidos para auditoria.
