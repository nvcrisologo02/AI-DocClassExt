# 📋 TAREAS PENDIENTES PARA MAÑANA

**Fecha**: 13 de Febrero de 2026  
**Prioridad**: 🔴 CRÍTICA

---

## 🎯 TAREA PRINCIPAL: Guardar Detalles de Integración en BD

### 📝 Descripción
Los detalles de ejecución de plugins (enriquecimiento) se pierden completamente. Solo se guarda un resumen básico. Necesita se implementar persistencia completa del objeto `ResultadoIntegracion` con todos sus detalles de plugins.

### 🔴 Impacto Actual
- ❌ No se pueden auditar qué enriquecio cada plugin
- ❌ No se rastrean errores específicos de plugins
- ❌ No se mide performance de integración
- ❌ No se pueden comparar datos antes/después del enriquecimiento

### ✅ Solución a Implementar

#### **Step 1: Migrations - Agregar columna en BD**
Archivo: `src/backend/DocumentIA.Data/Migrations/AddResultadoIntegracionDetalladoJson.cs`

```csharp
// Agregar en ResultadosProcesamiento tabla:
ALTER TABLE ResultadosProcesamiento 
ADD ResultadoIntegracionDetalladoJson NVARCHAR(MAX);
```

#### **Step 2: Modelo - Actualizar Entity**
Archivo: `src/backend/DocumentIA.Data/Entities/ResultadoProcesamientoEntity.cs`

Agregar propiedad:
```csharp
[Column(TypeName = "nvarchar(max)")]
public string? ResultadoIntegracionDetalladoJson { get; set; }
```

#### **Step 3: Mapeo - Actualizar PersistirActivity**
Archivo: `src/backend/DocumentIA.Functions/Activities/PersistirActivity.cs`

En método `MapearResultado()`, agregar después de `ResultadoIntegracion`:
```csharp
resultado.ResultadoIntegracionDetalladoJson = JsonSerializer.Serialize(
    salida.DetalleEjecucion.Integracion,
    new JsonSerializerOptions { WriteIndented = true }
);
```

#### **Step 4: Testing - Verificar que se guarda**
Scripts: 
- `scripts/test-multi-plugin.ps1` - Ya existe
- `scripts/check-database.ps1` - Actualizar para mostrar integración detallada

#### **Step 5: Documentación**
Actualizar:
- `docs/ANALISIS_PERSISTENCIA_BBDD.md` - Agregar nueva columna
- `docs/CAMPOS_NO_GUARDADOS.md` - Marcar como RESUELTO

---

## 📊 Campos que Se Guardarán

### Estructura JSON (ResultadoIntegracionDetalladoJson):
```json
{
  "tipologia": "nota.simple.1_3",
  "estado": "OK",
  "mensaje": "Integración completada con 3 plugins",
  "timestamp": "2026-02-13T11:50:05Z",
  "datosOriginales": {
    "FincaRegistral": "123",
    "Titular": "Juan Pérez",
    "superficie": 150
  },
  "datosFinales": {
    "FincaRegistral": "123",
    "Titular": "Juan Pérez",
    "superficie": 150,
    "NivelRiesgoCargas": "BAJO",
    "CompletitudDocumento": 85,
    "IdInternoSAREB": "NS-20260213-A1B2C3D4",
    "PrioridadGestion": "NORMAL"
  },
  "plugins": [
    {
      "pluginKey": "mock-enrichment",
      "priority": 1,
      "success": true,
      "mensaje": "Mock REST enriquecimiento completado",
      "statusCode": 200,
      "durationMs": 245,
      "error": null,
      "datosEnriquecidos": {
        "CampoX": "valor1",
        "CampoY": "valor2"
      }
    },
    {
      "pluginKey": "mock-soap-catastro",
      "priority": 2,
      "success": true,
      "mensaje": "SOAP catastral consultado",
      "statusCode": 200,
      "durationMs": 1523,
      "error": null,
      "datosEnriquecidos": {
        "SuperficieCatastral": 87.5,
        "ValorCatastral": 185000
      }
    },
    {
      "pluginKey": "sareb-business-rules",
      "priority": 3,
      "success": true,
      "mensaje": "Reglas SAREB aplicadas",
      "statusCode": 0,
      "durationMs": 156,
      "error": null,
      "datosEnriquecidos": {
        "NivelRiesgoCargas": "BAJO",
        "IdInternoSAREB": "NS-20260213-A1B2C3D4"
      }
    }
  ]
}
```

---

## ⏱️ Estimación de Tiempo
- **Step 1-2**: ~15 min (Migration + Entity)
- **Step 3**: ~10 min (Mapeo)
- **Step 4**: ~10 min (Testing)
- **Step 5**: ~10 min (Documentación)

**Total**: ~45-60 minutos

---

## 🚀 Checklist de Implementación

- [ ] Crear/ejecutar migration para columna nueva
- [ ] Actualizar `ResultadoProcesamientoEntity`
- [ ] Mapear en `PersistirActivity.MapearResultado()`
- [ ] Compilar: `dotnet build`
- [ ] Ejecutar test: `.\test-multi-plugin.ps1`
- [ ] Verificar con: `.\scripts/check-database.ps1`
- [ ] Validar JSON guardado en BD
- [ ] Actualizar documentación
- [ ] **Commit**: `feat: guardar detalles completos de integración en BD`

---

## 💾 Commit Message Sugerido

```
feat: persisten complete integration details with plugin execution metrics

- Add ResultadoIntegracionDetalladoJson column to ResultadosProcesamiento
- Store full plugin execution details (success, duration, errors, enriched data)
- Enable audit trail of data enrichment before/after plugins
- Allows performance analysis by plugin and error tracking

BREAKING CHANGE: None (additive only)
```

---

## 📍 Archivos a Modificar

1. `src/backend/DocumentIA.Data/Entities/ResultadoProcesamientoEntity.cs`
2. `src/backend/DocumentIA.Functions/Activities/PersistirActivity.cs`
3. `docs/ANALISIS_PERSISTENCIA_BBDD.md` (documentación)

---

## 🔗 Referencias
- Especificación: `docs/CAMPOS_NO_GUARDADOS.md`
- Schema actual: `docs/ANALISIS_PERSISTENCIA_BBDD.md`
- Contrato: `src/backend/DocumentIA.Core/Models/ContratoSalida.cs`

---

**Estado**: ⏳ Pendiente para mañana  
**Creado**: 13 Feb 2026, 11:50 UTC  
**Prioridad**: 🔴 CRÍTICA
