# Confianza Agregada — Análisis Técnico y Funcional

---

## 1. Propósito

El sistema asigna una **puntuación de confianza numérica (0–1)** a cada ejecución de procesamiento documental, descompuesta en tres etapas del pipeline: clasificación, extracción y validación. A partir de esas tres puntuaciones se calcula una **confianza global** y un **estado de calidad** ("OK", "REVISION", "ERROR") que permiten a los consumidores tomar decisiones automatizadas o de revisión humana sin necesidad de inspeccionar el detalle interno.

---

## 2. Visión funcional

### 2.1 ¿Qué mide cada etapa?

| Etapa | Pregunta que responde | Fuente |
|---|---|---|
| **Clasificación** | ¿Con qué certeza se ha identificado el tipo de documento? | Confianza nativa del modelo (DI o GPT self-report) |
| **Extracción** | ¿Con qué completitud y fiabilidad se han obtenido los datos? | Ratio de campos cubiertos + confianza de campo (CU) o confianza self-report (GPT) |
| **Validación** | ¿Cuántas reglas de negocio se han superado? | 1 − errores / reglas_requeridas |

### 2.2 Confianza global

La confianza global es el **mínimo de las tres etapas**. Un solo eslabón débil lastra el resultado completo, forzando revisión humana si alguna etapa falla.

```
ConfianzaGlobal = MIN(ConfianzaClasificacion, ConfianzaExtraccion, ConfianzaValidacion)
```

> Si la tipología tiene extracción deshabilitada (`extraction.enabled = false`), la confianza de extracción se omite del cálculo.

### 2.3 Estado de calidad

| Estado | Condición | Interpretación |
|---|---|---|
| **OK** | `ConfianzaGlobal ≥ 0.85` | Procesamiento automático sin revisión |
| **REVISION** | `0.70 ≤ ConfianzaGlobal < 0.85` | Requiere revisión humana |
| **ERROR** | `ConfianzaGlobal < 0.70` | Alta probabilidad de dato incorrecto o fallido |

Los umbrales son configurables por tipología (ver §5). Los defaults `0.85` y `0.70` representan los criterios consensuados para el MVP.

> `EstadoCalidad` es un campo **independiente** de `Estado`. `Estado` refleja el resultado del proceso (OK, VALIDACION_CON_ERRORES, ERROR, DUPLICADO…). `EstadoCalidad` refleja la fiabilidad de los datos obtenidos.

---

## 3. Pipeline y proveedores

```
IngestDocument (HTTP trigger)
        │
        ▼
DocumentProcessOrchestrator (Durable)
        │
        ├─► ClasificarActivity ──────────────────────────────────────────┐
        │       ├─ Azure Document Intelligence  → ConfianzaDI            │
        │       └─ GPT 4o-mini (fallback si DI < umbralFallback)        │
        │               └─ ConfianzaGPT                                  │
        │                                                                 ▼
        │                                              ConfigurableClasificarDataProvider
        │                                              selecciona proveedor y propaga
        │                                              ConfianzaDI + ProveedorClasif
        │
        ├─► ExtraerActivity ─────────────────────────────────────────────┐
        │       ├─ Azure Content Understanding  → formula ponderada CU   │
        │       └─ GPT 4o-mini (fallback)                                │
        │               ├─ confianza_extraccion (self-report del modelo) │
        │               └─ valor calculado (ExtracCU) como fallback      │
        │                                                                 ▼
        │                                              ConfianzaExtraccion + ProveedorExtrac
        │
        ├─► ValidarActivity ──────────────────────────────────────────────┐
        │       └─ Motor de reglas                                        │
        │              ConfianzaValidacion = 1 − errores/reglas_req       │
        │
        └─► (Integrar | SubirGDC | Persistir | Prompt)
                │
                ▼
        ConfidenceCalculator.Global(clasif, extrac?, valid)
        ConfidenceCalculator.EstadoCalidad(global, cfg)
```

---

## 4. Cálculo detallado por etapa

### 4.1 Clasificación — `ConfidenceCalculator.ClasifFinal`

```
si fallbackUsado:
    confianza = CLAMP(ConfianzaGPT ?? ConfianzaDI ?? 0.5, 0, 1)
si no:
    confianza = CLAMP(ConfianzaDI ?? 0.0, 0, 1)
```

- **Azure Document Intelligence** devuelve `documents[0].confidence` directamente.
- **GPT 4o-mini** devuelve un campo `"confianza"` en el JSON de respuesta (0–1). Si no lo incluye, se usa `0.5` como fallback neutro.
- `ConfigurableClasificarDataProvider` propaga `ConfianzaDI` al resultado GPT cuando éste toma el control (para trazabilidad: se sabe la confianza que provocó el fallback).

### 4.2 Extracción CU — `ConfidenceCalculator.ExtracCU`

**Fórmula (3 componentes ponderados):**

```
ConfianzaExtrac =
    w_campos   × AvgConf
  + w_req      × RatioRequeridos
  + w_warnings × (1 − RatioWarnings)
```

| Variable | Descripción | Valor default del peso |
|---|---|---|
| `AvgConf` | Promedio de `fields[*].confidence` del response CU. Si CU no devuelve confianzas, se usa `camposPresentes / camposTotales` | `w_campos = 0.5` |
| `RatioRequeridos` | `camposRequeridosPresentes / camposRequeridos` | `w_requeridos = 0.3` |
| `1 − RatioWarnings` | Penalización por warnings. `RatioWarnings = min(1, warnings / camposTotales)` | `w_warnings = 0.2` |

**Métricas de debug** disponibles en `DetalleEjecucion.Extraccion.MetricasDebug` (no persistidas en BBDD):
- `PromedioConfianza` — componente avgConf calculado
- `RatioRequeridos` — componente ratioReq
- `CamposConConfianza` — número de campos con confianza individual de CU
- `CamposTotales` — total de campos de la tipología

### 4.3 Extracción GPT — `confianza_extraccion` auto-reportada

El modelo GPT incluye en su respuesta JSON el campo `confianza_extraccion` (0.0–1.0), siguiendo el mismo patrón que clasificación usa con `confianza`. El system prompt solicita explícitamente ese campo:

```
'confianza_extraccion' (número entre 0.0 y 1.0 que refleja tu confianza global en la extracción)
```

La asignación final sigue la regla:

```
ConfianzaExtrac = CLAMP(confianzaExtraccionGpt ?? confianzaCalculada, 0, 1)
```

- **`confianzaExtraccionGpt`**: valor `confianza_extraccion` del JSON de respuesta. Si el modelo lo devuelve, se usa directamente.
- **`confianzaCalculada`**: resultado de `ExtracCU(fieldConfs, campos, requeridos, warnings, cfg)`, idéntico al cálculo de CU. Se usa como fallback si el modelo no incluye el campo o la respuesta está mal formada.

Cuando el modelo GPT reporta `confianza_extraccion`, se usa ese valor; en su ausencia se aplica como fallback el valor calculado al estilo CU (`confianzaCalculada`). El helper `ConfidenceCalculator.ExtracGPT` conserva un valor por defecto de `0.6` para los casos en que se invoca sin confianza self-report (ver tabla de la sección 8). GPT no devuelve confianzas por campo individuales; éstas siguen siendo solicitadas en `confianza_por_campo` y se propagan a `CamposBajaConfianza`.

### 4.4 Validación — `ValidarActivity`

```
si no hay errores:    ConfianzaValid = 1.0
si hay errores:       ConfianzaValid = CLAMP(1 − ErrorCount / max(1, TotalChecked), 0, 1)
```

Donde:
- `ErrorCount` = número de resultados con severidad `Error`
- `TotalChecked` = **total de reglas evaluadas** (las que pasaron + las que fallaron), contado en `ValidationEngine`

Los `Warning` **no** penalizan el numerador (`ErrorCount`). Aparecen en `Validaciones` del output pero no reducen la confianza. La confianza es proporcional al ratio de errores sobre el total de reglas configuradas para esa tipología.

Ejemplo: 20 reglas, 1 falla con Error:
```
ConfianzaValid = 1 - 1/20 = 0.95  →  EstadoCalidad = "OK"
```

Ejemplo real (NS 2691): 28 reglas evaluadas, 2 errores de campo:
```
ConfianzaValid = 1 - 2/28 ≈ 0.93
```

> **Nota sobre `ConfidenceCalculator.Validacion`**: existe como método auxiliar con la misma fórmula (`1 - errores/totalReglas`), pero la fuente de verdad en producción es `ValidarActivity`, que calcula directamente sobre el `ValidationReport`.

---

## 5. Configuración por tipología

### 5.1 Jerarquía de umbrales de fallback

Los umbrales que determinan cuándo se activa el fallback GPT siguen una cadena de prioridad. En clasificación hay un único umbral. En extracción hay **dos criterios independientes**:

- **Completitud**: ratio de campos esperados (según la tipología) presentes en `DatosExtraidos`.
- **Confianza**: valor de `ConfianzaExtraccion` devuelto por CU.

Si **cualquiera** de los dos criterios no supera su umbral, se activa el fallback GPT.

La cadena de resolución para **cada criterio de extracción** (de mayor a menor prioridad) es:

```
petición HTTP (específico/legado)   →   tipología (específico/legado)   →   modelo/servidor
```

| Nivel | Campo clasificación | Campo extracción (completitud) | Campo extracción (confianza) |
|---|---|---|---|
| **1 — Petición** | `instrucciones.classification.umbral` | `instrucciones.extraction.umbralCompletitud ?? instrucciones.extraction.umbral` | `instrucciones.extraction.umbralConfianza ?? instrucciones.extraction.umbral` |
| **2 — Tipología** | `confidenceConfig.clasifUmbralFallback` | `confidenceConfig.extracUmbralFallbackCompletitud ?? confidenceConfig.extracUmbralFallback` | `confidenceConfig.extracUmbralFallbackConfianza ?? confidenceConfig.extracUmbralFallback` |
| **3 — Modelo/Servidor** | `Classification:GptFallback:FallbackThreshold` | `Extraction:GptFallback:MinFieldsRatio` | `Extraction:GptFallback:MinFieldsRatio` |

> Regla de compatibilidad: en cada capa, el umbral legado (`umbral` / `extracUmbralFallback`) solo se usa cuando el específico del criterio está en `null`.

El orquestador resuelve el umbral efectivo antes de invocar cada actividad:

```csharp
// Clasificación (tipología no conocida todavía — solo niveles 1 y 4)
var umbralClasifFallback = entrada.Instrucciones.Classification.Umbral
    ?? _gptClasifSettings.FallbackThreshold;

// Clasificación — check BAJA_CONFIANZA (tipología ya resuelta — cadena completa)
var umbralBajaConfianza = entrada.Instrucciones.Classification.Umbral
    ?? tipologiaResuelta.ConfidenceConfig?.ClasifUmbralFallback
    ?? _gptClasifSettings.FallbackThreshold;

// Extracción — umbral legado (se pasa como UmbralFallbackEfectivo, nivel 3)
var umbralExtracFallback = entrada.Instrucciones.Extraction.Umbral
    ?? tipologiaResuelta.ConfidenceConfig?.ExtracUmbralFallback
    ?? _gptExtracSettings.MinFieldsRatio;

// Extracción — capa petición (específico si existe; si no, legado de la misma capa)
var umbralExtracCompletitudRequest = entrada.Instrucciones.Extraction.UmbralCompletitud
  ?? entrada.Instrucciones.Extraction.Umbral;
var umbralExtracConfianzaRequest   = entrada.Instrucciones.Extraction.UmbralConfianza
  ?? entrada.Instrucciones.Extraction.Umbral;
```

Dentro de `ConfigurableExtraerDataProvider.EsResultadoCuSuficiente`, la resolución final de cada criterio es:

```csharp
// Prioridad por capas: petición > tipología > modelo/servidor
umbralCompletitud = umbralFallbackCompletitudRequest      // nivel 1
    ?? confidenceConfig?.ExtracUmbralFallbackCompletitud   // nivel 2
  ?? umbralLegado                                        // legado tipología o modelo
  ?? _fallbackSettings.MinFieldsRatio;                   // último recurso

umbralConfianza = umbralFallbackConfianzaRequest           // nivel 1
    ?? confidenceConfig?.ExtracUmbralFallbackConfianza     // nivel 2
  ?? umbralLegado                                        // legado tipología o modelo
  ?? _fallbackSettings.MinFieldsRatio;                   // último recurso

return ratioCompletitud >= umbralCompletitud && confianzaCu >= umbralConfianza;
```

> El nivel de tipología para clasificación se usa en el check `BAJA_CONFIANZA_CLASIFICACION` pero no en el umbral de DI→GPT previo a la resolución de tipología, donde solo se usan los niveles 1 y 4.

### 5.2 Bloque `confidenceConfig`

Cada fichero `*.validation.json` puede incluir un bloque `confidenceConfig` opcional. Si no está, se usan los defaults:

```json
"confidenceConfig": {
  "clasifUmbralFallback": 0.85,
  "extracUmbralFallback": 0.9,
  "extracUmbralFallbackCompletitud": 0.9,
  "extracUmbralFallbackConfianza": 0.9,
  "extracWeightCampos": 0.5,
  "extracWeightRequeridos": 0.3,
  "extracWeightWarnings": 0.2,
  "umbralOK": 0.85,
  "umbralRevision": 0.70
}
```

| Parámetro | Descripción | Default |
|---|---|---|
| `clasifUmbralFallback` | Umbral de confianza DI para activar fallback GPT en clasificación, y también para el check `BAJA_CONFIANZA_CLASIFICACION` (cuando la petición no especifica umbral). | `0.85` |
| `extracUmbralFallback` | Umbral **legado**: aplica a completitud y confianza si los campos específicos no están informados en la tipología ni en la petición. Mantiene retrocompatibilidad. | `null` |
| `extracUmbralFallbackCompletitud` | Ratio mín. de campos esperados presentes (nivel tipología). Nivel 2 en la jerarquía de completitud. `null` = usar legado o global. | `null` |
| `extracUmbralFallbackConfianza` | Confianza CU mínima para no activar fallback (nivel tipología). Nivel 2 en la jerarquía de confianza. `null` = usar legado o global. | `null` |
| `extracWeightCampos` | Peso del promedio de confianzas de campo CU | `0.5` |
| `extracWeightRequeridos` | Peso del ratio de campos requeridos presentes | `0.3` |
| `extracWeightWarnings` | Peso de la penalización por warnings | `0.2` |
| `umbralOK` | `ConfianzaGlobal ≥ umbral` → EstadoCalidad = "OK" | `0.85` |
| `umbralRevision` | `ConfianzaGlobal ≥ umbral` → EstadoCalidad = "REVISION" | `0.70` |

> La suma `extracWeightCampos + extracWeightRequeridos + extracWeightWarnings` debe ser `1.0`. Si no, el resultado queda fuera del rango esperado (aunque se aplica `CLAMP` final).

---

## 6. Contrato de salida — campos expuestos

### 6.1 Resultado principal (`Resultado`)

```json
{
  "Estado": "VALIDACION_CON_ERRORES",
  "ConfianzaGlobal": 0.333,
  "EstadoCalidad": "ERROR",
  "ConfianzaClasificacion": 0.874,
  "ConfianzaExtraccion": 0.889,
  "ConfianzaValidacion": 0.333
}
```

### 6.2 Detalle de clasificación (`DetalleEjecucion.Clasificacion`)

```json
{
  "Modelo": "DocumentAICC_v0",
  "Confianza": 0.874,
  "ConfianzaDI": 0.874,
  "ConfianzaGPT": 0,
  "ProveedorClasif": "DocumentIntelligence",
  "FallbackLLM": false
}
```

### 6.3 Detalle de extracción (`DetalleEjecucion.Extraccion`)

```json
{
  "Modelo": "CU_NS_1.4_2",
  "ConfianzaExtraccion": 0.889,
  "ProveedorExtrac": "AzureContentUnderstanding",
  "FallbackUsado": false
}
```

### 6.4 Postproceso (`DetalleEjecucion.Postproceso`)

```json
{
  "Normalizaciones": ["Aplicadas 3 reglas de validacion", "Confianza de validacion: 33 %"],
  "Validaciones": ["[Warning] CuotaParticipacion: ..."],
  "Inconsistencias": ["[ERROR] Anejos: ...", "Total de errores: 2"],
  "ConfianzaValidacion": 0.333
}
```

---

## 7. Arquitectura del código

### 7.1 Ficheros involucrados

| Fichero | Capa | Rol |
|---|---|---|
| `DocumentIA.Core/Services/ConfidenceCalculator.cs` | Core | Cálculos puros, sin DI. Único punto de verdad matemático |
| `DocumentIA.Core/Models/ContratoSalida.cs` | Core | Campos nuevos en `ResultadoFinal`, `ResultadoClasificacion`, `ResultadoExtraccion`, `InformacionPostproceso` |
| `DocumentIA.Core/Models/ExtraccionModels.cs` | Core | `ConfianzaExtraccion`, `ProveedorExtrac`, `MetricasDebug` en `ExtraccionResultado` |
| `DocumentIA.Core/Models/ContratoEntrada.cs` | Core | `ConfiguracionIA.Umbral`, `UmbralCompletitud`, `UmbralConfianza` → `double?` (null = no especificado, usar jerarquía) |
| `DocumentIA.Core/Models/ClasificacionModels.cs` | Core | `UmbralFallbackEfectivo double?` en `ClasificacionInput` |
| `DocumentIA.Core/Models/ExtraccionModels.cs` | Core | `UmbralFallbackEfectivo`, `UmbralFallbackEfectivoCompletitud`, `UmbralFallbackEfectivoConfianza` `double?` en `ExtraccionInput` |
| `DocumentIA.Core/Configuration/TipologiaValidationConfig.cs` | Core | Clase `ConfidenceConfig` + propiedad en `TipologiaValidationConfig` + `ExtracUmbralFallback`, `ExtracUmbralFallbackCompletitud`, `ExtracUmbralFallbackConfianza` `double?` |
| `DocumentIA.Core/Configuration/ITipologiaVersionResolver.cs` | Core | `ConfidenceConfig` en `ResolvedTipologia` record |
| `DocumentIA.Core/Configuration/TipologiaVersionResolver.cs` | Core | Propagación de `ConfidenceConfig` al resolver |
| `DocumentIA.Functions/Services/AzureDocumentIntelligenceClasificarProvider.cs` | Functions | Extrae y asigna `ConfianzaDI`, `ProveedorClasif` |
| `DocumentIA.Functions/Services/GptClasificarDataProvider.cs` | Functions | Extrae `confianza` del JSON GPT, asigna `ConfianzaGPT`, `ProveedorClasif` |
| `DocumentIA.Functions/Services/ConfigurableClasificarDataProvider.cs` | Functions | Propaga `ConfianzaDI` cuando ejecuta fallback GPT |
| `DocumentIA.Functions/Services/AzureContentUnderstandingProvider.cs` | Functions | `TryExtractFieldConfidences()` + cálculo `ExtracCU()` + `MetricasDebug` |
| `DocumentIA.Functions/Services/GptFallbackExtraerDataProvider.cs` | Functions | Pide `confianza_extraccion` al modelo en el prompt; `BuildFallbackMetricas` devuelve `(double, ConfidenceMetricasExtraccion)`; `ConfianzaExtraccion = confianzaExtraccionGpt ?? confianzaCalculada`; `BuildFieldList` incluye hints de validación por campo (enum, regex, fecha, rango, dirección) |
| `DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs` | Functions | Resuelve jerarquía de umbrales (petición→tipología→config) en 3 puntos; agrega `Global()`, asigna `EstadoCalidad`, descompone campos en contrato |
| `DocumentIA.Functions/config/tipologias/*.validation.json` | Config | Bloque `confidenceConfig` con defaults explícitos |
| `DocumentIA.Tests.Unit/Services/ConfidenceCalculatorTests.cs` | Tests | 24 tests unitarios cubriendo todos los métodos y casos límite |

### 7.2 Principios de diseño

- **`ConfidenceCalculator` es estático y puro**: sin dependencias de DI, testeable de forma directa, sin efectos laterales.
- **Datos de confianza fluyen de actividad a orquestador**: cada provider rellena sus campos propios; el orquestador agrega al final, evitando acoplamiento entre actividades.
- **`EstadoCalidad` ≠ `Estado`**: `Estado` es el resultado del proceso (pudo completarse o no). `EstadoCalidad` es la fiabilidad de los datos, independiente del éxito del proceso.
- **Sin migración de BBDD**: `ConfianzaGlobal` ya estaba en la entidad. Los campos de descomposición se exponen solo en el JSON de salida.

---

## 8. Tests unitarios

Localización: `src/backend/DocumentIA.Tests.Unit/Services/ConfidenceCalculatorTests.cs`

| Grupo | Tests |
|---|---|
| `ClasifFinal` | Sin fallback usa DI; con fallback usa GPT; ambos null devuelve 0.5; fallback con null GPT usa DI |
| `ExtracCU` | Confianzas perfectas → 1.0; sin fieldConfs usa ratio campos; muchos warnings bajan score; campos requeridos ausentes bajan score; pesos custom aplicados |
| `ExtracGPT` | Null → 0.6; valor dado → ese valor; valor > 1 → clamped a 1 |
| `Validacion` | 0 errores → 1.0; todos errores → 0.0; 0 reglas → 1.0 |
| `Global` | Todos altos → mínimo; validación es cuello de botella; extracción null se omite |
| `EstadoCalidad` | ≥ 0.85 → OK; [0.70, 0.85) → REVISION; < 0.70 → ERROR; null config usa defaults |

Ejecutar:
```bash
dotnet test --filter "FullyQualifiedName~ConfidenceCalculator" \
    src/backend/DocumentIA.Tests.Unit/DocumentIA.Tests.Unit.csproj
```

---

## 9. Ejemplo real de ejecución (NS 1.4)

Documento: `2691_NS_2691.pdf` (Nota Simple)

| Etapa | Proveedor | Confianza |
|---|---|---|
| Clasificación | DocumentIntelligence | **0.874** |
| Extracción | AzureContentUnderstanding | **0.889** |
| Validación | Motor de reglas (2 errores de 3) | **0.333** |
| **Global** | MIN(0.874, 0.889, 0.333) | **0.333** |
| **EstadoCalidad** | 0.333 < 0.70 | **ERROR** |

Interpretación: la clasificación y extracción son buenas, pero 2 campos requeridos fallan validación. El documento necesita revisión manual antes de integrarse.
