# Anexo: GptClasificarDataProvider — Arquitectura de Dos Fases

**Fecha**: Junio 2, 2026  
**Versión**: 1.0 (Final)  
**Estado**: PRODUCTION

---

## 1. Resumen Ejecutivo

`GptClasificarDataProvider` implementa un modelo jerárquico de clasificación de documentos del sector inmobiliario español (SAREB) mediante dos fases sucesivas de consulta a un modelo LLM (Azure OpenAI):

1. **Phase 1 (TDN1 Family Selection)**: Selecciona la familia de tipología (ej., ESCR = Escritura).
2. **Phase 2 (TDN2 Specific Type)**: Dentro de la familia, selecciona la tipología específica enriquecida (ej., ESCR-06 = Escritura: venta).

El catálogo de Phase 2 es **dinámico** y **enriquecido**, contiene metadatos desde la BD:
- `tipologiaNombre`: Nombre de la tipología
- `gptdescripcion`: Descripción experta para el LLM (contexto + criterio de clasificación automática)

---

## 2. Arquitectura de Dos Fases

### 2.1 Phase 1: Resolución de Familia TDN1

**Input**:
- `byte[] pdf`: Documento (binario o markdown)
- `ClasificacionTipologiaPromptBuilder`: Generador dinámico de catálogos

**Prompt**:
```
System: Eres un sistema experto en clasificación de documentos del sector inmobiliario español.
        Analiza el contenido del documento y clasifica según la familia TDN1 proporcionada.
User:   Documento: [contenido extraído]
        
        Tipologías disponibles (familia nivel 1):
        - ACTR: Acuerdos y transacciones
        - ACUE: Acuerdos externos
        - CEDU: Cédulas urbanísticas
        ...
        - ESCR: Escritura
        - SERE: Documentos de series
        
        Responde en JSON: {"tdn1": "CODIGO_O_NULL", "propuesta": "texto"}
```

**Response**:
```json
{
  "tdn1": "ESCR",
  "propuesta": "Documento de escritura notarial"
}
```

**Caching**: 
- Key: `clasificacion:catalogo:tdn1` (global, mismo para todas las peticiones)
- TTL: 5 minutos (via `IMemoryCache`)
- Comportamiento: Si TTL vence, se regenera catálogo desde `ClassificationTipologiaPromptBuilder.BuildTdn1Catalog()`

---

### 2.2 Phase 2: Resolución de Tipología Específica TDN2

**Precondición**: Phase 1 devolvió un `tdn1` válido.

**Input**:
- `tdn1Code`: Familia resuelta en Phase 1 (ej., "ESCR")
- `ClassificationTipologiaPromptBuilder.BuildTdn2CatalogByFamilia(tdn1Code)`

**Catalog Enrichment**:

Cada tipología en la familia aporta **cuatro campos**:

| Campo | Origen | Ejemplo |
|-------|--------|---------|
| `tipologiacodigo` | Tipologias.Codigo | ESCR-06 |
| `tipologiaNombre` | ConfiguracionJson.tipologiaNombre | Escritura: venta |
| `tdn2` | ConfiguracionJson.ResolvedTdn2 | ESCR-06 |
| `gptdescripcion` | ConfiguracionJson.gptdescripcion | "Tipo de documento: Escritura: venta. Descripción principal: Documento notarial que formaliza y da fe de la transmisión... Contexto descriptivo: ... Criterio para clasificación automática: asignar cuando..." |

**Prompt**:
```
System: Eres un sistema experto en clasificación de documentos del sector inmobiliario español,
        especialmente documentos de SAREB. Tu tarea es clasificar documentos en tipologías
        concretas dentro de una familia conocida.
User:   Documento clasificado preliminarmente en familia ESCR (Escritura).
        
        Tipologías disponibles en esta familia:
        - ESCR-01 [ESCR-01: Escritura: compraventa] Tipo de documento: Escritura: compraventa...
        - ESCR-02 [ESCR-02: Escritura: donación] Tipo de documento: Escritura: donación...
        - ESCR-06 [ESCR-06: Escritura: venta] Tipo de documento: Escritura: venta. Descripción principal...
        - ESCR-10 [ESCR-10: Escritura: hipoteca] Tipo de documento: Escritura: hipoteca...
        ...
        
        Regla fallback si el documento no encaja exactamente:
        - Si el contenido principal coincide con la descripción de una tipología, asignar esa clase.
        - Si hay ambigüedad, usar el contexto descriptivo para desambiguar.
        - Máximo 200 caracteres de justificación.
        
        Responde en JSON: {"tdn2": "CODIGO_TDN2"}
User:   Contenido: [markdown extraído del PDF]
```

**Response**:
```json
{
  "tdn2": "ESCR-06"
}
```

**Caching**:
- Key: `clasificacion:catalogo:tdn2:{tdn1_normalized}` (ej., `clasificacion:catalogo:tdn2:escr`)
- TTL: 5 minutos
- Regeneración: Consulta `ITipologiaRepository` y construye catálogo enriquecido bajo demanda

---

## 3. Flujo de Ejecución

```
ClasificarAsync(byte[] pdf, ClassificationConfig config)
    │
    ├─ PHASE 1
    │  ├─ Catálogo TDN1 = BuildTdn1Catalog() [IMemoryCache TTL 5min]
    │  ├─ Prompt Phase 1 = BuildTdn1Prompt(catalogo)
    │  ├─ LLM Response = AzureOpenAIClient.ChatCompletionsAsync(systemMsg, userMsg)
    │  ├─ Parse JSON = {"tdn1": "ESCR", "propuesta": "..."}
    │  └─ tdn1Code = "ESCR" ✓
    │
    ├─ PHASE 2 (si tdn1Code != null)
    │  ├─ Catálogo TDN2 = BuildTdn2CatalogByFamilia(tdn1Code) [IMemoryCache TTL 5min]
    │  │  └─ Query DB: SELECT * FROM Tipologias WHERE ResolvedTdn1 = tdn1Code AND IsPublished = 1 AND IsActive = 1
    │  │  └─ Enrich: Para cada tipología, extraer [tipologiacodigo, tipologiaNombre, tdn2, gptdescripcion]
    │  ├─ Prompt Phase 2 = BuildTdn2Prompt(tdn1Code, catalogo)
    │  ├─ LLM Response = AzureOpenAIClient.ChatCompletionsAsync(systemMsg, userMsg)
    │  ├─ Parse JSON = {"tdn2": "ESCR-06"}
    │  └─ tdn2Code = "ESCR-06" ✓
    │
    └─ RESOLUTION
       ├─ ResolveTipologiaByTdn2(tdn2Code) = Tipologia { ResolvedTdn2 = "ESCR-06", ... }
       └─ return ClasificacionResultado { TipologiaId, Confianza, CustomData }
```

---

## 4. Flujo de Fallback Completo (HybridTdnClasificarProvider)

`GptClasificarDataProvider` es el **cuarto y último** eslabón en la cadena de fallback:

```
ClasificarActivity.ClasificarAsync()
    │
    ├─ 1. RuleBasedTdnClassifier (reglas deterministas)
    │     ├─ Si matchea regla → return resultado ✓
    │     └─ Si no → fallback
    │
    ├─ 2. DocumentIntelligenceProvider (Azure DI)
    │     ├─ Si confianza > umbral → return resultado ✓
    │     └─ Si no → fallback
    │
    ├─ 3. FoundryTdnRescueClassifier (Foundry LLM)
    │     ├─ Si timeout o error → fallback
    │     ├─ Si resultado → retorna
    │     └─ Si no satisfactorio → fallback
    │
    └─ 4. GptClasificarDataProvider (Azure OpenAI, two-phase)
          ├─ Phase 1: TDN1 family
          ├─ Phase 2: TDN2 enriquecido (NEW)
          └─ return resultado final ✓
```

---

## 5. Manejo de Errores y Propiedades Faltantes

### 5.1 ConfiguracionJson Incompleto

En algunos casos, `ConfiguracionJson` no contiene todas las propiedades esperadas:

```json
{
  "gdc": {...},
  "tdn1": "ESCR",
  // falta: tipologiaNombre
  // falta: gptdescripcion
}
```

**Solución** (en PowerShell script `Backup-Actualizar-Tipologias.ps1`):
```powershell
# Crear propiedades si no existen
if (-not ($json.PSObject.Properties.Name -contains 'tipologiaNombre')) {
    $json | Add-Member -MemberType NoteProperty -Name 'tipologiaNombre' -Value $null -Force
}
if (-not ($json.PSObject.Properties.Name -contains 'gptdescripcion')) {
    $json | Add-Member -MemberType NoteProperty -Name 'gptdescripcion' -Value $null -Force
}
```

### 5.2 Fallback en Deserialization

En `TipologiaVersionResolver`:
```csharp
TipologiaNombre: config.TipologiaNombre ?? (config.ResolvedTdn2 ?? string.Empty),
```

Si `tipologiaNombre` está ausente, usa `ResolvedTdn2` (ej., "ESCR-06") como fallback.

---

## 6. Modelo de Resolución de LLM

**Resolución Dinámica**:
- `instrucciones.classification.model` (si viene explícito y válido)
- `auto` o vacío → fallback a modelo marcado `useAsFallback=true` en BD

**Ventajas**:
- Sin lock-in de deployment
- Cada petición resuelve dinámicamente
- Cliente de chat creado por petición (no reutilizado)

---

## 7. Propiedades de ConfiguracionJson Ahora Pobladas

| Propiedad | Tipo | Origen | Propósito |
|-----------|------|--------|----------|
| `tipologiaNombre` | string | BD (Tipologias.Nombre) | Nombre humano de la tipología |
| `gptdescripcion` | string | CSV + BD | Descripción experta para el LLM |
| `ResolvedTdn1` | string | ConfiguracionJson | Familia TDN1 (ej., "ESCR") |
| `ResolvedTdn2` | string | ConfiguracionJson | Código TDN2 (ej., "ESCR-06") |

**Población**:
- `Backup-Actualizar-Tipologias.ps1` (script 2026-06-02)
  - Pobladas **204 tipologías**
  - Backup: `scripts/backups/Tipologias_Backup_20260602_125111.sql`
  - Reporte: `scripts/backups/Reporte_20260602_125111.txt`

---

## 8. Testing E2E

### 8.1 Validación de Catálogos

**DRY RUN**:
```powershell
.\scripts\Backup-Actualizar-Tipologias.ps1 -DryRun
# Resultado: 204 tipologías procesadas ✓
```

**BD Verification**:
```powershell
.\scripts\Verificar-Cambios-BD.ps1
# Resultado: 204 tipologías con tipologiaNombre en ConfiguracionJson ✓
```

### 8.2 E2E Classification

```powershell
.\smoke_e2e.ps1  # o notasimple1-4 test
```

**Esperado**:
- Phase 1 ejecutada (TDN1 resuelto)
- Phase 2 ejecutada con catálogo enriquecido
- Output contiene `Identificacion.TipologiaNombre` poblado

---

## 9. Diagrama de Catálogo Enriquecido

```
TDN1: ESCR (Escritura)
  │
  ├─ ESCR-01: Escritura: compraventa
  │   ├─ tipologiaNombre: "Escritura: compraventa"
  │   ├─ tdn2: "ESCR-01"
  │   └─ gptdescripcion: "Tipo de documento: Escritura: compraventa..."
  │
  ├─ ESCR-06: Escritura: venta
  │   ├─ tipologiaNombre: "Escritura: venta"
  │   ├─ tdn2: "ESCR-06"
  │   └─ gptdescripcion: "Tipo de documento: Escritura: venta..."
  │
  └─ ESCR-10: Escritura: hipoteca
      ├─ tipologiaNombre: "Escritura: hipoteca"
      ├─ tdn2: "ESCR-10"
      └─ gptdescripcion: "Tipo de documento: Escritura: hipoteca..."
```

---

## 10. Próximos Pasos

1. ✅ **Base de datos poblada** (2026-06-02)
2. ⏳ **E2E Testing**: Validar TipologiaNombre en output
3. ⏳ **Performance Tuning**: Monitorear cache hits y latencia Phase 1/2
4. ⏳ **Production Monitoring**: Observabilidad de dos fases en AppInsights

---

## 11. Referencias

- **Código**: `src/backend/DocumentIA.Core/Configuration/ClassificationTipologiaPromptBuilder.cs`
- **Código**: `src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs`
- **Script**: `scripts/Backup-Actualizar-Tipologias.ps1`
- **Documento Principal**: `docs/03_DISENO_TECNICO_DETALLADO.md` (Sección 3.1 - Anexo integrado)

---

**Versión**: 1.0 — Final  
**Próxima revisión**: Después de E2E testing en producción
