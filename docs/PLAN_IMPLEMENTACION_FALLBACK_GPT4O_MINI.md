# Plan de Implementación: Fallback GPT-4o-mini para Extracción

**Rama:** `feature/fallback-gpt4o-mini`  
**Fecha:** 2026-03-25  
**Estado:** PENDIENTE DE IMPLEMENTACIÓN

---

## 1. Problema / Motivación

El proveedor actual de extracción (**Azure Content Understanding**) puede fallar o devolver resultados incompletos en varios escenarios:

| Escenario | Frecuencia esperada | Impacto |
|---|---|---|
| Error transitorio del servicio / timeout | Bajo | Alto — proceso sin datos extraídos |
| Analyzer no configurado para una tipología nueva | Medio | Alto — proceso sin datos extraídos |
| Documento de calidad baja (escaneo, rotación) | Medio | Medio — campos críticos nulos |
| Campos insuficientes extraídos vs. los esperados | Medio | Medio — contrato de salida incompleto |

**Objetivo:** incorporar GPT-4o-mini como capa de fallback que se active automáticamente cuando CU falle o cuando el porcentaje de campos extraídos sea inferior a un umbral configurable.

---

## 2. Alcance de esta rama

| Incluido | Excluido |
|---|---|
| Nuevo proveedor `GptFallbackExtraerDataProvider` | Cambios en lógica de clasificación |
| Settings `GptFallbackSettings` | Nuevas tipologías o analyzers CU |
| Integración en `ConfigurableExtraerDataProvider` | UI / portal de COMPLETAR_GDC_HTTP_BASIC_USERNAMEistración |
| Configuración por tipología en YAML | Migración de base de datos |
| Campo `FallbackUsado` en `ExtraccionResultado` | Ajuste de prompts por cada tipología |
| Trazabilidad en `ContratoSalida.Extraccion` | |
| Tests unitarios del nuevo proveedor | |

---

## 3. Arquitectura objetivo

```
IExtraerDataProvider
        │
        ▼
ConfigurableExtraerDataProvider          ← sin cambios en routing, solo añade fallback check
        │
        ├─── "azure-content-understanding" ──► AzureContentUnderstandingProvider  (primario)
        │                                              │ falla o campos insuficientes
        │                                              ▼
        │                                    GptFallbackExtraerDataProvider        (nuevo)
        │                                              │
        │                                              ▼
        │                                    Azure OpenAI gpt-4o-mini
        │
        └─── "mock" ──► MockExtraerDataProvider
```

### Flujo de decisión del fallback

```
1. Llamar a CU (primary provider)
   ├─ Excepción → log de error + IR A FALLBACK
   └─ Éxito:
       ├─ Campos extraídos ≥ MinFieldsRatio * camposEsperados → retornar resultado CU
       └─ Campos insuficientes → log de advertencia + IR A FALLBACK

FALLBACK:
   ├─ Construir prompt con definiciones de campos de la tipología
   ├─ Si CU devolvió markdown → adjuntar como contexto textual
   ├─ Si CU falló → enviar bytes del PDF (base64, modo visión)
   ├─ Llamar Azure OpenAI con JSON mode / Structured Outputs
   └─ Parsear respuesta y componer ExtraccionResultado con FallbackUsado = true
```

---

## 4. Cambios de código requeridos

### 4.1 Nuevo: `GptFallbackSettings.cs` (DocumentIA.Functions/Services)

```csharp
namespace DocumentIA.Functions.Services;

public class GptFallbackSettings
{
    /// <summary>Endpoint de Azure OpenAI (sin /openai/...).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>ApiKey o vacío si se usa DefaultAzureCredential.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>ApiKey | DefaultAzureCredential</summary>
    public string AuthMode { get; set; } = "ApiKey";

    /// <summary>Nombre del deployment en Azure OpenAI (p.ej. "gpt-4o-mini").</summary>
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    /// <summary>Versión de la API de Azure OpenAI.</summary>
    public string ApiVersion { get; set; } = "2024-08-01-preview";

    /// <summary>
    /// Fracción mínima de campos esperados que deben haberse extraído para
    /// considerar el resultado CU suficiente. Si hay < ratio*N campos → fallback.
    /// Rango [0.0-1.0]. 0.0 = fallback solo en excepción. 1.0 = siempre fallback.
    /// </summary>
    public double MinFieldsRatio { get; set; } = 0.5;

    /// <summary>Temperatura para GPT (0 = determinista).</summary>
    public double Temperature { get; set; } = 0.0;

    /// <summary>Máximo de tokens en respuesta GPT.</summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>Timeout en segundos para la llamada a OpenAI.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
```

### 4.2 Nuevo: `GptFallbackExtraerDataProvider.cs` (DocumentIA.Functions/Services)

Responsabilidades:
- Recibe el `ExtraccionInput`, la config de la tipología, y opcionalmente el markdown del resultado CU
- Construye el prompt del sistema con los campos definidos en `tipologiaConfig.Fields`
- Envía al modelo (gpt-4o-mini) en JSON mode
- Parsea la respuesta JSON y mapea a `Dictionary<string, object>`
- Devuelve `ExtraccionResultado` con `FallbackUsado = true` y tiempos de llamada

**Firma del método principal:**
```csharp
public Task<ExtraccionResultado> ObtenerDatosConFallbackAsync(
    ExtraccionInput input,
    string? markdownContexto,
    CancellationToken cancellationToken = default)
```

**Prompt de sistema (plantilla base):**
```
Eres un sistema de extracción de datos de documentos legales y registrales.
Se te proporciona el contenido de un documento de tipo "{tipologia}".
Extrae los siguientes campos y devuelve EXCLUSIVAMENTE un objeto JSON con esos campos.
Si un campo no aparece en el documento, devuelve null para ese campo.

CAMPOS A EXTRAER:
{lista de campos con nombre, descripción y ejemplos de valor}

FORMATO DE RESPUESTA (JSON):
{
  "Campo1": "valor o null",
  "Campo2": "valor o null",
  ...
}
```

**Nota:** Los valores siempre como string (normalización posterior igual que CU), excepto números y fechas que se normalizarán igual que en `ContentUnderstandingResultMapper`.

### 4.3 Modificar: `ExtraccionResultado.cs` (DocumentIA.Core/Models)

Añadir propiedad:
```csharp
/// <summary>Indica si se usó el proveedor de fallback (GPT) en lugar del primario (CU).</summary>
public bool FallbackUsado { get; set; }

/// <summary>Razón por la que se activó el fallback.</summary>
public string? FallbackRazon { get; set; }
```

### 4.4 Modificar: `ConfigurableExtraerDataProvider.cs`

Cambio principal: envolver la llamada a CU con lógica de fallback:

```csharp
public async Task<ExtraccionResultado> ObtenerDatosAsync(ExtraccionInput input, CancellationToken ct = default)
{
    var config = _tipologiaConfigLoader.LoadConfig(input.Tipologia);
    var providerName = ResolverProvider(config);

    if (providerName == "azure-content-understanding" && _fallbackEnabled)
    {
        ExtraccionResultado resultadoCU;
        string? markdownContexto = null;
        string? razonFallback = null;

        try
        {
            resultadoCU = await _azureProvider.ObtenerDatosAsync(input, ct);
            markdownContexto = resultadoCU.MarkdownExtraido;  // nuevo campo en ExtraccionResultado
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CU falló para {Tipologia}. Activando fallback GPT.", input.Tipologia);
            razonFallback = $"exception:{ex.GetType().Name}";
            resultadoCU = null!;
        }

        if (resultadoCU != null)
        {
            var camposEsperados = config.Fields.Count;
            var camposObtenidos = resultadoCU.DatosExtraidos.Count;
            var ratio = camposEsperados > 0 ? (double)camposObtenidos / camposEsperados : 1.0;

            if (ratio >= _fallbackSettings.MinFieldsRatio)
            {
                return resultadoCU;  // CU fue suficiente
            }
            razonFallback = $"insufficient_fields:{camposObtenidos}/{camposEsperados}";
            _logger.LogWarning("CU insuficiente ({Ratio:P0}) para {Tipologia}. Activando fallback GPT.", ratio, input.Tipologia);
        }

        var resultadoGpt = await _gptFallbackProvider.ObtenerDatosConFallbackAsync(input, markdownContexto, ct);
        resultadoGpt.FallbackUsado = true;
        resultadoGpt.FallbackRazon = razonFallback;
        return resultadoGpt;
    }

    return providerName switch
    {
        "azure-content-understanding" => await _azureProvider.ObtenerDatosAsync(input, ct),
        "mock" => await _mockProvider.ObtenerDatosAsync(input, ct),
        _ => throw new NotSupportedException(...)
    };
}
```

### 4.5 Modificar: `AzureContentUnderstandingProvider.cs`

Añadir extracción del markdown del resultado CU para que el fallback lo pueda reutilizar:

```csharp
// En ObtenerDatosAsync, tras parsear el resultado:
var markdownExtraido = TryExtractMarkdown(analysisDocument);

return new ExtraccionResultado
{
    ...
    MarkdownExtraido = markdownExtraido,  // nuevo campo
};
```

```csharp
private static string? TryExtractMarkdown(JsonDocument doc)
{
    // result.contents[0].markdown
    if (doc.RootElement.TryGetProperty("result", out var r)
        && r.TryGetProperty("contents", out var c)
        && c.ValueKind == JsonValueKind.Array && c.GetArrayLength() > 0
        && c[0].TryGetProperty("markdown", out var md)
        && md.ValueKind == JsonValueKind.String)
    {
        return md.GetString();
    }
    return null;
}
```

### 4.6 Modificar: `ExtraccionResultado.cs` (nuevo campo)

```csharp
/// <summary>Texto markdown extraído por CU (disponible para fallback GPT).</summary>
public string? MarkdownExtraido { get; set; }
```

### 4.7 Modificar: `ContratoSalida.cs`

Añadir en `SeccionExtraccion` (o equivalente):
```csharp
public bool FallbackGptUsado { get; set; }
public string? FallbackGptRazon { get; set; }
```

### 4.8 Modificar: `DocumentProcessOrchestrator.cs`

Propagar el flag de fallback desde `ExtraccionResultado` a `ContratoSalida`:
```csharp
if (resultadoExtraccion.FallbackUsado)
{
    salida.Extraccion.FallbackGptUsado = true;
    salida.Extraccion.FallbackGptRazon = resultadoExtraccion.FallbackRazon;
}
```

### 4.9 Modificar: `appsettings.json`

```json
{
  "Extraction": {
    "DefaultProvider": "mock",
    "AzureContentUnderstanding": { ... },
    "GptFallback": {
      "Endpoint": "",
      "ApiKey": "",
      "AuthMode": "ApiKey",
      "DeploymentName": "gpt-4o-mini",
      "ApiVersion": "2024-08-01-preview",
      "MinFieldsRatio": 0.5,
      "Temperature": 0.0,
      "MaxTokens": 2000,
      "TimeoutSeconds": 60
    }
  }
}
```

### 4.10 Modificar: `Program.cs`

```csharp
services.Configure<GptFallbackSettings>(context.Configuration.GetSection("Extraction:GptFallback"));
services.AddSingleton<GptFallbackExtraerDataProvider>();
// Actualizar registro de ConfigurableExtraerDataProvider para inyectar el nuevo proveedor
```

### 4.11 Nuevo (opcional): configuración por tipología

En los YAML de tipología, nuevo bloque:
```yaml
extraction:
  provider: azure-content-understanding
  fallback:
    enabled: true          # default: false (hereda la configuración global)
    minFieldsRatio: 0.6    # sobreescribe el global para esta tipología
```

Esto requiere extender `TipologiaExtractionConfig` con `FallbackConfig`.

---

## 5. Dependencias NuGet

| Paquete | Uso |
|---|---|
| `Azure.AI.OpenAI` | Cliente de Azure OpenAI (gpt-4o-mini) |
| `Azure.Identity` | DefaultAzureCredential (ya en uso para CU) |

Añadir al `DocumentIA.Functions.csproj`:
```xml
<PackageReference Include="Azure.AI.OpenAI" Version="2.*" />
```

---

## 6. Tests unitarios

### `GptFallbackExtraerDataProviderTests.cs`

| Test | Descripción |
|---|---|
| `FallbackActivado_CuException_RetornaResultadoGpt` | CU lanza excepción → fallback extrae datos |
| `FallbackActivado_CuInsuficiente_RetornaResultadoGpt` | CU < 50% campos → fallback activo |
| `FallbackNoActivado_CuSuficiente_RetornaResultadoCu` | CU ≥ 50% campos → no se llama GPT |
| `FallbackReutilizaMarkdown_CuParcial` | Si CU tuvo markdown, GPT lo recibe como contexto |
| `FallbackUsaPdf_CuSinMarkdown` | Si CU falló sin markdown, GPT recibe el PDF |
| `PromptContieneCamposTipologia` | El prompt incluye todos los campos de la config |
| `RespuestaGptMapeada_CamposMapeadosCorrectamente` | JSON de GPT se mapea a DatosExtraidos |
| `FallbackRazon_Exception_Registrada` | `FallbackRazon` contiene nombre de la excepción |
| `FallbackRazon_Insuficiente_Registrada` | `FallbackRazon` contiene ratios de campos |

---

## 7. Estrategia de prompt (detalle)

### 7.1 System message

```
Eres un extractor de datos especializados en documentos inmobiliarios y registrales españoles.
Tu tarea es extraer campos específicos del documento proporcionado.
Devuelve ÚNICAMENTE un objeto JSON válido, sin texto adicional.
Usa null para campos no encontrados.
```

### 7.2 User message (cuando hay markdown de CU)

```
Tipo de documento: {tipologia} ({descripcion})

CONTENIDO DEL DOCUMENTO:
{markdown}

EXTRAE estos campos:
{campos_json_schema}
```

### 7.3 User message (cuando el PDF se envía como imagen)

```
Tipo de documento: {tipologia} ({descripcion})
[imagen adjunta: PDF convertido a imagen(es)]

EXTRAE estos campos:
{campos_json_schema}
```

### 7.4 `campos_json_schema` — formato

```json
{
  "NombreComprador": { "tipo": "texto", "descripcion": "Nombre completo del comprador o adquirente", "ejemplo": "JUAN GARCÍA LÓPEZ" },
  "FechaEscritura": { "tipo": "fecha", "descripcion": "Fecha de firma de la escritura (formato DD/MM/YYYY)", "ejemplo": "15/03/2023" },
  "PrecioCompraventa": { "tipo": "número", "descripcion": "Precio total de la compraventa en euros", "ejemplo": "250000" }
}
```

---

## 8. Trazabilidad y observabilidad

- El campo `FallbackGptUsado: true` en `ContratoSalida` permite detectar qué documentos requirieron fallback
- Logs de nivel `Warning` al activar el fallback con la razón
- Métrica recomendada en Application Insights: `extraction.fallback.activated` con dimensión `razon` y `tipologia`
- El tiempo de la llamada GPT se incluye en `ExtraccionResultado.TiemposMs["gpt-fallback"]`

---

## 9. Orden de implementación recomendado

```
Paso 1 → GptFallbackSettings.cs                    [nuevo fichero, sin dependencias]
Paso 2 → ExtraccionResultado.cs                    [añadir FallbackUsado, FallbackRazon, MarkdownExtraido]
Paso 3 → AzureContentUnderstandingProvider.cs      [añadir TryExtractMarkdown + poblar MarkdownExtraido]
Paso 4 → GptFallbackExtraerDataProvider.cs          [nuevo fichero, corazón del fallback]
Paso 5 → ConfigurableExtraerDataProvider.cs         [integrar fallback en el routing]
Paso 6 → Program.cs                                [registrar dependencias]
Paso 7 → appsettings.json                          [añadir sección GptFallback]
Paso 8 → ContratoSalida.cs                         [FallbackGptUsado + FallbackGptRazon]
Paso 9 → DocumentProcessOrchestrator.cs             [propagar flag a salida]
Paso 10 → Tests unitarios                          [GptFallbackExtraerDataProviderTests.cs]
Paso 11 → Ajuste de prompts por tipología (iterativo, post-MVP)
```

---

## 10. Validación de la implementación

### Escenario 1: Fallback por excepción CU

1. Configurar CU con endpoint inválido en `local.settings.json`
2. Ingestar un documento con tipología `nota-simple`
3. Verificar en el log: `"CU falló para nota-simple. Activando fallback GPT."`
4. Verificar en la respuesta: `"FallbackGptUsado": true`, `"FallbackGptRazon": "exception:RequestFailedException"`
5. Verificar que los campos clave están extraídos

### Escenario 2: Fallback por campos insuficientes

1. Configurar `MinFieldsRatio: 0.9` (umbral muy alto)
2. Ingestar un documento que CU extrae parcialmente
3. Verificar en log: `"CU insuficiente (40%) para nota-simple. Activando fallback GPT."`
4. Verificar `"FallbackGptRazon": "insufficient_fields:4/10"`

### Escenario 3: Sin fallback (CU suficiente)

1. `MinFieldsRatio: 0.4` (umbral bajo)
2. CU extrae ≥ 40% campos
3. Verificar `"FallbackGptUsado": false` (o campo ausente por defecto `false`)
4. Verificar que GPT **NO** fue llamado (ausencia de log `"Activando fallback GPT"`)

---

## 11. Riesgos y mitigaciones

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| Coste de GPT-4o-mini por llamadas frecuentes | Media | `MinFieldsRatio` configurable; alertas de coste en Azure |
| Latencia adicional (CU + GPT en secuencia) | Alta si fallback frecuente | Logging de frecuencia; ajustar umbral o corregir CU |
| Alucinaciones de GPT en campos numéricos | Baja-Media | Validar con el mismo `ValidationEngine` ya existente |
| Tamaño del documento excede límite de GPT | Baja | Truncar markdown a N tokens; detectar y loguear |
| Prompt injection si el documento contiene instrucciones | Baja | usar JSON mode / Structured Outputs de Azure OpenAI |
