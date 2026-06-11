# Decisiones de Arquitectura: Prompts Configurables

**Fecha:** 2026-06-11  
**Epic:** AB#99800 - Prompts de clasificación configurables sin recompilar  
**Fase:** 0 - Preparación y decisiones arquitectónicas  
**Estado:** Propuesta en revisión

---

## 1. Claves canónicas de prompts

Basado en el análisis del código actual en `GptClasificarDataProvider.cs`, se definen las siguientes claves:

| Clave | Descripción | Ubicación actual (hardcoded) |
|---|---|---|
| `classification.phase1.system` | System message para clasificación TDN1 | GptClasificarDataProvider, línea ~89-96 |
| `classification.phase1.user` | User prompt template para clasificación TDN1 | GptClasificarDataProvider, línea ~99-119 |
| `classification.phase2.system` | System message para clasificación TDN2 | GptClasificarDataProvider, línea ~192-198 |
| `classification.phase2.user` | User prompt template para clasificación TDN2 | GptClasificarDataProvider, línea ~200-218 |

### Convenciones de naming:
- Formato: `{dominio}.{fase}.{role}`
- Dominio: `classification` (extensible a otros dominios futuros)
- Fase: `phase1`, `phase2`
- Role: `system`, `user`

---

## 2. Límites máximos de contenido

### Restricciones técnicas:

| Límite | Valor propuesto | Justificación |
|---|---|---|
| **Longitud máxima de prompt** | 16,000 caracteres | Equivalente aprox. a 4,000 tokens (asumiendo 4 chars/token) |
| **Longitud máxima total (system + user)** | 32,000 caracteres | Límite conservador para contexto GPT-4 (128k tokens disponibles) |
| **Número de placeholders por prompt** | Ilimitado | Validación por nombre de placeholder, no por cantidad |

### Validaciones en tiempo de guardado:
- Error si excede 16,000 caracteres por prompt individual
- Warning si excede 12,000 caracteres (soft limit)
- Validación de placeholders obligatorios presentes

---

## 3. Placeholders permitidos

### Placeholders existentes en código actual:

| Placeholder | Descripción | Usado en | Obligatorio |
|---|---|---|---|
| `{contextoPrompt}` | Instrucciones adicionales desde `Instrucciones.Prompt` | Phase1 User | No |
| `{phase1Catalog}` | Catálogo de familias TDN1 generado dinámicamente | Phase1 User | Sí |
| `{phase2Catalog}` | Catálogo de tipologías TDN2 por familia | Phase2 User | Sí |
| `{tdn1Code}` | Código de familia TDN1 resuelto en phase1 | Phase2 User | Sí |
| `{contextoTexto}` | Contenido textual del documento (markdown/texto) | Phase1 User, Phase2 User | No |
| `{resumenInstruction}` | Instrucciones adicionales para generar resumen (si aplica) | Phase1 System/User, Phase2 System/User | No |
| `{documentName}` | Nombre del archivo del documento | Phase1 User (fallback) | No |

### Sintaxis de placeholders:
- Formato: `{nombreCamelCase}`
- Case-sensitive
- Sin espacios ni caracteres especiales
- Sustitución: reemplazar por cadena vacía si no está disponible (excepto obligatorios)

### Validación de placeholders obligatorios por prompt:

- **Phase1 System:** ninguno obligatorio
- **Phase1 User:** `{phase1Catalog}` obligatorio
- **Phase2 System:** ninguno obligatorio
- **Phase2 User:** `{tdn1Code}`, `{phase2Catalog}` obligatorios

---

## 4. Decisiones de arquitectura

### 4.1 Versionado de prompts

**Decisión:** Versionado numérico secuencial (`int`) autoincrementado

**Justificación:**
- Simplicidad operativa (no requiere gestión manual de versiones)
- Orden cronológico implícito (mayor número = más reciente)
- Fácil comparación y rollback (`Version - 1`)
- Suficiente para auditoría básica

**Alternativa descartada:** Versionado semántico (string `major.minor.patch`)
- Pros: expresividad de cambios (breaking/feature/fix)
- Contras: complejidad innecesaria para prompts de texto, requiere parsing

**Implementación:**
- Campo `Version` tipo `int` NOT NULL
- Autoincremento por `PromptKey` (no global)
- Restricción única: (`PromptKey`, `Version`, `Environment`)

---

### 4.2 Scopes por entorno

**Decisión:** Scopes por entorno con campo `Environment` opcional

**Valores permitidos:** `dev`, `pre`, `pro`, `null` (aplica a todos)

**Justificación:**
- Permite testing de prompts en `dev`/`pre` antes de `pro`
- Simplicidad: misma tabla para todos los entornos
- Fallback: si no existe versión activa para entorno específico, buscar versión con `Environment = null`

**Alternativa descartada:** Tablas separadas por entorno
- Pros: aislamiento total
- Contras: migración complicada, sincronización manual, pérdida de trazabilidad

**Implementación:**
- Campo `Environment` tipo `nvarchar(10)` NULL
- Lógica de resolución:
  1. Buscar versión activa para `(PromptKey, Environment = <actual>, IsActive = true)`
  2. Si no existe, buscar `(PromptKey, Environment = null, IsActive = true)`
  3. Si no existe, fallback a `appsettings.json`

---

### 4.3 TTL de caché

**Decisión:** TTL de **120 segundos (2 minutos)** para caché en memoria

**Justificación:**
- Balance entre latencia y frescura de cambios
- Suficientemente corto para iteración rápida (cambio visible en 2 min máx)
- Suficientemente largo para evitar martilleo de BBDD (60 reads/hora en carga constante)

**Configuración:**
- TTL configurable via `appsettings.json` (`ClassificationPrompts:CacheTtlSeconds`)
- Default: 120 segundos
- Mínimo aceptable: 30 segundos
- Máximo recomendado: 300 segundos (5 min)

**Invalidación proactiva:**
- NO implementada en v1 (complejidad innecesaria)
- Alternativa futura: eventos de invalidación desde API admin

---

### 4.4 Política de edición

**Decisión:** Inmutabilidad de prompts publicados + creación de nuevas versiones

**Flujo operativo:**
1. **Crear borrador** (API POST `/admin/prompts`) → `IsActive = false`
2. **Activar versión** (API PUT `/admin/prompts/{id}/activate`) → `IsActive = true`, desactivar versión anterior activa
3. **Rollback** (API POST `/admin/prompts/{id}/rollback`) → Activar versión anterior, desactivar actual

**Reglas de negocio:**
- Solo 1 versión activa por (`PromptKey`, `Environment`) en un momento dado
- No se permite edición del campo `Content` de una versión activa
- No se permite borrado físico (solo marcar como inactiva)
- Auditoría completa: `CreatedBy`, `UpdatedBy`, `PublishedBy`, `PublishedAtUtc`

**Alternativa descartada:** Edición in-place de versión activa
- Pros: simplicidad UI
- Contras: pérdida de trazabilidad, imposibilidad de rollback preciso

---

## 5. Modelo de datos propuesto (refinado)

```sql
CREATE TABLE [dbo].[PromptTemplate] (
    [Id] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [PromptKey] NVARCHAR(100) NOT NULL,
    [Version] INT NOT NULL,
    [Environment] NVARCHAR(10) NULL, -- 'dev', 'pre', 'pro', NULL (all)
    [Content] NVARCHAR(MAX) NOT NULL,
    [IsActive] BIT NOT NULL DEFAULT 0,
    [Description] NVARCHAR(500) NULL,
    [CreatedAtUtc] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [CreatedBy] NVARCHAR(100) NOT NULL,
    [UpdatedAtUtc] DATETIME2(7) NULL,
    [UpdatedBy] NVARCHAR(100) NULL,
    [PublishedAtUtc] DATETIME2(7) NULL,
    [PublishedBy] NVARCHAR(100) NULL,
    
    CONSTRAINT [UQ_PromptTemplate_Key_Version_Env] UNIQUE ([PromptKey], [Version], [Environment]),
    CONSTRAINT [CK_PromptTemplate_Content_Length] CHECK (LEN([Content]) <= 16000),
    CONSTRAINT [CK_PromptTemplate_Environment] CHECK ([Environment] IN ('dev', 'pre', 'pro') OR [Environment] IS NULL)
);

CREATE INDEX [IX_PromptTemplate_Key_Env_Active] 
ON [dbo].[PromptTemplate] ([PromptKey], [Environment], [IsActive])
WHERE [IsActive] = 1;
```

---

## 6. Configuración de fallback en appsettings.json

```json
{
  "ClassificationPrompts": {
    "CacheTtlSeconds": 120,
    "Fallback": {
      "Phase1": {
        "System": "Eres un sistema experto en clasificación de documentos del sector inmobiliario español...",
        "User": "Prompt adicional de instrucciones (si aplica):\n{contextoPrompt}\n\nFamilias TDN1 disponibles:\n{phase1Catalog}\n\n..."
      },
      "Phase2": {
        "System": "Eres un sistema experto en clasificación de documentos del sector inmobiliario español...",
        "User": "Familia TDN1 resuelta: {tdn1Code}\n\nTipologías disponibles en esta familia:\n{phase2Catalog}\n\n..."
      }
    }
  }
}
```

---

## 7. Criterios de aceptación de Fase 0

- [x] Claves canónicas definidas y documentadas
- [x] Límites de contenido especificados con justificación
- [x] Placeholders documentados con obligatoriedad por prompt
- [x] Decisión de versionado tomada (numérico secuencial)
- [x] Decisión de scopes tomada (campo Environment opcional)
- [x] Decisión de TTL tomada (120 segundos)
- [x] Decisión de política de edición tomada (inmutabilidad + versiones)
- [x] Modelo de datos refinado con restricciones
- [x] Configuración de fallback especificada

---

## 8. Próximos pasos (Fase 1)

1. Crear entidad EF `PromptTemplate` según modelo refinado
2. Generar migración EF con restricciones y índices
3. Implementar `IClassificationPromptProvider` con lógica de resolución (DB → cache → fallback)
4. Añadir sección `ClassificationPrompts` a `appsettings.json` con prompts actuales como fallback
5. Refactorizar `GptClasificarDataProvider` para consumir `IClassificationPromptProvider`

---

**Documento de trabajo:** Este documento sirve como base para implementación de Fase 1. Cualquier cambio debe actualizarse aquí antes de implementación.
