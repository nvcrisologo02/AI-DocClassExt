# 12. Guía de Migración v1.4 — PromptGPT Deprecation & ConfiguracionJson Refactor

> **Última actualización:** 2026-06-04  
> **Versión:** 1.0  
> **Proyecto:** AI DocClassExt — SAREB  
> **Alcance:** Cambios Fase 2 (AB#99732 TipologiaCleanup epic)

---

## Resumen Ejecutivo

La versión **v1.4** introduce cambios importantes en cómo se configura la información de prompts en Tipologías:

| Aspecto | Antes (v1.3) | Después (v1.4) |
|--------|-------------|----------------|
| **Campo de prompts** | `Tipologia.PromptGPT` (columna directa) | `ConfiguracionJson.PromptConfig.SystemPrompt` (anidado) |
| **Acceso recomendado** | Lectura directa del campo | Extension methods (safe accessors) |
| **Conflictos** | No controlados | Validados y detectados |
| **Performance** | 1 lectura + 1 parse JSON | 1 lectura + 1 parse JSON (optimizado) |
| **Línea de tiempo** | — | v1.4: Deprecation (leer/escribir), v1.5: ReadOnly, v2.0: Removido |

---

## ¿Por Qué Este Cambio?

### Problema Original (v1.3)

La información de prompts existía en **dos lugares**:
```
Tipologias.PromptGPT                              (redundante)
Tipologias.ConfiguracionJson.PromptConfig         (canónico, pero no siempre usado)
```

**Consecuencias:**
- 🔴 **Inconsistencia de datos:** Prompts diferentes en ambos campos
- 🔴 **Complejidad del schema:** Dos rutas de acceso para lo mismo
- 🔴 **Dificultad en migraciones:** Scripts tenían que sincronizar ambos campos
- 🔴 **Deuda técnica:** Código duplicado en mappers y DTOs

### Solución (v1.4)

**Single source of truth:**
```
Tipologias.ConfiguracionJson.PromptConfig         (ÚNICA FUENTE)
Tipologias.PromptGPT                              (deprecated, legacy only)
```

**Ventajas:**
- ✅ **Consistencia:** Un único lugar de verdad
- ✅ **Claridad:** Código más fácil de entender
- ✅ **Mantenibilidad:** Menos rutas de acceso
- ✅ **Preparación para v2.0:** Facilita eliminación futura

---

## Impacto Por Rol

### Para Desarrolladores Backend

**❌ Código que NO debe escribirse en v1.4:**
```csharp
var prompt = tipologia.PromptGPT;                      // DEPRECATED
tipologia.PromptGPT = "new value";                     // DEPRECATED
```

**✅ Código correcto en v1.4:**
```csharp
// Opción 1: Extension method (recomendado)
var prompt = tipologia.GetSystemPrompt();              // ✓ SAFE
var template = tipologia.GetUserPromptTemplate();      // ✓ SAFE

// Opción 2: Acceso estructurado (alternativa)
var config = tipologia.GetValidationConfig();
if (config?.promptConfig != null) 
{
    var prompt = config.promptConfig.systemPrompt;    // ✓ SAFE
}

// ❌ NO HAGAS ESTO:
// tipologia.PromptGPT = "value"                       // Compilará error

// Mapeo de DTOs
var dto = TipologiaMapper.ToResponseDto(tipologia);    // Usa extension methods internamente
```

**Cambios en servicios principales:**

| Servicio | v1.3 | v1.4 | Efecto |
|----------|------|------|--------|
| `GptFallbackExtraerDataProvider` | Acceso directo a .PromptGPT | Usa `GetSystemPrompt()` | ✓ Refactorizado (-350 LOC) |
| `TipologiasAdminFunction` | DTO incluye .PromptGPT | DTO omite .PromptGPT | ✓ DTOs limpios |
| `TipologiaConfigurationCache` | 2 TTLs (Prompt + Config) | 1 TTL unificado | ✓ Más simple |
| `TipologiaMapper` | Copia ambos campos | Copia solo ConfiguracionJson | ✓ Migración automática |

---

### Para Administrativos / Operaciones

**Cambio en interfaz de administración:**

Cuando crees o edites una Tipología, **el campo `PromptGPT` ya no aparecerá** en el formulario web.

**Antes (v1.3):**
```
┌─────────────────────────────────────────┐
│ CREAR TIPOLOGÍA                         │
├─────────────────────────────────────────┤
│ Código:            [  tasacion      ]   │
│ Nombre:            [  Tasación      ]   │
│ PromptGPT:         [  You are a... ]    │ ← AQUÍ EDITABAS DIRECTO
│ ConfiguracionJson: [  {...json...} ]    │
│ ...                                     │
└─────────────────────────────────────────┘
```

**Después (v1.4):**
```
┌─────────────────────────────────────────┐
│ CREAR TIPOLOGÍA                         │
├─────────────────────────────────────────┤
│ Código:            [  tasacion      ]   │
│ Nombre:            [  Tasación      ]   │
│ ConfiguracionJson: [  {...json...} ]    │
│   ├─ promptConfig:                      │
│   │  ├─ systemPrompt: [You are a...]   │ ← AQUÍ AHORA
│   │  └─ userPromptTemplate: [...]      │
│   ├─ classification: {...}             │
│   └─ validation: {...}                 │
│ ...                                     │
└─────────────────────────────────────────┘
```

**Procedimiento para editar prompts:**
1. Abre la Tipología en administración
2. Edita `ConfiguracionJson` → `promptConfig` → `systemPrompt`
3. Guarda (la API valida automáticamente)

---

### Para Consumidores de API

**Cambio en respuesta HTTP:**

**Antes (v1.3):**
```json
{
  "id": 1,
  "codigo": "tasacion",
  "promptGPT": "You are a tasacion expert...",
  "configuracionJson": {
    "promptConfig": {
      "systemPrompt": "You are a tasacion expert..."
    }
  }
}
```

**Después (v1.4):**
```json
{
  "id": 1,
  "codigo": "tasacion",
  "configuracionJson": {
    "promptConfig": {
      "systemPrompt": "You are a tasacion expert..."
    }
  }
}
```

**Impacto:**
- ✓ Si tu código ya ignora `.promptGPT`, **no hay cambio**
- ⚠ Si tu código DEPENDE de `.promptGPT`, **actualizará en v1.5** (read-only) y **eliminará en v2.0** (breaking)

**Recomendación para clientes:**
```csharp
// ❌ EVITA
var prompt = response.PromptGPT;                    // Déjalo para v1.5

// ✅ MEJOR
var prompt = response.ConfiguracionJson
    ?.promptConfig
    ?.systemPrompt 
    ?? "fallback prompt";
```

---

## Estructura de ConfiguracionJson v1.4

### Formato Canónico

```json
{
  "promptConfig": {
    "systemPrompt": "Eres un experto en tasaciones inmobiliarias...",
    "userPromptTemplate": "Analiza el siguiente documento:\n{documento}"
  },
  "classification": {
    "tdn1": "Documento de titularidad",
    "tdn2": "Tasación"
  },
  "validation": {
    "fields": [
      {
        "name": "valor_tasacion",
        "type": "decimal",
        "required": true,
        "pattern": "^\\d+(\\.\\d{2})?$"
      }
    ]
  },
  "gdc": {
    "docType": "TASACION",
    "riskLevel": "NORMAL"
  },
  "assetResolver": {
    "enabled": true,
    "criteria": ["IDUFIR", "RefCatastral"]
  }
}
```

### Secciones

| Sección | Requerida | Propósito | Notas |
|---------|-----------|----------|-------|
| `promptConfig` | ✓ | Prompts GPT | Antes: campo .PromptGPT |
| `classification` | ✓ | Metadata TDN | Categorización |
| `validation` | ✗ | Reglas de validación | Opcional si no se valida |
| `gdc` | ✗ | Configuración GDC | Si se integra GDC |
| `assetResolver` | ✗ | Plugin asset resolver | Si está habilitado |

---

## Extension Methods: Cómo Usarlos

### En Código Backend

**Ubicación:** `src/backend/DocumentIA.Data/Extensions/TipologiaEntityExtensions.cs`

```csharp
public static class TipologiaEntityExtensions
{
    /// <summary>
    /// Extrae el systemPrompt de ConfiguracionJson.
    /// Retorna string.Empty si no existe.
    /// </summary>
    public static string GetSystemPrompt(this TipologiaEntity entity)
    {
        var config = GetValidationConfig(entity);
        return config?.promptConfig?.systemPrompt ?? string.Empty;
    }

    /// <summary>
    /// Extrae el userPromptTemplate de ConfiguracionJson.
    /// </summary>
    public static string GetUserPromptTemplate(this TipologiaEntity entity)
    {
        var config = GetValidationConfig(entity);
        return config?.promptConfig?.userPromptTemplate ?? string.Empty;
    }

    /// <summary>
    /// Parsea ConfiguracionJson completo.
    /// Retorna null si JSON es inválido.
    /// </summary>
    public static TipologiaValidationConfig? GetValidationConfig(this TipologiaEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.ConfiguracionJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TipologiaValidationConfig>(
                entity.ConfiguracionJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }
        catch
        {
            return null;
        }
    }
}
```

**Uso:**
```csharp
var tipologia = await tipologiaRepository.GetByIdAsync(1);

// ✓ CORRECTO
string systemPrompt = tipologia.GetSystemPrompt();
string userTemplate = tipologia.GetUserPromptTemplate();

// ✗ COMPILARÁ ERROR (no existe en tiempo de compilación)
// string oldPrompt = tipologia.PromptGPT;
```

---

## Impacto en Pruebas

### Cambios en Test Fixtures

**Antes (v1.3):**
```csharp
var tipologia = new TipologiaEntity
{
    Id = 1,
    Codigo = "tasacion",
    PromptGPT = "You are a tasacion expert...",     // ❌ IGNORADO EN v1.4
    ConfiguracionJson = "{...}" 
};
```

**Después (v1.4):**
```csharp
var tipologia = new TipologiaEntity
{
    Id = 1,
    Codigo = "tasacion",
    PromptGPT = null,                               // ✓ NULL
    ConfiguracionJson = @"{
        ""promptConfig"": {
            ""systemPrompt"": ""You are a tasacion expert...",
            ""userPromptTemplate"": ""...""
        },
        ""classification"": {...}
    }"
};
```

### Test Cases

```csharp
[Fact]
public void GetSystemPrompt_ReturnsPromptFromConfigJson()
{
    // ARRANGE
    var tipologia = new TipologiaEntity
    {
        ConfiguracionJson = @"{""promptConfig"": {""systemPrompt"": ""Test prompt""}}"
    };

    // ACT
    var result = tipologia.GetSystemPrompt();

    // ASSERT
    Assert.Equal("Test prompt", result);
}

[Fact]
public void GetSystemPrompt_ReturnsEmptyStringIfNoConfigJson()
{
    // ARRANGE
    var tipologia = new TipologiaEntity { ConfiguracionJson = null };

    // ACT
    var result = tipologia.GetSystemPrompt();

    // ASSERT
    Assert.Empty(result);
}
```

---

## Migración de Datos (Dry-Run Results)

### Análisis de la base de datos actual

```
Total Tipologías: 204
├─ Con datos en .PromptGPT: 204 (100%)
├─ Con ConfiguracionJson: 204 (100%)
│
├─ CONFLICTING (diferente valor en ambos campos): 187 (92%)
│  └─ Requieren revisión manual para decidir qué valor mantener
│
├─ ASYMMETRIC (uno lleno, otro vacío): 17 (8%)
│  ├─ .PromptGPT lleno + ConfigJson vacío: 11 cases (MIGRABLES)
│  └─ Ambos vacíos: 6 cases (sin acción)
│
└─ READY_FOR_MIGRATION (valores idénticos): 0 (0%)
```

### Decisión de Fase 2

**Status:** SKIP automated migration  
**Razón:** 187 conflictos (92%) requieren contexto de cliente para decidir

**Recomendación:** 
En v1.5 o superior, implementar script de migración asistida con:
1. Backup automático de base de datos
2. Copia de .PromptGPT → ConfiguracionJson para los 11 casos asimétricos
3. Reporte de los 187 conflictos para revisión manual
4. Opción de mantener .PromptGPT como read-only (no eliminar aún)

---

## Línea de Tiempo de Deprecation

### v1.4 (Actual) — DEPRECATION NOTICE

**Estado del campo .PromptGPT:**
- ✓ Lectura: Soportada (pero no recomendada)
- ✓ Escritura: Soportada (pero no recomendada)
- ⚠ APIs devuelven campo en respuesta (para compatibilidad)
- ✓ Código nuevo usa extension methods automáticamente
- ✓ Tests validan ambas rutas

**Acciones requeridas:**
1. Actualizar código backend existente para usar `GetSystemPrompt()`
2. Revisar integraciones que consumen la API para usar `ConfiguracionJson`
3. No iniciar nuevos campos basados en `.PromptGPT`

**Línea de tiempo:**
```
v1.4 (Jun 2026)      v1.5 (Jun 30)      v2.0 (Jul 31)
│                    │                   │
├─ DEPRECATED        ├─ READ-ONLY        ├─ REMOVED ⚠️
├─ Read/Write OK     ├─ Write ERROR      ├─ BREAKING CHANGE
├─ Warnings emitted  ├─ Migration tools  ├─ DB column dropped
└─ Full support      └─ Transition window└─ v1.x NOT compatible
```

### v1.5 (Junio 30, 2026) — READ-ONLY

**Estado del campo .PromptGPT:**
- ✓ Lectura: Soportada (con warning)
- ✗ Escritura: ERROR (422 Unprocessable Entity)
- ⚠ APIs devuelven campo pero marcan como read-only
- ✓ Migration tools disponibles

**Acciones requeridas:**
1. Ejecutar migration tools (script de migración asistida)
2. Actualizar integraciones para no escribir .PromptGPT
3. Testing end-to-end con ConfiguracionJson

### v2.0 (Julio 31, 2026) — REMOVED ⚠️ BREAKING

**Estado del campo .PromptGPT:**
- ✗ Lectura: N/A (columna no existe)
- ✗ Escritura: N/A (columna no existe)
- ✗ APIs no devuelven campo
- ✓ Schema limpio

**Impacto:**
- 🔴 **BREAKING CHANGE:** Código que accede .PromptGPT falla
- 🔴 **v1.x deployments incompatibles** con v2.0 database
- ✓ Pero: Todos los clientes migraron en v1.5

---

## Guía de Actualización por Escenario

### Escenario A: Consumidor HTTP de la API

**Task:** Actualizar cliente que consume GET `/api/admin/tipologias/1`

**Paso 1: Detectar dependencia**
```csharp
var tipologia = await client.GetAsync("/api/admin/tipologias/1");
var prompt = tipologia.promptGPT;                    // ← AQUÍ
```

**Paso 2: Cambiar referencia**
```csharp
var tipologia = await client.GetAsync("/api/admin/tipologias/1");
var prompt = tipologia.configuracionJson?.promptConfig?.systemPrompt ?? "";
```

**Paso 3: Test**
```csharp
[Fact]
public async Task GetTipologia_ReadSystemPromptFromConfigJson()
{
    var response = await _httpClient.GetAsync("/api/admin/tipologias/1");
    var json = await response.Content.ReadAsAsync<TipologiaDto>();
    
    var prompt = json.ConfiguracionJson?.PromptConfig?.SystemPrompt;
    Assert.NotNull(prompt);
}
```

### Escenario B: Servicio Interno Backend

**Task:** Migrar `ExtraerDataProvider` que usa `.PromptGPT` directamente

**Antes (v1.3):**
```csharp
public class GptFallbackExtraerDataProvider : IExtraerDataProvider
{
    public async Task<ExtraccionResultado> ExecuteExtractionAsync(...)
    {
        // ❌ DEPRECATED en v1.4
        var systemPrompt = tipologia.PromptGPT;     
        
        // Construir prompt...
    }
}
```

**Después (v1.4):**
```csharp
public class GptFallbackExtraerDataProvider : IExtraerDataProvider
{
    public async Task<ExtraccionResultado> ExecuteExtractionAsync(...)
    {
        // ✓ RECOMENDADO
        var systemPrompt = tipologia.GetSystemPrompt();
        
        // Construir prompt...
    }
}
```

**Cambio mecánico:**
```diff
-var systemPrompt = tipologia.PromptGPT;
+var systemPrompt = tipologia.GetSystemPrompt();
```

---

## Validación y Testing

### Pre-Deploy Checklist (v1.4 → v1.5)

- [ ] Todos los tests pasan con extension methods
- [ ] No hay referencias directas a `.PromptGPT` en código nuevo
- [ ] DTOs omiten campo deprecated (no serializado)
- [ ] API documentation marca `.promptGPT` como `[Obsolete]`
- [ ] Swagger muestra advertencia: `This field is deprecated. Use ConfiguracionJson.PromptConfig instead.`
- [ ] End-to-end tests validan ambas rutas:
  - [ ] Lectura de ConfiguracionJson
  - [ ] Lectura de extensión method
  - [ ] Escritura a ConfiguracionJson vía API
- [ ] Documentación actualizada (v1.4 migration guide distribuida)

### Pre-Deploy Checklist (v1.5 → v2.0)

- [ ] Migration tools ejecutados en todas las instancias
- [ ] Backup de base de datos con .PromptGPT field (antes de remover)
- [ ] No hay código que escriba a .PromptGPT
- [ ] Database migration (EF) genera script de ALTER TABLE para DROP COLUMN
- [ ] Tests validan que `.PromptGPT` no existe en v2.0 schema
- [ ] Comunicación a clientes sobre breaking change
- [ ] Plan de rollback disponible

---

## FAQ

### P: ¿Desaparecerá mi prompt en v1.4?
**R:** No. El dato sigue disponible vía `GetSystemPrompt()`. Lo que cambia es dónde se GUARDA (en ConfiguracionJson).

### P: ¿Tengo que cambiar mi código ahora (v1.4)?
**R:** 
- Si eres **desarrollador interno:** Sí, cambia de inmediato a extension methods.
- Si eres **consumidor de API:** No urgente hasta v1.5. En v1.5 será read-only.
- Si eres **admin de BD:** No. La BD soporta ambos campos.

### P: ¿Qué pasa si tengo conflictos (prompts diferentes en ambos campos)?
**R:** En v1.4 se valida y se reporta. En v1.5, habrá un script interactivo para elegir qué valor mantener. En v2.0, solo existe ConfiguracionJson.

### P: ¿Se puede automatizar la migración?
**R:** Parcialmente. Los 11 casos asimétricos (uno lleno, otro vacío) se pueden automatizar. Los 187 conflictos requieren decisión humana.

### P: ¿Hay forma de volver atrás si algo sale mal?
**R:** Sí:
- v1.4 y v1.5: Rollback sin problema (ambos campos existen).
- v2.0: Después de eliminar el campo, solo si tienes backup.

### P: ¿Dónde reporto problemas con la migración?
**R:** Azure DevOps Work Item en el epic AB#99732. Estado: Feature/AB#99732-tipologias-cleanup-fase1

---

## Referencias Relacionadas

- [DEPRECATION_PROMPTGPT.md](DEPRECATION_PROMPTGPT.md) — Nota de deprecation original (Fase 1)
- [03_DISENO_TECNICO_DETALLADO.md](03_DISENO_TECNICO_DETALLADO.md) — Arquitectura técnica actualizada
- [05_MANUAL_USO_CONFIGURACION.md](05_MANUAL_USO_CONFIGURACION.md) — Guía de configuración de Tipologías
- [contratos/CONTRATO_API_HTTP.md](contratos/CONTRATO_API_HTTP.md) — Especificación de endpoints
- [src/backend/DocumentIA.Data/Extensions/TipologiaEntityExtensions.cs](../src/backend/DocumentIA.Data/Extensions/TipologiaEntityExtensions.cs) — Código de extension methods
- [tests/DocumentIA.Functions.Tests/TipologiaEntityExtensionsTests.cs](../tests/DocumentIA.Functions.Tests/TipologiaEntityExtensionsTests.cs) — Ejemplos de tests

---

## Contacto / Soporte

- **Tech Lead:** Equipo Backend DocumentIA
- **Product Owner:** SAREB AI DocClassExt
- **Tracking:** Azure DevOps - Epic AB#99732
- **Issue Tracker:** GitHub Issues (si aplica)
