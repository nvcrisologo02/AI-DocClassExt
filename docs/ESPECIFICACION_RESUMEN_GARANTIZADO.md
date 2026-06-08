# Especificación: Resumen Garantizado en Clasificación (AB#99754)

**Rama:** `feature/resumen-garantizado-impl` desde `develop`  
**Relacionado:** AB#99754  
**Versión Documento:** 1.0  
**Fecha:** 2026-06-08  

---

## 1. Contexto y Requisito

La funcionalidad **Resumen Garantizado** permite generar automáticamente un resumen ejecutivo de documentos clasificados cuando `GenerarResumenPorDefecto=true` en la solicitud de clasificación.

### Objetivo
- Garantizar que cada clasificación pueda venir acompañada de un resumen estructurado en 5 apartados
- Reutilizar configuración de prompts desde `appsettings.json` (PromptDefaults)
- Permitir override por tipología en `config/tipologias/<TIPOLOGIA>.validation.json`
- Mantener trazabilidad en `DatosExtraidos["Resumen"]`

---

## 2. Arquitectura Actual

### 2.1 Componentes Clave

#### a) **ConfiguracionClasificacion (Request Model)**
- **Ubicación:** `DocumentIA.Core.Models.ClasificacionModels.cs`
- **Campo:** `GenerarResumenPorDefecto: bool`
- **Semántica:** 
  - `true` → orquestador solicita resumen con prompts por defecto o custom
  - `false` → no se genera resumen automático (puede haber resumen ad-hoc via request)

#### b) **PromptDefaultsSettings (Configuration)**
- **Ubicación:** `DocumentIA.Core.Configuration.PromptConfig.cs` (línea 46)
- **Propósito:** Define valores por defecto de prompt para resumen garantizado
- **Fuente:** `appsettings.json` sección `"PromptDefaults"` (línea 101)

**Estructura:**
```csharp
public class PromptDefaultsSettings
{
    public string ModelKey { get; set; } = "default.gpt4o-mini";
    public string SystemPrompt { get; set; } = "Eres un analista documental experto...";
    public string UserPromptTemplate { get; set; } = "Genera un resumen ejecutivo...";
    public int MaxTokens { get; set; } = 1600;
    public double Temperature { get; set; } = 0.0;
    public string ContentMode { get; set; } = "markdown";
    
    public PromptConfig ToPromptConfig() => new() { /* ... */ };
}
```

#### c) **PromptConfig (Schema)**
- **Ubicación:** `DocumentIA.Core.Configuration.PromptConfig.cs` (línea 8)
- **Propósito:** Define un prompt ejecutable (modelo, system, user template, tokens, temp, modo contenido)
- **Placeholders soportados:**
  - `{contenido}` — markdown extraído o contenido del documento
  - `{campo:NombreCampo}` — valor de campo ya extraído (interpolación dinámica)

#### d) **GptClasificarDataProvider**
- **Ubicación:** `src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs`
- **Entry Point:** `ClasificarAsync()` → línea 73: `var resumenPrompt = ResolveResumenPrompt(input, contextoTexto);`
- **Método Clave:** `ResolveResumenPrompt()` (línea 272-290)
  - Valida `GenerarResumenPorDefecto` flag
  - Carga defaults desde `_promptDefaults`
  - Interpola template con contenido
  - Retorna `PromptConfig` listo para ejecución

#### e) **OpenAIPromptDataProvider**
- **Ubicación:** `src/backend/DocumentIA.Functions/Services/OpenAIPromptDataProvider.cs`
- **Método Clave:** `EjecutarPromptAsync()` — ejecuta prompt contra Azure OpenAI
- **Interpolación:** `InterpolateTemplate()` (línea 481)
  ```csharp
  internal static string InterpolateTemplate(
      string template,
      string contenido,
      Dictionary<string, object> datos)
  {
      // Sustituir {contenido}
      var result = template.Replace("{contenido}", contenido, StringComparison.OrdinalIgnoreCase);
      
      // Sustituir {campo:NombreCampo} usando regex
      result = CampoPlaceholderRegex.Replace(result, match => {
          var nombreCampo = match.Groups[1].Value;
          if (datos.TryGetValue(nombreCampo, out var valor) && valor is not null) {
              return valor.ToString() ?? string.Empty;
          }
          return string.Empty;
      });
      
      return result;
  }
  ```

### 2.2 Flujo de Resolución

```
ClasificacionInput (GenerarResumenPorDefecto=true)
    ↓
GptClasificarDataProvider.ResolveResumenPrompt()
    ├─ ¿GenerarResumenPorDefecto == true? → SÍ
    ├─ Cargar _promptDefaults.ToPromptConfig()
    ├─ ¿UserPromptTemplate no vacío? → SÍ
    ├─ Interpolar template (OpenAIPromptDataProvider.InterpolateTemplate)
    └─ return PromptConfig
        ↓
        OpenAIPromptDataProvider.EjecutarPromptAsync()
        ├─ Cargar override tipología (si existe)
        ├─ Resolver jerarquía: request > tipología > defaults
        ├─ Crear ChatClient
        ├─ Enviar a Azure OpenAI
        └─ Capturar respuesta → DatosExtraidos["Resumen"]
```

### 2.3 Jerarquía de Resolución de Prompts

**Prioridad (mayor a menor):**
1. **Request-level PromptConfig** (si viene en `PromptActivityInput`)
2. **Tipología Override** (de `config/tipologias/<TIPOLOGIA>.validation.json`)
3. **PromptDefaults** (de `appsettings.json`)
4. **Fallback:** Sin prompt si ninguno está configurado

---

## 3. Configuración por Defecto

### 3.1 appsettings.json

Ubicación: `src/backend/DocumentIA.Functions/appsettings.json` línea 101

```json
"PromptDefaults": {
  "ModelKey": "default.gpt4o-mini",
  "SystemPrompt": "Eres un analista documental experto. Responde en español de España, sin inventar información y siguiendo estrictamente el formato solicitado.",
  "UserPromptTemplate": "Genera un resumen ejecutivo del documento procesado siguiendo estrictamente estas instrucciones:\n\n- Idioma: Español (España)\n- Longitud máxima: 500 caracteres\n- No inventar información ni inferir datos no presentes en el documento\n- Ser claro, conciso y preciso\n- No utilizar frases genéricas ni vagas\n- Evitar redundancias\n- Priorizar información relevante para la toma de decisiones\n\nFormato obligatorio (mantener este orden y estructura):\n\n1. Objetivo del documento:\n   Describir brevemente la finalidad del documento\n\n2. Datos clave:\n   Enumerar los puntos más relevantes o información esencial\n\n3. Alertas:\n   Identificar riesgos, inconsistencias o aspectos críticos\n\n4. Acciones recomendadas:\n   Proponer actuaciones basadas únicamente en el contenido del documento\n\n5. Contenido:\n   Resumen general del contenido principal\n\nReglas adicionales:\n- Si algún apartado no tiene información suficiente, indicar exactamente: \"N/A\"\n- No completar apartados con suposiciones\n- No añadir información externa al documento\n- El resultado debe ser compacto, profesional y fácil de leer\n\nContenido del documento:\n{contenido}",
  "MaxTokens": 1600,
  "Temperature": 0.0,
  "ContentMode": "markdown"
}
```

### 3.2 Override por Tipología

Ubicación: `config/tipologias/<TIPOLOGIA>.validation.json`

Estructura esperada:
```json
{
  "TipologiaCode": "ESCR-06",
  "PromptConfig": {
    "Enabled": true,
    "ModelKey": "custom.gpt4-turbo",
    "SystemPrompt": "Eres un especialista en escrituras de dación en pago...",
    "UserPromptTemplate": "Para esta escritura de dación, genera resumen en 3 apartados principales...",
    "MaxTokens": 2000,
    "Temperature": 0.1,
    "ContentMode": "markdown"
  }
  // otros campos de la tipología...
}
```

---

## 4. Placeholders e Interpolación

### 4.1 Placeholders Soportados

| Placeholder | Fuente | Ejemplo | Resolución |
|---|---|---|---|
| `{contenido}` | Markdown extraído o PDF completo | `"# Escritura de dación..."` | Directo en template |
| `{campo:NombreCampo}` | `DatosExtraidos[NombreCampo]` | `{campo:FincaRegistral}` | Regex + lookup en dict |

### 4.2 Proceso de Interpolación

**Entrada:**
```
Template: "Finca: {campo:FincaRegistral}\nContenido: {contenido}"
Datos: { "FincaRegistral": "C-12345-A" }
Contenido: "# Escritura de dación en pago..."
```

**Proceso:**
1. Replace `{contenido}` → "# Escritura de dación en pago..."
2. Regex para `{campo:*}` → lookup en Datos dict
3. Si no existe campo → replace con ""

**Salida:**
```
Finca: C-12345-A
Contenido: # Escritura de dación en pago...
```

---

## 5. Ficheros a Revisar y Validar

### 5.1 Core y Configuration

| Archivo | Propósito | Líneas |
|---------|----------|--------|
| `DocumentIA.Core/Configuration/PromptConfig.cs` | Definición `PromptConfig` y `PromptDefaultsSettings` | 1-75 |
| `DocumentIA.Core/Models/ClasificacionModels.cs` | Flag `GenerarResumenPorDefecto` en `ClasificacionInput` | 16 |
| `DocumentIA.Core/Validation/TipologiaPromptConfigValidator.cs` | Validación de `PromptConfig` por tipología | 1-150 |

### 5.2 Services y Providers

| Archivo | Propósito | Líneas |
|---------|----------|--------|
| `DocumentIA.Functions/Services/GptClasificarDataProvider.cs` | Entry point `ResolveResumenPrompt()` | 272-290 |
| `DocumentIA.Functions/Services/OpenAIPromptDataProvider.cs` | Interpolación + ejecución (`InterpolateTemplate()`) | 481-510 |
| `DocumentIA.Functions/Services/GptFallbackExtraerDataProvider.cs` | Fallback extraction (reutiliza resumen) | 20-80 |

### 5.3 Dependency Injection y Configuration

| Archivo | Propósito | Líneas |
|---------|----------|--------|
| `DocumentIA.Functions/Program.cs` | DI registration `PromptDefaultsSettings` | 145 |
| `DocumentIA.Functions/appsettings.json` | Configuración `PromptDefaults` | 101-120 |
| `DocumentIA.Functions/config/tipologias/*.validation.json` | Override por tipología | PromptConfig section |

### 5.4 Orchestration

| Archivo | Propósito | Líneas |
|---------|----------|--------|
| `DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs` | Settlea `GenerarResumenPorDefecto` | 766, 1530 |

### 5.5 Tests (Crear/Validar)

| Archivo | Propósito | Prioridad |
|---------|----------|----------|
| `DocumentIA.Tests.Unit/Services/Classification/GptClasificarDataProviderTests.cs` | Tests `ResolveResumenPrompt()` | **CREAR** |
| `DocumentIA.Tests.Unit/Services/OpenAIPromptDataProviderTests.cs` | Tests `InterpolateTemplate()` | **REVISAR** |
| `DocumentIA.Tests.E2E/Classification/...` | E2E con `GenerarResumenPorDefecto=true` | **CREAR** |

---

## 6. Puntos de Extensión (Phase 2)

### 6.1 Interface IGenericSummaryGenerator

Propósito: Abstraer ejecución de resúmenes.

```csharp
public interface IGenericSummaryGenerator
{
    Task<string> GenerateSummaryAsync(
        string contenido,
        Dictionary<string, object> datosExtraidos,
        PromptConfig config,
        CancellationToken cancellationToken = default);
}
```

Implementaciones futuras:
- `AzureOpenAISummaryGenerator` (OpenAI)
- `AzureContentUnderstandingSummaryGenerator` (CU)
- `LocalLLMSummaryGenerator` (inference local)

### 6.2 Opciones Avanzadas

- Caching de resúmenes por `(tipología, hash(contenido))`
- Rate limiting y throttling
- Validación de contenido (detección spam)
- Multilingüismo
- Trazabilidad extendida en telemetría

---

## 7. Definition of Done

- [ ] Rama `feature/resumen-garantizado-impl` creada en develop
- [ ] `ResolveResumenPrompt()` en `GptClasificarDataProvider` implementado
- [ ] Interpolación de `{contenido}` en `OpenAIPromptDataProvider` funcional
- [ ] Interpolación de `{campo:*}` en `InterpolateTemplate()` funcional
- [ ] Tests unitarios: GptClasificarDataProviderTests
- [ ] Tests unitarios: OpenAIPromptDataProviderTests
- [ ] Tests E2E: smoke test con `GenerarResumenPorDefecto=true`
- [ ] Override por tipología probado
- [ ] TipologiaPromptConfigValidator validando PromptConfig
- [ ] Documentación actualizada (README / Manual de Explotación)
- [ ] Telemetría de ejecución (timing, errores)
- [ ] Code review y merge a develop

---

## 8. Referencias Relacionadas

- **AB#99754** — Resumen Garantizado en Clasificación
- **AB#99725** — Confianza dinámica self-reported GPT (feature/AB#99725-gpt-self-reported-confidence)
- **AB#99676** — Filtering by avoidConfidence (feature/99676-avoid-confidence)
- **Docs:** 03_DISENO_TECNICO_DETALLADO.md

---

## Histórico de Cambios

| Versión | Fecha | Cambios |
|---------|-------|---------|
| 1.0 | 2026-06-08 | Creación inicial. Especificación completa de arquitectura. |

