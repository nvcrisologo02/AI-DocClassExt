# Análisis: ¿QUÉ del Contrato de Salida NO se Guarda?

## 📋 Mapa de Campos: Contrato vs Base de Datos

### **Nivel 1: Identificacion**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `Documento` | "nota-simple-test.pdf" | ✅ Sí | `Documentos.NombreArchivo` |
| `Guid` | "a4db1c34-bf2e-4dc1-..." | ✅ Sí | `Documentos.Guid` |
| `Tipologia` | "nota.simple.1_3" | ✅ Sí | `Documentos.Tipologia` |
| `FechaProceso` | "2026-02-13T11:50:01Z" | ✅ Sí | `Documentos.FechaProceso` |
| `Paginas` | 5 | ✅ Sí | `Documentos.Paginas` |

### **Nivel 2: Integridad (Hashes y Metadatos)**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `CRC32` | "a1b2c3d4" | ✅ Sí | `Documentos.CRC32` |
| `SHA256` | "e3b0c44298fc1c149afbf4c8996fb..." | ✅ Sí | `Documentos.SHA256` |
| `GestorDocumental` | "GESTOR_A" | ❌ **NO** | - |
| `IdActivo` | "ACT-001234" | ❌ **NO** | Campo existe pero no se mapea |

**¿Por qué?** El mapeador en `PersistirActivity` no toma estos campos.

---

### **Nivel 3: DetalleEjecucion.Clasificacion**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `Modelo` | "gpt-4" | ✅ Sí | `ResultadosProcesamiento.ModeloClasificacion` |
| `Confianza` | 0.95 | ✅ Sí | `ResultadosProcesamiento.ConfianzaClasificacion` |
| `FallbackLLM` | false | ✅ Sí | `ResultadosProcesamiento.FallbackLLM` |
| `TipologiaDetectada` | "nota.simple.1_3" | ❌ **NO** | - |

**¿Por qué?** Se usa mas bien el campo `Tipologia` de `Identificacion`.

---

### **Nivel 4: DetalleEjecucion.Extraccion**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `Modelo` | "claude-3-opus" | ✅ Sí | `ResultadosProcesamiento.ModeloExtraccion` |
| `LayoutEnabled` | true | ✅ Sí | `ResultadosProcesamiento.LayoutEnabled` |
| `TiemposMs` (dict) | {"Classify": 1500, "Extract": 3200} | ⚠️ Parcial | Solo "Classify" y "Extract" |

**¿Por qué TiemposMs es parcial?**
- Classif: ✅ → `TiempoClasificacionMs`
- Extract: ✅ → `TiempoExtraccionMs`
- Otros tiempos (Normalize, Validate, Integrate): ❌ NO se guardan

---

### **Nivel 5: DetalleEjecucion.Postproceso**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `Normalizaciones[]` | ["nombre_mayuscula", "fecha_formato_DD/MM/YYYY"] | ✅ Sí (JSON) | `ResultadosProcesamiento.NormalizacionesJson` |
| `Validaciones[]` | ["campo_requerido_OK", "rango_permitido_OK"] | ✅ Sí (JSON) | `ResultadosProcesamiento.ValidacionesJson` |
| `Inconsistencias[]` | ["fecha_futura", "nif_invalido"] | ✅ Sí (JSON) | `ResultadosProcesamiento.InconsistenciasJson` |

**✅ Todo se guarda como JSON.**

---

### **Nivel 6: DetalleEjecucion.Integracion (🔴 MUCHOS campos NO se guardan!)**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `Tipologia` | "nota.simple.1_3" | ❌ **NO** | - |
| `Estado` | "OK" | ✅ Sí | `ResultadosProcesamiento.ResultadoIntegracion` |
| `Mensaje` | "Integración completada" | ❌ **NO** | - |
| `Timestamp` | "2026-02-13T11:50:05Z" | ❌ **NO** | - |
| `DatosOriginales` (dict) | {"FincaRegistral": "123", ...} | ❌ **NO** | - |
| `DatosFinales` (dict) | {"FincaRegistral": "123", "NivelRiesgo": "ALTO", ...} | ❌ **NO** | - |

#### **Detalles de Plugins (lista de PluginExecutionResult)**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `PluginKey` | "mock-enrichment" | ✅ Parcial | En `ModuloIntegracion` como string concatenado |
| `Priority` | 1 | ❌ **NO** | - |
| `Success` | true | ❌ **NO** | - |
| `Mensaje` | "Enriquecimiento exitoso" | ❌ **NO** | - |
| `StatusCode` | 200 | ❌ **NO** | - |
| `DurationMs` | 1250 | ❌ **NO** | - |
| `Error` | null | ❌ **NO** | - |
| `DatosEnriquecidos` (dict) | {"NivelRiesgo": "ALTO", "IdSAREB": "NS-001"} | ❌ **NO** | - |

**¿Cómo se guarda la integración actualmente?**
```csharp
// Solo esto:
resultado.ModuloIntegracion = "mock-enrichment,mock-soap-catastro,sareb-business-rules";
resultado.ResultadoIntegracion = "OK";
```

**Falta absolutamente todo el detalle de cada plugin!** 🔴

---

### **Nivel 7: DatosExtraidos (Root)**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| Todos los campos | {"FincaRegistral": "123", "Titular": "Juan Pérez", ...} | ✅ Sí (JSON completo) | `ResultadosProcesamiento.DatosExtraidosJson` |

**✅ El JSON completo de datos extraídos se guarda.**

---

### **Nivel 8: ResultadoFinal (Root)**

| Campo | Valor Ejemplo | ¿Se Guarda? | Ubicación BD |
|-------|---------------|-----------|------------|
| `Estado` | "OK" / "ERROR" / "VALIDACION_CON_ERRORES" | ✅ Sí | `Documentos.Estado` |
| `ConfianzaGlobal` | 0.89 | ✅ Sí | `Documentos.ConfianzaGlobal` |

**✅ Se guarda pero está duplicado** (también en la raíz de Documentos).

---

## 🔴 RESUMEN: INFORMACIÓN PERDIDA (NO GUARDADA)

### **Campos que NO se guardan pero DEBERÍAN:**

#### 1️⃣ **Del Contrato de Integridad**
```csharp
❌ GestorDocumental      // Nombre del gestor documental
❌ IdActivo             // ID del activo en el gestor
```

#### 2️⃣ **De la Clasificación**
```csharp
❌ TipologiaDetectada   // No se guarda (pero es redundante con Tipologia)
```

#### 3️⃣ **De Tiempos (Performance Metrics) 🔴 CRÍTICO**
```csharp
❌ TiempoNormalizacionMs   // Tiempo de normalización
❌ TiempoValidacionMs       // Tiempo de validación
❌ TiempoIntegracionMs      // Tiempo de llamadas a plugins
❌ TiempoTotalMs            // Tiempo total del procesamiento
```

**Impacto**: No puedes medir performance ni identificar cuellos de botella.

#### 4️⃣ **De Integración (FALTA CASI TODO) 🔴🔴 MUY CRÍTICO**

```csharp
❌ Integracion.Mensaje           // Mensaje descriptivo
❌ Integracion.Timestamp         // Cuándo se ejecutó
❌ Integracion.DatosOriginales   // Datos antes del enriquecimiento
❌ Integracion.DatosFinales      // Datos después del enriquecimiento

// Detalles de cada plugin (Lista completa):
❌ Plugin.Priority              // Prioridad de ejecución
❌ Plugin.Success               // ¿Ejecutó correctamente?
❌ Plugin.Mensaje               // Mensaje del plugin
❌ Plugin.StatusCode            // HTTP status code
❌ Plugin.DurationMs            // ¿Cuánto tardó?
❌ Plugin.Error                 // Trace del error si falló
❌ Plugin.DatosEnriquecidos     // Los datos que enriquecio el plugin
```

---

## 💥 PROBLEMA CRÍTICO: Detalles de Plugins Desaparecen

Actualmente solo se guarda:
```
ModuloIntegracion: "mock-enrichment,mock-soap-catastro,sareb-business-rules"
ResultadoIntegracion: "OK"
```

**Se pierden completamente:**
- ¿Qué dato enriqueció cada plugin?
- ¿Cuál plugin falló?
- ¿Cuánto tiempo tardó cada uno?
- ¿Cuál fue el detalle del error?

---

## 🔧 Soluciones Recomendadas

### **Opción 1: Guardar JSON con detalles completos (Recomendado)**

Agregar nueva columna en `ResultadosProcesamiento`:
```sql
ALTER TABLE ResultadosProcesamiento 
ADD ResultadoIntegracionJson NVARCHAR(MAX);
```

```csharp
// En PersistirActivity.MapearResultado():
resultado.ResultadoIntegracionJson = JsonSerializer.Serialize(
    salida.DetalleEjecucion.Integracion
);
```

**Ventajas:**
- ✅ Guarda TODO sin cambiar schema
- ✅ Flexible para futuras extensiones
- ✅ SQL queries fáciles para análisis

---

### **Opción 2: Tabla separada de PluginExecution (Más estructurado)**

```sql
CREATE TABLE PluginExecutions (
    Id INT PRIMARY KEY IDENTITY(1,1),
    ResultadoId INT NOT NULL FOREIGN KEY,
    PluginKey NVARCHAR(100),
    Priority INT,
    Success BIT,
    Mensaje NVARCHAR(MAX),
    StatusCode INT,
    DurationMs INT,
    Error NVARCHAR(MAX),
    DatosEnriquecidosJson NVARCHAR(MAX),
    FechaEjecucion DATETIME DEFAULT GETUTCDATE()
);
```

**Ventajas:**
- ✅ Schema normalizado
- ✅ Fácil hacer queries por plugin
- ✅ Posibilidad de histórico

---

### **Opción 3: Campos individuales (No recomendado - explosión de columnas)**

Agregar múltiples columnas para cada métrica...
Pero se vuelve inmantenible. ❌

---

## 📊 Tabla Comparativa Final

```
GUARDADO     NOT GUARDADO          IMPACTO
============ ==================== ===============
✅ Tipología ❌ Gestor Doc        Bajo
✅ Confianza ❌ IdActivo          Bajo
✅ Modelos   ❌ Tiempo normaliz   MEDIO (auditoría)
✅ Datos ext ❌ Tiempo integr     MEDIO (auditoría)
✅ Valid.    ❌ Plugin details    🔴 CRÍTICO
✅ Normals   ❌ Enriquecimiento   🔴 CRÍTICO
             ❌ Errores plugins   🔴 CRÍTICO
```

---

## 🎯 Recomendación Inmediata

**Implementar Opción 1** (JSON con detalles de integración):

```csharp
// Agregar en PersistirActivity
resultado.ResultadoIntegracionJson = JsonSerializer.Serialize(
    new {
        salida.DetalleEjecucion.Integracion.Estado,
        salida.DetalleEjecucion.Integracion.Mensaje,
        salida.DetalleEjecucion.Integracion.Timestamp,
        Plugins = salida.DetalleEjecucion.Integracion.Plugins.Select(p => new {
            p.PluginKey,
            p.Priority,
            p.Success,
            p.Mensaje,
            p.StatusCode,
            p.DurationMs,
            p.Error,
            EnriquecimientoCampos = p.DatosEnriquecidos?.Keys ?? new List<string>()
        })
    }
);
```

**Esto permitiría:**
- Auditar qué enriqueció cada plugin
- Rastrear errores de integración
- Analizar performance de plugins
- Comparar datos antes/después

