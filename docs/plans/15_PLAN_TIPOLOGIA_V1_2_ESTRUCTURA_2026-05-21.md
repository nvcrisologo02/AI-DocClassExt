# Plan de implementacion — Estructura Tipologia v1.2 (bloques GDC y Classification)

**Fecha:** 2026-05-21
**Estado:** Pendiente de inicio
**Rama:** `feature/99491-tipologia-v1-2-gdc-classification-blocks`
**ADO Feature:** feat: Estructura tipología v1.2 - bloques GDC y Classification jerárquicos (AB#99491)

---

## 1. Objetivo

Refactorizar el modelo JSON de tipologías para separar campos en bloques semánticos anidados:

- **`gdc.*`** — configuración de subida a GDC (tipoDocumento, subtipoDocumento, serie, matricula, skipUpload)
- **`classification.*`** — metadatos de clasificación IA (tdn1, tdn2 como _códigos_, gptDescripcion, enableRules)

Los campos raíz legacy (`gdcTipoDocumento`, `tdn1`, etc.) se mantienen como fallback mediante propiedades `Resolved*` en el modelo C#, garantizando compatibilidad total con tipologías existentes en BBDD sin migración de emergencia.

Adicionalmente, se elimina el código legacy de lectura de tipologías desde ficheros `.validation.json` en producción (solo aplica BBDD).

---

## 2. Plantilla JSON objetivo (v1.2)

Ver: `docs/auxiliares/tipologiaCompleta plantilla.json`

Cambios clave respecto a JSON anterior:
```
// ANTES (campos raíz planos)
{
  "gdcTipoDocumento": "NOTS",
  "tdn1": "NOTS",
  "gptDescripcion": "...",
  ...
}

// DESPUÉS (bloques anidados + legacy raíz como fallback)
{
  "gdc": {
    "tipoDocumento": "NOTS",
    "subtipoDocumento": "NOTS01",
    "serie": "AI01",
    "matricula": "AI-01-NOTS-01",
    "skipUpload": false
  },
  "classification": {
    "tdn1": "NOTS",
    "tdn2": "NOTS-01",
    "gptDescripcion": "...",
    "enableRules": true
  },
  // campos raíz legacy se mantienen durante migración (Resolved* hace el fallback)
  "gdcTipoDocumento": "NOTS",
  "tdn1": "NOTS",
  ...
}
```

---

## 3. Decisiones de diseño cerradas

| Decisión | Opción elegida | Motivo |
|---|---|---|
| **Compatibilidad backward** | Propiedades `Resolved*` con fallback a legacy | Tipologías existentes en BBDD no se rompen |
| **Eliminación legacy ficheros** | Sí, eliminar de producción | Solo aplica BBDD; tests migran a constructor testeable |
| **Descripciones TDN en prompt** | `CatalogoTdn1`/`CatalogoTdn2` como fuente de enriquecimiento | Infraestructura para clasificación futura por TDN1/TDN2 en fases |
| **`gptDescripcion`** | Convive con catálogo TDN | Es específica de la tipología; catálogo da contexto de familia |
| **`enableRules`** | Consumido por `DbTipologiaClassificationProfileProvider` | Si `false`, la tipología no entra al motor de señales |
| **Migración BBDD** | Script SQL idempotente con `JSON_MODIFY`/`JSON_VALUE` | Sin riesgo; backward compatible; campos raíz se mantienen |
| **Validación GDC en backend** | Usa `Resolved*` (acepta ambas estructuras) | Nueva plantilla es requisito para nuevas tipologías |

---

## 4. Tasks de implementación

### FASE 1 — Backend (DocumentIA.Core / DocumentIA.Functions)

#### Task B1 — Modelos C# base _(sin dependencias)_
**Archivos:** `DocumentIA.Core/Configuration/TipologiaValidationConfig.cs`, `ConfidenceConfig.cs`

- [ ] Añadir clase `GdcConfig` con: `SkipUpload`, `Matricula`, `TipoDocumento`, `SubtipoDocumento`, `Serie`
- [ ] Añadir clase `ClassificationTdnConfig` con: `Tdn1`, `Tdn2`, `GptDescripcion`, `EnableRules` (default `true`)
- [ ] Añadir propiedades `GdcConfig? Gdc` y `ClassificationTdnConfig? Classification` a `TipologiaValidationConfig`
- [ ] Marcar con `[Obsolete]` los campos raíz legacy: `SkipGDCUpload`, `TipologiaMGDCMatricula`, `GdcTipoDocumento`, `GdcSubtipoDocumento`, `GdcSerie`, `Tdn1`, `Tdn2`, `GptDescripcion`
- [ ] Añadir propiedades `[JsonIgnore]` con patrón fallback:
  - `ResolvedSkipGDCUpload => Gdc?.SkipUpload ?? SkipGDCUpload`
  - `ResolvedMatricula => Gdc?.Matricula ?? TipologiaMGDCMatricula`
  - `ResolvedGdcTipo => Gdc?.TipoDocumento ?? GdcTipoDocumento`
  - `ResolvedGdcSubtipo => Gdc?.SubtipoDocumento ?? GdcSubtipoDocumento`
  - `ResolvedGdcSerie => Gdc?.Serie ?? GdcSerie`
  - `ResolvedTdn1 => Classification?.Tdn1 ?? Tdn1`
  - `ResolvedTdn2 => Classification?.Tdn2 ?? Tdn2`
  - `ResolvedGptDescripcion => Classification?.GptDescripcion ?? GptDescripcion`
  - `ResolvedEnableRules => Classification?.EnableRules ?? true`
- [ ] `ConfidenceConfig.cs`: añadir `[JsonPropertyName("camelCase")]` a todas las propiedades; alinear defaults con plantilla (`ExtracWeightRequeridos=0.25`, `ExtracWeightWarnings=0.15`)

**Verificación crítica:** compilar sin errores; tipologías existentes en BBDD resuelven igual que antes via Resolved*.

---

#### Task B2 — Eliminación legacy ficheros _(depende B1)_
**Archivos:** `TipologiaVersionResolver.cs`, `TipologiaConfigLoader.cs`, tests asociados

- [ ] `TipologiaVersionResolver.cs`:
  - Eliminar constructor `TipologiaVersionResolver(string configBasePath)`
  - Eliminar campo `_configBasePath`, `_index` (Lazy)
  - Eliminar método `BuildIndex()`
  - Eliminar método `TryReadJsonString()`
  - Simplificar `GetIndex()`: ya solo llama `BuildIndexFromDatabase()` via cache
  - Añadir constructor interno (solo para tests): `internal TipologiaVersionResolver(Func<IEnumerable<TipologiaEntity>> entityFactory)`
- [ ] `TipologiaConfigLoader.cs`:
  - Eliminar constructor `TipologiaConfigLoader(string configBasePath)`
  - Eliminar `LoadConfig()` vía ficheros
  - Mantener solo el constructor DB y `LoadConfigFromDatabase()`
- [ ] `TipologiaVersionResolverTests.cs`: migrar todos los tests al constructor interno testeable (sin ficheros temp)
- [ ] `TipologiaConfigLoaderTests.cs`: migrar al constructor DB con mock de `ITipologiaRepository`

---

#### Task B3 — Resolvers + Validaciones _(depende B1, B2)_
**Archivos:** `TipologiaVersionResolver.cs`, `TipologiasAdminFunction.cs`

- [ ] `TipologiaVersionResolver.BuildIndexFromDatabase()`:
  - Sustituir asignaciones directas `config.GdcTipoDocumento`, `config.Tdn1`, etc. por `config.ResolvedGdcTipo`, `config.ResolvedTdn1`, etc.
  - Sustituir `config.SkipGDCUpload` por `config.ResolvedSkipGDCUpload`
  - Sustituir `config.TipologiaMGDCMatricula` por `config.ResolvedMatricula`
  - Sustituir `config.GptDescripcion` por `config.ResolvedGptDescripcion`
- [ ] `TipologiasAdminFunction.ValidateBusinessRules()`:
  - `config.SkipGDCUpload` → `config.ResolvedSkipGDCUpload`
  - `config.GdcTipoDocumento` → `config.ResolvedGdcTipo`
  - `config.GdcSerie` → `config.ResolvedGdcSerie`

---

#### Task B4 — Perfiles clasificación + PromptBuilder TDN _(depende B1)_
**Archivos:** `TipologiaClasificacionProfiles.cs`, `ClassificationTipologiaPromptBuilder.cs`

- [ ] `TipologiaClassificationConfig`: añadir campos:
  - `ExamplePhrases` (`List<string>`) con `[JsonPropertyName("examplePhrases")]`
  - `DisambiguationHints` (`List<string>`) con `[JsonPropertyName("disambiguationHints")]`
- [ ] `TipologiaClassificationProfile`: añadir `ExamplePhrases` y `DisambiguationHints` (`IReadOnlyList<string>`)
- [ ] `TipologiaClassificationProfile.FromDefinition()`: propagar ambos campos desde config
- [ ] `TipologiaClassificationDefinition`: añadir propiedad `Classification` (`ClassificationTdnDefinition?`) para leer `classification.enableRules`
  - Añadir `ClassificationTdnDefinition` con `EnableRules` (`bool`, default `true`)
- [ ] `DbTipologiaClassificationProfileProvider.TryBuildProfile()`:
  - Leer `enableRules` del bloque `classification` del JSON (via `TipologiaValidationConfig` o `TipologiaClassificationDefinition`)
  - Si `enableRules == false` → devolver `null` (la tipología no entra al motor de señales)
- [ ] `ClassificationTipologiaPromptBuilder.BuildFromDatabase()`:
  - JOIN con `CatalogoTdn1` y `CatalogoTdn2` para resolver nombre/descripción por código
  - Formato enriquecido: `- {codigo}: [{tdn1Nombre} / {tdn2Nombre}] {gptDescripcion}`
  - Usar `config.ResolvedGptDescripcion` y `config.ResolvedTdn1`/`ResolvedTdn2`
  - Infraestructura preparada para futura clasificación por TDN1-only (agrupación por familia)
- [ ] Actualizar uso de `ExamplePhrases` y `DisambiguationHints` en el prompt de rescate Foundry cuando estén informados

---

### FASE 2 — Frontend (DocumentIA.Admin)

#### Task F1 — Wizard + StateService _(paralelo con B3/B4)_
**Archivos:** `TipologiaWizardStateService.cs`, `TipologiaWizard.razor`

- [ ] `TipologiaWizardDraft`: añadir propiedades:
  - `string Tdn1`, `string Tdn2`
  - `bool EnableRules` (default `true`)
  - `string GptDescripcion`
  - `List<string> ExamplePhrases`
  - `List<string> DisambiguationHints`
- [ ] `BuildConfigurationJson()`:
  - Generar bloque `classification: { tdn1, tdn2, gptDescripcion, enableRules }` anidado
  - Generar bloque `gdc: { tipoDocumento, subtipoDocumento, serie, matricula, skipUpload }` anidado
  - Mantener campos raíz legacy redundantes para backward compat durante migración
- [ ] `InitFromSource()` / clonado: leer nuevos campos con fallback legacy (`config.Classification?.Tdn1 ?? config.Tdn1`)
- [ ] `TipologiaWizard.razor`:
  - Añadir **step Classification** (antes del step Extracción) con campos:
    - `tdn1`: input texto, label "Familia TDN1", placeholder "NOTS"
    - `tdn2`: input texto, label "Subtipo TDN2", placeholder "NOTS-01"
    - `gptDescripcion`: textarea "Descripción para clasificación IA" (hint: si vacía usa nombre)
    - `enableRules`: checkbox "Activar motor de reglas" (default: true)
  - **Step ClassificationConfig**: añadir listas editables para `examplePhrases` y `disambiguationHints`
  - **Step GDC**: actualizar binding a `Draft.GdcTipoDocumento` (nombre interno igual, genera `gdc.tipoDocumento` en JSON)
  - Actualizar validación wizard: `GdcTipoDocumento` sigue siendo obligatorio cuando `!SkipGdcUpload`

---

#### Task F2 — Edit + Detail _(paralelo con F1)_
**Archivos:** `TipologiaEdit.razor`, `TipologiaDetail.razor`

- [ ] `TipologiaEdit.razor`:
  - Leer/escribir campos GDC con fallback: `gdc?.tipoDocumento ?? gdcTipoDocumento`
  - Añadir campos `classification.*` si no los tiene (tdn1, tdn2, enableRules, gptDescripcion)
  - Al guardar, respetar estructura v1.2
- [ ] `TipologiaDetail.razor`:
  - Mostrar `gdc.tipoDocumento ?? gdcTipoDocumento` con fallback explícito
  - Mostrar `classification.tdn1 ?? tdn1`, `classification.tdn2 ?? tdn2`, `classification.gptDescripcion ?? gptDescripcion`
  - Añadir sección "Clasificación IA" mostrando `enableRules`

---

### FASE 3 — Migración BBDD

#### Task M1 — Script SQL idempotente
**Archivo:** `scripts/migrations/migrate-tipologias-v1_2-json-structure.sql`

- [ ] Crear script SQL `JSON_MODIFY`/`JSON_VALUE` para cada tipología publicada:
  - Añadir bloque `gdc` con valores de campos raíz legacy
  - Añadir bloque `classification` con valores de campos `tdn1`, `tdn2`, `gptDescripcion`
  - Dejar campos raíz intactos (idempotente: solo añade si `gdc` no existe aún)
- [ ] Verificación post-script:
  - `SELECT COUNT(*) FROM Tipologias WHERE JSON_VALUE(ConfiguracionJson,'$.gdc.tipoDocumento') IS NULL AND Activa=1`
  - Debe devolver 0 tras la migración
- [ ] Documentar ejecución en manual de explotación

---

### FASE 4 — Documentación _(al finalizar todas las fases)_

#### Task D1 — Actualización docs
**Archivos:** `docs/03_DISENO_TECNICO_DETALLADO.md`, `docs/05_MANUAL_USO_CONFIGURACION.md`, `docs/referencias/TIPOLOGIAS_REFERENCIA.md`, `docs/07_ROADMAP_PENDIENTES.md`

- [ ] `03_DISENO_TECNICO_DETALLADO.md`:
  - Actualizar diagrama de modelo de tipología con nueva estructura de bloques
  - Documentar `Resolved*` properties y patrón fallback
  - Documentar `ClassificationTipologiaPromptBuilder` con enriquecimiento TDN
- [ ] `05_MANUAL_USO_CONFIGURACION.md`:
  - Actualizar tabla de campos de tipología con estructura v1.2
  - Añadir sección "Migración de tipologías antiguas"
  - Describir wizard: nuevo step Classification
- [ ] `docs/referencias/TIPOLOGIAS_REFERENCIA.md` (si existe):
  - Eliminar referencias a ficheros `.validation.json` como mecanismo de alta operativo
  - Actualizar sección "Añadir nueva tipología": alta exclusivamente via wizard Admin
  - Anotar que el constructor de fichero fue eliminado del código de producción
  - Reflejar nueva estructura de bloques `gdc` y `classification`
- [ ] `docs/07_ROADMAP_PENDIENTES.md`:
  - Marcar esta Feature como "En progreso" / "Completado" según avance
  - Añadir nota sobre infraestructura TDN lista para clasificación futura por TDN1 en fases

---

## 5. Orden de ejecución y paralelismo

```
B1 ──────────────────────────────────────────────
     │                                           │
     ▼                                           ▼
     B2 → B3                                    B4
                                                │
                                                ▼
F1 (paralelo con B3/B4) ──── F2 (paralelo con F1)
│                             │
└─────────────────────────────┘
              │
              ▼
             M1 (último, cuando backend y frontend mergeados a develop)
              │
              ▼
             D1 (cierre)
```

| Orden | Task | Dependencias | Paralelizable |
|-------|------|--------------|---------------|
| 1 | B1 - Modelos C# | Ninguna | No (base) |
| 2 | B2 - Eliminar legacy ficheros | B1 | Sí con F1 |
| 3 | B3 - Resolvers + Validaciones | B1, B2 | Sí con B4 y F1 |
| 3 | B4 - Perfiles + PromptBuilder TDN | B1 | Sí con B3 y F1 |
| 3 | F1 - Wizard + StateService | Independiente del backend | Sí |
| 4 | F2 - Edit + Detail | F1 (misma estructura JSON) | Sí con F2 |
| 5 | M1 - Script SQL BBDD | Todo mergeado a develop | No |
| 6 | D1 - Documentación | Todo completado | No |

---

## 6. Criterios de aceptación

- [ ] Build limpio: 0 errores, 0 warnings nuevos en `DocumentIA.Functions`, `DocumentIA.Core`, `DocumentIA.Admin`
- [ ] Todos los tests unitarios pasan (`dotnet test`)
- [ ] Tipologías existentes en BBDD resuelven igual que antes (verificar via `ResolvedTdn1`, `ResolvedGdcTipo` en debug)
- [ ] Nueva tipología creada via wizard genera JSON con bloques `gdc` y `classification` anidados
- [ ] Tipología con `enableRules: false` no aparece en perfiles del motor de señales
- [ ] Script SQL de migración ejecutado sin errores; verificación post-script devuelve 0
- [ ] Documentación actualizada en docs/ correspondientes

---

## 7. Riesgos

| Riesgo | Probabilidad | Mitigación |
|--------|-------------|-----------|
| `Resolved*` no hace fallback correcto en algún path | Media | Test unitario explícito en B1 para cada Resolved* property |
| Tests de VersionResolver migrados rompen lógica de resolución | Media | Mantener cobertura equivalente; verificar casos límite (multi-version, isDefault) |
| `ConfidenceConfig` `[JsonPropertyName]` rompe deserialización existente | Baja | Los nombres camelCase deben coincidir exactamente con los JSON actuales en BBDD |
| Script SQL modifica JSON malformado y rompe tipología | Baja | Script verifica `JSON_VALUE IS NOT NULL` antes de modificar; rollback script preparado |

---

## 8. WIs Azure DevOps (por crear)

| Tipo | Título | Asignado | Ref |
|------|--------|---------|-----|
| Feature | feat: Estructura tipología v1.2 - bloques GDC y Classification jerárquicos | - | AB#99491 |
| Task | B1 - Modelos C# base: GdcConfig, ClassificationTdnConfig, Resolved* | - | AB#99492 |
| Task | B2 - Eliminar legacy ficheros: TipologiaVersionResolver + ConfigLoader + tests | - | AB#99493 |
| Task | B3 - Resolvers + Validaciones: usar Resolved* en VersionResolver y AdminFunction | - | AB#99494 |
| Task | B4 - Perfiles clasificación + PromptBuilder TDN: ExamplePhrases, enableRules, CatalogoTdn join | - | AB#99495 |
| Task | F1 - Frontend Wizard: step Classification + campos examplePhrases/disambiguationHints | - | AB#99496 |
| Task | F2 - Frontend Edit + Detail: leer/mostrar gdc.* y classification.* con fallback | - | AB#99497 |
| Task | M1 - Script SQL migración BBDD: JSON_MODIFY idempotente + verificación | - | AB#99498 |
| Task | D1 - Documentación: diseño técnico, manual configuración, referencia tipologías, roadmap | - | AB#99499 |
