# 📝 COMMITS LISTOS PARA HACER (OPCIÓN 3)

**4 commits organizados por categoría**

---

## ✅ COMMIT 1: Fix - Content-Type y ArrayValidator

```bash
git add src/backend/DocumentIA.Core/Validation/Rules/ArrayValidator.cs
git add src/backend/DocumentIA.Plugins/Integration/SoapPlugin.cs
git commit -m "fix: corregir formato Content-Type en SoapPlugin y mejorar ArrayValidator

- Eliminar charset de mediaType en SoapPlugin StringContent (SOAP 1.1 y 1.2)
- System.Net.Http rechaza formato 'text/xml; charset=utf-8'
- Mejorar ArrayValidator para manejar múltiples tipos: JsonElement, strings JSON, objetos
- Convertir inputs dinámicamente a List<object> para validación consistente

Fixes:
  - FormatException en llamadas SOAP
  - 'Cargas: El valor no es una colección válida' en validación arrays"
```

---

## ✅ COMMIT 2: Feature - Sistema Multi-Plugin

```bash
git add src/backend/DocumentIA.Plugins/Integration/
git add src/enrichments/SarebEnrichments/
git commit -m "feat: implementar sistema multi-plugin con soporte REST, SOAP y Custom

- Crear PluginFactory para instanciación dinámica de plugins
- Soportar tipos de plugin: REST, SOAP, Custom (cargar DLLs externas)
- Implementar ResilientPlugin con retry policies y exponential backoff
- Crear interfaz ICustomEnricher para DLLs externas reutilizables
- Implementar CustomPlugin que carga assemblies dinámicamente
- Implementar NotaSimpleEnricher con reglas de negocio SAREB:
  * Clasificación de riesgo por cargas (ALTO, MEDIO, BAJO)
  * Cálculo de completitud de documento
  * Generación de ID interno SAREB
  * Determinación de prioridad de gestión (NORMAL, ALTA, URGENTE)
- Health checks por enriquecedor
- Logging detallado por plugin y duración de ejecución

Soporta: Integración flexible con múltiples servicios sin recompilación"
```

---

## ✅ COMMIT 3: Refactor - Flujo de Validación

```bash
git add src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs
git add src/backend/DocumentIA.Core/Validation/
git commit -m "refactor: mejorar flujo de validación y permitir enriquecimiento con advertencias

- Cambiar modelo de validación: Error detiene, Warning continúa procesamiento
- Introducir estado 'VALIDACION_CON_ERRORES' cuando hay advertencias pero sin errores críticos
- Permitir enriquecimiento aún con validaciones fallidas (non-blocking)
- Reportar confianza reducida manteniendo integridad del flujo
- Actualizar orquestación para no interrumpir en paso 5 (Validación)
- Continuar con pasos 6 y 7 (Integración y Persistencia) con estado degradado
- Mejorar logging de validaciones con clasificación por severidad

Impacto: Documentos con problemas menores no se pierden, se enriquecen y guardan
         ahora con flag de advertencia"
```

---

## ✅ COMMIT 4: Chore - Tooling, Scripts y Configuraciones

```bash
git add scripts/compile-all-plugins.ps1
git add scripts/test-multi-plugin.ps1
git add scripts/mock-soap-server.py
git add scripts/check-database.ps1
git add src/backend/DocumentIA.Functions/config/tipologias/
git add docs/ANALISIS_PERSISTENCIA_BBDD.md
git add docs/CAMPOS_NO_GUARDADOS.md
git add TAREAS_PENDIENTES.md
git commit -m "chore: agregar tooling, scripts de testing y documentación completa

Scripts PowerShell:
  - compile-all-plugins.ps1: Compilar y copiar DLLs a carpeta plugins
  - test-multi-plugin.ps1: Testing end-to-end con 3 plugins (REST, SOAP, Custom)
  - check-database.ps1: Diagnóstico de datos guardados en BD SQL Server

Mock Server:
  - mock-soap-server.py: SOAP 1.1 server para testing sin servidor real
  - mock-enrichment-server.py: REST server para testing basic

Configuraciones Tipologías:
  - Normalizar tipologiaId a 'notasimple' en todas las versiones
  - Actualizar severidades en validaciones (Error vs Warning)
  - Corregir patrones regex 
  - Reorganizar orden de plugins para 1.3

Documentación:
  - ANALISIS_PERSISTENCIA_BBDD.md: Qué se guarda en BD
  - CAMPOS_NO_GUARDADOS.md: Qué campos no se persisten (y por qué)
  - TAREAS_PENDIENTES.md: Mejoras para mañana (persistencia integración)

Permite: Testing local sin dependencias externas, debugging facilitado"
```

---

## 🎯 Cómo Ejecutarlos

### Opción A: Uno por uno
```bash
# 1. Fix
git add src/backend/DocumentIA.Core/Validation/Rules/ArrayValidator.cs src/backend/DocumentIA.Plugins/Integration/SoapPlugin.cs
git commit -m "fix: corregir formato Content-Type..."

# 2. Feature
git add src/backend/DocumentIA.Plugins/Integration/ src/enrichments/SarebEnrichments/
git commit -m "feat: implementar sistema multi-plugin..."

# 3. Refactor
git add src/backend/DocumentIA.Functions/Orchestrators/ src/backend/DocumentIA.Core/Validation/
git commit -m "refactor: mejorar flujo de validación..."

# 4. Chore
git add scripts/ src/backend/DocumentIA.Functions/config/ docs/ TAREAS_PENDIENTES.md
git commit -m "chore: agregar tooling, scripts..."
```

### Opción B: Script bash automático
```bash
#!/bin/bash

# Commit 1
git add src/backend/DocumentIA.Core/Validation/Rules/ArrayValidator.cs src/backend/DocumentIA.Plugins/Integration/SoapPlugin.cs
git commit -m "fix: corregir formato Content-Type en SoapPlugin y mejorar ArrayValidator"

# Commit 2
git add src/backend/DocumentIA.Plugins/Integration/ src/enrichments/SarebEnrichments/
git commit -m "feat: implementar sistema multi-plugin con soporte REST, SOAP y Custom"

# Commit 3
git add src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs src/backend/DocumentIA.Core/Validation/
git commit -m "refactor: mejorar flujo de validación y permitir enriquecimiento con advertencias"

# Commit 4
git add scripts/ src/backend/DocumentIA.Functions/config/ docs/ TAREAS_PENDIENTES.md
git commit -m "chore: agregar tooling, scripts de testing y documentación completa"

git log --oneline -4
```

---

## 📊 Resumen de Cambios

| Commit | Archivos | Líneas | Tipo |
|--------|----------|--------|------|
| 1. Fix | 2 | ~80 | Bug fixes críticos |
| 2. Feature | 10+ | ~600+ | Arquitectura plugins |
| 3. Refactor | 3 | ~40 | Flujo orquestación |
| 4. Chore | 20+ | ~1000+ | Tooling & docs |

**Total**: ~35 archivos, ~1720 líneas

---

## ✅ Verificación Post-Commits

Después de hacer los commits:

```bash
# Ver historial
git log --oneline -4

# Ver cambios por commit
git show <commit-hash>

# Compilar para validar
cd src/backend/DocumentIA.Plugins
dotnet build

# Ejecutar tests
.\scripts\test-multi-plugin.ps1

# Verificar BD
.\scripts\check-database.ps1
```

---

**Creado**: 13 Feb 2026  
**Estado**: Listo para ejecutar  
**Validado**: ✅
