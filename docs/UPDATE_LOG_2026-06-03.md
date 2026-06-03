# Update Log — 2026-06-03

> Cambios técnicos, configuración y documentación aplicados hoy  
> Proyecto: AI DocClassExt — SAREB

---

## Resumen Ejecutivo

Se han integrado **3 commits principales** que afectan a clasificación, catálogo de tipologías y configuración:

| Commit | Descripción | Ficheros Impactados | Estado |
|--------|-------------|-------------------|--------|
| **a5601a1** | fix(classification): Configuration binding para diccionario Flows | Program.cs, ClassificationRoutingSettings | ✅ Documentado |
| **b26fb8d** | feat(catalog): Campo Nombre en TdnCatalogItem | CatalogoTdnRepository, ClassificationTipologiaPromptBuilder | ✅ Documentado |
| *anteriores* | feat: Custom prompts TDN2, pre-markdown, límite de páginas | Múltiples | ✅ Documentado |

**Resultado:** Pipeline de clasificación más robusto, configuración manual de flujos soportada, catálogos enriquecidos con nombres legibles para LLM.

---

## 1. Fix de Binding de Configuración (a5601a1)

### Problema

.NET ConfigurationBuilder no deserializa automáticamente estructuras anidadas complejas como `Dictionary<string, ClassificationFlowSettings>` desde JSON. Síntoma:

- `ClassificationRoutingSettings.Flows` quedaba vacío en runtime
- `ConfigurableClasificarDataProvider` no encontraba flujos nombrados
- Error: `System.NotSupportedException: Proveedor de clasificación 'hybrid-rules-di-gpt' no soportado`

### Solución

**Program.cs** — Implementar `PostConfigure` explícito para cargar diccionario Flows:

```csharp
services.PostConfigure<ClassificationRoutingSettings>(settings =>
{
    var flowsSection = context.Configuration.GetSection("Classification:Flows");
    if (flowsSection.Exists())
    {
        foreach (var flowSection in flowsSection.GetChildren())
        {
            var flowName = flowSection.Key;
            var flowSettings = new ClassificationFlowSettings();
            flowSection.Bind(flowSettings);
            settings.Flows[flowName] = flowSettings;
        }
    }
});
```

**local.settings.json** — Sincronizar estructura de Flows:

```json
"Classification:Flows:hybrid-rules-di-gpt:Providers:0": "rules",
"Classification:Flows:hybrid-rules-di-gpt:Providers:1": "di",
"Classification:Flows:hybrid-rules-di-gpt:Providers:2": "gpt"
```

### Ficheros Modificados

- `src/backend/DocumentIA.Functions/Program.cs` — PostConfigure agregado
- `src/backend/DocumentIA.Functions/local.settings.json` — Flows sincronizados (no commiteado, local dev)

### Documentación Actualizada

- ✅ `docs/03_DISENO_TECNICO_DETALLADO.md` — Sección 3.1.2: Explicación de binding y PostConfigure
- ✅ `docs/CATALOGO_APP_SETTINGS.md` — Regenerado automáticamente (incluye nuevas claves Classification:Flows)

---

## 2. Campo Nombre en TdnCatalogItem (b26fb8d)

### Cambio

Nuevo campo `Nombre` (string) agregado a `TdnCatalogItem` para incluir nombre legible de tipología en catálogos dinámicos.

### Beneficio

El LLM recibe catálogos enriquecidos con contexto:

**Antes:**
```
- ESCR-06
- ESCR-07
```

**Después:**
```
- ESCR-06: Escritura de venta
- ESCR-07: Escritura de compra
```

Mejor clasificación inicial, menos ambigüedad.

### Ficheros Modificados

- `src/backend/DocumentIA.Data/Repositories/ICatalogoTdnRepository.cs` — Interfaz extendida
- `src/backend/DocumentIA.Data/Repositories/CatalogoTdnRepository.cs` — Implementación actualizada
- `src/backend/DocumentIA.Core/Configuration/ClassificationTipologiaPromptBuilder.cs` — Uso de Nombre en BuildTdn2CatalogByFamilia()

### Documentación Actualizada

- ✅ `docs/03_DISENO_TECNICO_DETALLADO.md` — Tabla de ClasificarActivity y descripción en ClassificationTipologiaPromptBuilder
  
---

## 3. Cambios Previos (Integración Anterior)

### Commits Relacionados

- **5027bed** — Pre-markdown (paso 2.8) antes de clasificar, skip fallback redundante, tipología virtual TDN1
- **fbf3351** — Límite de páginas configurable por tipología
- **4b23fde, 7217f01** — Custom prompts y fallback dinámico
- **ffd1b1a** — Campo TDN2_Prompt

Todos estos cambios ya están reflejados en `docs/03_DISENO_TECNICO_DETALLADO.md` (v2026-04-24). Hoy se ha actualizado la fecha a 2026-06-03 y se han añadido los detalles de binding + campo Nombre.

---

## 4. Documentos Regenerados / Actualizados Hoy

### CATALOGO_APP_SETTINGS.md

**Regenerado automáticamente** — Incluye nuevas claves escaneadas:

```
Classification:Flows:*:Providers:*
Classification:UseGlobalFallback
Classification:GlobalFallbackProvider
Classification:DefaultFlow
```

Comando: `pwsh ./scripts/generate-config/generate-appsettings-catalog.ps1`

Estado: ✅ Actualizado (2026-06-03 01:52+)

### 03_DISENO_TECNICO_DETALLADO.md

**Actualizado manualmente** — Cambios:

1. Fecha: 2026-04-24 → 2026-06-03
2. Sección 3.1.2 (nueva): **Vinculación de flujos de clasificación — Estructura de configuración**
   - Explicación del problema de binding
   - Código del PostConfigure
   - Sincronización local.settings.json
   - Resolución de flujo en runtime
3. Tabla de actividades (fila ClasificarActivity): Actualizada con referencia a PostConfigure
4. Descripción ClassificationTipologiaPromptBuilder: Aclaración de campo Nombre

---

## 5. Cambios Pendientes / Notas

### ✅ Completado

- Binding fix implementado y testeado
- Campo Nombre implementado y documentado
- Documentación técnica actualizada
- Catálogo app settings regenerado
- 4 Work Items cerrados a Done (99709, 99710, 99714, 99715)

### ⏳ Monitorear

- Validar flujo `hybrid-rules-di-gpt` en E2E (orden: rules → DI → GPT)
- Confirmar que custom prompts TDN2 se generan con Nombre enriquecido
- Verificar funcionamiento en producción (Azure Container Intelligence)

### 📝 Notas para Equipo

- **Para nuevos flujos:** Agregar definición en `appsettings.json` bajo `Classification:Flows` con clave única y array `Providers`.
- **Para desarrollo local:** Replicar estructura de `appsettings.json` en `local.settings.json` (Variables de entorno).
- **PostConfigure es idiomático:** Patrón recomendado por .NET para config binding de tipos complejos; documentado en MS Docs.

---

## 6. Referencias

- **ADO Epic 99461:** Pipeline de clasificación configurable (✅ Done)
- **Related WI Closed:** 99709, 99710, 99714, 99715 (✅ Done)
- **Commits:**
  - a5601a1: fix(classification): Configuration binding for ClassificationRoutingSettings.Flows dictionary
  - b26fb8d: feat(catalog): Add Nombre field to TdnCatalogItem for complete taxonomy display
- **Documentos Base:**
  - `docs/03_DISENO_TECNICO_DETALLADO.md`
  - `docs/CATALOGO_APP_SETTINGS.md`
  - `docs/05_MANUAL_USO_CONFIGURACION.md`

---

**Próximo paso recomendado:** Validar E2E con test-ingest y confirmar flujos de clasificación en orden correcto.
