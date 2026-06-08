# Checklist de Revisión: Resumen Garantizado (AB#99754)

## Fase 1: Familiarización con Arquitectura Actual

### Core Configuration y Models
- [ ] [DocumentIA.Core/Configuration/PromptConfig.cs](../src/backend/DocumentIA.Core/Configuration/PromptConfig.cs)
  - Líneas 1-75: `PromptConfig` y `PromptDefaultsSettings`
  - **Verificar:** Estructura de propiedades, método `ToPromptConfig()`

- [ ] [DocumentIA.Core/Models/ClasificacionModels.cs](../src/backend/DocumentIA.Core/Models/ClasificacionModels.cs)
  - Línea 16: Flag `GenerarResumenPorDefecto` en `ClasificacionInput`
  - **Verificar:** Tipo y documentación

- [ ] [DocumentIA.Core/Validation/TipologiaPromptConfigValidator.cs](../src/backend/DocumentIA.Core/Validation/TipologiaPromptConfigValidator.cs)
  - **Verificar:** Validaciones aplicables a `PromptConfig`
  - **Nota:** Puede ser necesario extender para soportar nuevas validaciones

### Services y Providers
- [ ] [DocumentIA.Functions/Services/GptClasificarDataProvider.cs](../src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs)
  - Línea 73: Call a `ResolveResumenPrompt()`
  - Líneas 272-290: Implementación de `ResolveResumenPrompt()`
  - **Verificar:** Entry point y lógica de resolución

- [ ] [DocumentIA.Functions/Services/OpenAIPromptDataProvider.cs](../src/backend/DocumentIA.Functions/Services/OpenAIPromptDataProvider.cs)
  - Línea 169, 236, 256, 337: Calls a `InterpolateTemplate()`
  - Líneas 481-510: Implementación de `InterpolateTemplate()`
  - **Verificar:** Lógica de sustitución {contenido} y {campo:*}

- [ ] [DocumentIA.Functions/Services/GptFallbackExtraerDataProvider.cs](../src/backend/DocumentIA.Functions/Services/GptFallbackExtraerDataProvider.cs)
  - **Verificar:** Cómo se integra resumen en fallback de extracción

### Configuration y DI
- [ ] [DocumentIA.Functions/Program.cs](../src/backend/DocumentIA.Functions/Program.cs)
  - Línea 145: Registration de `PromptDefaultsSettings`
  - **Verificar:** DI setup

- [ ] [DocumentIA.Functions/appsettings.json](../src/backend/DocumentIA.Functions/appsettings.json)
  - Líneas 101-120: Sección `"PromptDefaults"`
  - **Verificar:** Valores por defecto (SystemPrompt, UserPromptTemplate, etc.)

### Orchestration
- [ ] [DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs](../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs)
  - Línea 766: Settea `GenerarResumenPorDefecto = true`
  - Línea 1530: Condición fallback para generar resumen
  - **Verificar:** Puntos donde se activa el resumen

### Configuration Loaders
- [ ] [DocumentIA.Functions/Services/TipologiaConfigLoader.cs](../src/backend/DocumentIA.Functions/Services/TipologiaConfigLoader.cs)
  - **Verificar:** Cómo carga `<TIPOLOGIA>.validation.json` con `PromptConfig`

- [ ] [DocumentIA.Functions/Services/ClassificationModelRegistryLoader.cs](../src/backend/DocumentIA.Functions/Services/ClassificationModelRegistryLoader.cs)
  - **Verificar:** Cómo resuelve `ModelKey` en `PromptConfig`

---

## Fase 2: Tests Existentes (Revisar/Crear)

### Unit Tests
- [ ] [DocumentIA.Tests.Unit/Services/Classification/GptClasificarDataProviderTests.cs](../src/backend/DocumentIA.Tests.Unit/Services/Classification/GptClasificarDataProviderTests.cs)
  - **Crear tests para:** `ResolveResumenPrompt()` con diferentes escenarios
    - ✓ GenerarResumenPorDefecto=true + defaults válidos
    - ✓ GenerarResumenPorDefecto=false
    - ✓ UserPromptTemplate vacío
    - ✓ Interpolación de {contenido}
    - ✓ Override de tipología

- [ ] [DocumentIA.Tests.Unit/Services/OpenAIPromptDataProviderTests.cs](../src/backend/DocumentIA.Tests.Unit/Services/OpenAIPromptDataProviderTests.cs)
  - **Revisar tests para:** `InterpolateTemplate()`
  - **Crear tests para:**
    - ✓ {contenido} interpolation
    - ✓ {campo:*} interpolation con valores presentes
    - ✓ {campo:*} interpolation con valores ausentes
    - ✓ Múltiples placeholders en un template

- [ ] [DocumentIA.Tests.Unit/Configuration/TipologiaConfigLoaderTests.cs](../src/backend/DocumentIA.Tests.Unit/Configuration/TipologiaConfigLoaderTests.cs)
  - **Revisar tests:** Carga de PromptConfig desde tipología
  - **Crear tests para:** Override de prompt por tipología

### E2E Tests
- [ ] [DocumentIA.Tests.E2E/...](../src/backend/DocumentIA.Tests.E2E/)
  - **Crear test:** Clasificación con `GenerarResumenPorDefecto=true`
  - **Validar:** Resumen presente en `DatosExtraidos["Resumen"]`
  - **Validar:** Estructura de 5 apartados (Objetivo, Datos clave, Alertas, Acciones, Contenido)

---

## Fase 3: Tipologías de Configuración

### Ficheros de Configuración por Tipología
- [ ] Revisar estructura de `config/tipologias/<TIPOLOGIA>.validation.json`
  - **Ejemplo:** `config/tipologias/nota.simple.1_4.validation.json`
  - **Verificar:** Sección `"PromptConfig"` (si existe)

- [ ] Listar tipologías que podrían beneficiarse de resumen custom:
  - ESCR-* (Escrituras)
  - NOTI-* (Notificaciones)
  - CERT-* (Certificaciones)
  - **Crear override:** Para al menos 2 tipologías de prueba

---

## Fase 4: Validación y Telemetría

### Validación
- [ ] Extender `TipologiaPromptConfigValidator` (si es necesario)
  - **Verificar:** Que `PromptConfig` sea validado correctamente
  - **Crear tests:** Para validación de schema

### Telemetría y Logging
- [ ] Revisar telemetría en `OpenAIPromptDataProvider`
  - **Verificar:** Logging de ejecución de resumen
  - **Agregar métricas:** Timing, tokens, errores

- [ ] Revisar telemetría en `GptClasificarDataProvider`
  - **Verificar:** Logging de `ResolveResumenPrompt()`

---

## Fase 5: Documentación

### Documentación Técnica
- [ ] Actualizar [03_DISENO_TECNICO_DETALLADO.md](./03_DISENO_TECNICO_DETALLADO.md)
  - Sección de prompts y resumen garantizado
  - Arquitectura de extensión

### Documentación de Operación
- [ ] Actualizar [04_MANUAL_EXPLOTACION.md](./04_MANUAL_EXPLOTACION.md)
  - Configuración de `PromptDefaults` en appsettings.json
  - Cómo override por tipología

### README y Contribución
- [ ] Actualizar [README.md](../README.md)
  - Feature overview de Resumen Garantizado
  - Links a documentación técnica

---

## Fase 6: Definition of Done

Antes de mergear a `develop`:

- [ ] Todos los tests unitarios pasan
- [ ] Todos los tests E2E pasan
- [ ] Code review completado
- [ ] Documentación actualizada
- [ ] Telemetría configurada
- [ ] No hay warnings de compilation
- [ ] No hay regressions en tests existentes

---

## Notas Operacionales

### Comandos Útiles

**Ejecutar tests específicos:**
```powershell
dotnet test DocumentIA.Tests.Unit -k "ResolveResumenPrompt"
dotnet test DocumentIA.Tests.Unit -k "InterpolateTemplate"
```

**Buscar usages de GenerarResumenPorDefecto:**
```powershell
Select-String -Path "src\backend\**\*.cs" -Pattern "GenerarResumenPorDefecto" -Recurse
```

**Buscar usages de InterpolateTemplate:**
```powershell
Select-String -Path "src\backend\**\*.cs" -Pattern "InterpolateTemplate" -Recurse
```

### Tipologías de Prueba Recomendadas

Para validación E2E, usar tipologías con documentos test:
- `nota.simple.1_4` — estructura simple, bien documentada
- `escritura` — documentos complejos, múltiples campos
- `certificacion` — ejemplo de override custom

---

## Histórico

| Fecha | Cambios |
|-------|---------|
| 2026-06-08 | Creación inicial del checklist |

