# Plan de implementación: Prompts de clasificación configurables sin recompilar

Fecha: 2026-06-02  
Ámbito: `DocumentIA.Functions` (clasificación GPT en dos fases)  
Estado: Propuesta para revisión (sin implementación)

## 1. Objetivo

Eliminar hardcodes de prompts en el flujo de clasificación GPT (`phase1` y `phase2`) para permitir cambios operativos sin recompilar ni redeployar código.

Resultado esperado:
- Cambiar textos de prompts desde almacenamiento configurable (BBDD).
- Mantener fallback seguro en configuración local (`appsettings`) si falla BBDD.
- Trazabilidad de la versión de prompt usada en cada ejecución.

## 2. Motivación

Situación actual:
- Hay textos de prompt hardcodeados en `GptClasificarDataProvider`.
- Ajustar instrucciones requiere tocar código, compilar y desplegar.
- El ciclo de tuning para clasificación documental es más lento de lo necesario.

Necesidad:
- Iteración rápida de prompts en explotación.
- Control de versiones y rollback inmediato.
- Gobierno (auditoría de quién cambió qué y cuándo).

## 3. Alcance

Incluido:
- Prompts de clasificación GPT fase 1 y fase 2 (system/user).
- Resolución de prompts por clave lógica y versión activa.
- Caché en memoria con TTL corto.
- Fallback a `appsettings` si no hay prompt activo en BBDD.
- Telemetría: `promptVersion`, `promptKey`, `source` (db/config).
- Endpoint/flujo administrativo para publicación (opcional en fase 2).

Excluido (fase posterior):
- Editor UI completo en front admin (si no existe ya el módulo de gestión).
- A/B testing automático de prompts.
- DSL avanzada de plantillas.

## 4. Diseño propuesto

### 4.1 Modelo de datos (BBDD)

Tabla propuesta: `PromptTemplate`

Campos mínimos:
- `Id` (GUID o bigint)
- `PromptKey` (string, indexado)
  - Ejemplos:
    - `classification.phase1.system`
    - `classification.phase1.user`
    - `classification.phase2.system`
    - `classification.phase2.user`
- `Version` (int o semver string)
- `Content` (nvarchar(max))
- `IsActive` (bool)
- `Environment` (string: dev/pre/pro) opcional
- `Description` (string) opcional
- `CreatedAtUtc`, `CreatedBy`
- `UpdatedAtUtc`, `UpdatedBy`
- `PublishedAtUtc`, `PublishedBy` opcional

Restricciones recomendadas:
- Único por (`PromptKey`, `Version`, `Environment`).
- Regla de negocio: 1 activo por (`PromptKey`, `Environment`).

### 4.2 Servicio de resolución de prompts

Nuevo servicio: `IClassificationPromptProvider`

Contrato sugerido:
- `GetPromptSetAsync(context)` devuelve objeto con:
  - `Phase1System`
  - `Phase1User`
  - `Phase2System`
  - `Phase2User`
  - metadatos (`VersionMap`, `Source`)

Estrategia de lectura:
1. Intentar leer prompt activo desde BBDD.
2. Si no existe / error de acceso, usar fallback de configuración.
3. Guardar en caché (`IMemoryCache`) 1-5 minutos.

### 4.3 Placeholders de plantillas

Mantener placeholders simples para evitar romper compatibilidad:
- `{contextoPrompt}`
- `{phase1Catalog}`
- `{phase2Catalog}`
- `{tdn1Code}`
- `{contextoTexto}`
- `{resumenInstruction}`

Regla:
- Si falta un placeholder, sustituir por cadena vacía y registrar warning.

### 4.4 Fallback de configuración

Añadir sección en `appsettings.json`:

```json
"ClassificationPrompts": {
  "Phase1": {
    "System": "...",
    "User": "..."
  },
  "Phase2": {
    "System": "...",
    "User": "..."
  }
}
```

Uso:
- Fuente secundaria cuando DB no responde o no hay versión activa.
- Fuente primaria opcional por feature flag durante transición.

### 4.5 Telemetría y observabilidad

Extender evento `Prompt.Trace` con:
- `promptSource`: `db` | `config`
- `promptVersionPhase1System`
- `promptVersionPhase1User`
- `promptVersionPhase2System`
- `promptVersionPhase2User`
- `promptKeySetHash` (opcional)

Objetivo:
- Relacionar degradaciones de clasificación con cambios de prompt.

## 5. Plan por fases

### Fase 0 - Preparación
- Definir claves canónicas de prompts.
- Definir límites máximos de contenido por prompt (caracteres/tokens estimados).
- Alinear naming y convenciones de placeholders.

### Fase 1 - Núcleo técnico (sin UI)
- Crear entidad + repositorio `PromptTemplate`.
- Crear migración EF y desplegar esquema.
- Implementar `IClassificationPromptProvider` con caché y fallback.
- Reemplazar hardcodes en `GptClasificarDataProvider` por proveedor.
- Añadir trazabilidad de versión/source en telemetría.

### Fase 2 - Gestión operativa
- Exponer API admin para:
  - crear borrador
  - activar versión
  - desactivar/rollback
- Validaciones servidor (longitud, placeholders permitidos).

### Fase 3 - Endurecimiento
- Tests de resiliencia (DB caída => fallback config).
- Métricas y alertas (fallo de carga de prompts, fallback rate alto).
- Auditoría de cambios y permisos RBAC.

## 6. Cambios de código previstos (alto nivel)

- `DocumentIA.Data`
  - nueva entidad y mapeo EF
  - nueva migración
  - repositorio de prompts

- `DocumentIA.Functions`
  - nuevo provider de prompts
  - registro DI
  - adaptación de `GptClasificarDataProvider`
  - lectura de `ClassificationPrompts` fallback

- `DocumentIA.Tests.Unit`
  - tests unitarios de provider (db, cache, fallback)
  - tests de `GptClasificarDataProvider` con prompts externos

## 7. Riesgos y mitigaciones

Riesgo 1: prompts inválidos en producción  
Mitigación:
- Validación previa de placeholders y longitud.
- Publicación en dos pasos (draft -> active).
- Rollback rápido a versión anterior.

Riesgo 2: dependencia de BBDD para inferencia  
Mitigación:
- Caché en memoria.
- Fallback en `appsettings`.
- Timeout corto y circuit breaker de lectura.

Riesgo 3: pérdida de trazabilidad  
Mitigación:
- Guardar versión y fuente en telemetría por request.

## 8. Criterios de aceptación

- Se pueden cambiar prompts de fase 1/2 sin recompilar.
- Una nueva versión activa se aplica en runtime (máx. TTL de caché).
- Si DB falla, el sistema sigue clasificando con fallback de config.
- Telemetría identifica versión/fuente de prompt usada.
- Existe rollback funcional a versión previa en menos de 5 minutos.

## 9. Propuesta de implementación incremental

Orden recomendado:
1. Fase 1 completa en branch corto, con fallback activado por defecto.
2. Validar en entorno pre con pruebas E2E conocidas.
3. Activar lectura DB en producción de forma gradual.
4. Añadir API/UI (fase 2) tras estabilización.

## 10. Decisiones pendientes para la revisión

1. ¿Versionado numérico (`int`) o semántico (`string`)?
2. ¿Scopes por entorno (`Environment`) en la misma tabla o tablas separadas?
3. ¿TTL de caché objetivo: 60s, 120s o 300s?
4. ¿Se permite edición directa del prompt activo o solo crear nueva versión?
5. ¿Se implementa API admin en la misma release o en la siguiente?

---

Este documento define el plan de trabajo; no implica cambios funcionales aplicados todavía.
