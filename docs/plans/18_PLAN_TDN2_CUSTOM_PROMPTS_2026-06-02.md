# Plan de Implementación: TDN2 Custom Prompts por Familia TDN1

**Fecha**: 2026-06-02  
**Estado**: PLANIFICACIÓN  
**Objetivo**: Permitir prompts personalizados por familia TDN1 para clasificación Phase 2, con fallback a generación dinámica actual

---

## 📋 Resumen Ejecutivo

Actualmente, el sistema genera dinámicamente el catálogo de tipologías TDN2 consultando la tabla `Tipologias` y filtrando por `ResolvedTdn1`. Este plan introduce la posibilidad de **customizar el prompt completo de Phase 2** por familia TDN1, almacenándolo en la tabla `CatalogoTdn1`.

### Estrategia de Implementación

**Fallback inteligente**:
- Si `CatalogoTdn1.TDN2_Prompt` IS NULL → Generación dinámica (comportamiento actual)
- Si `CatalogoTdn1.TDN2_Prompt` IS NOT NULL → Usar prompt personalizado

**Contenido híbrido**:
El campo `TDN2_Prompt` contendrá **instrucciones específicas de la familia** (incluyendo el catálogo de tipologías si se desea). El sistema seguirá inyectando automáticamente:
- El código TDN1 resuelto en Phase 1
- El contenido extraído del documento (texto/markdown)
- Las instrucciones de formato JSON de respuesta

**Migración gradual**:
Los 60 registros actuales de `CatalogoTdn1` empezarán con `TDN2_Prompt = NULL`, permitiendo poblarlos gradualmente sin disrupciones.

---

## 🎯 Work Items en Azure DevOps

### Epic: Flexibilización de Prompts de Clasificación GPT

**Epic ID**: *(por asignar en ADO)*  
**Descripción**: Permitir personalización de prompts de clasificación Phase 2 por familia TDN1, manteniendo fallback a generación dinámica

---

### Feature 1: Modelo de Datos y Repositorio

**Feature ID**: *(por asignar)*  
**Título**: Agregar campo TDN2_Prompt a CatalogoTdn1 con soporte en repositorio

#### User Story 1.1: Agregar campo TDN2_Prompt a tabla CatalogoTdn1

**Descripción**:
```
Como desarrollador backend
Quiero agregar el campo TDN2_Prompt (nvarchar(MAX) NULL) a la tabla CatalogoTdn1
Para poder almacenar prompts personalizados de clasificación Phase 2 por familia
```

**Criterios de Aceptación**:
- [ ] Campo `TDN2_Prompt` agregado a `CatalogoTdn1Entity.cs` como `string? TDN2_Prompt { get; set; }`
- [ ] Migración EF Core generada y aplicada
- [ ] Los 60 registros existentes tienen `TDN2_Prompt = NULL` por defecto
- [ ] Documentación XML en la propiedad explicando su propósito

**Archivos Afectados**:
- `src/backend/DocumentIA.Data/Entities/CatalogoTdn1Entity.cs`
- `src/backend/DocumentIA.Data/Migrations/*_AddTdn2PromptToCatalogoTdn1.cs` (generado)

**Comandos**:
```powershell
# Generar migración
dotnet ef migrations add AddTdn2PromptToCatalogoTdn1 `
  --project src\backend\DocumentIA.Data `
  --startup-project src\backend\DocumentIA.Functions `
  --context DocumentIADbContext

# Aplicar migración (local)
dotnet ef database update `
  --project src\backend\DocumentIA.Data `
  --startup-project src\backend\DocumentIA.Functions `
  --context DocumentIADbContext
```

**Estimación**: 2 SP  
**Prioridad**: Alta

---

#### User Story 1.2: Extender ICatalogoTdnRepository para obtener prompt personalizado

**Descripción**:
```
Como desarrollador backend
Quiero un método en ICatalogoTdnRepository que retorne el TDN2_Prompt de una familia
Para poder usarlo en el builder de prompts de clasificación
```

**Criterios de Aceptación**:
- [ ] Nuevo método en interfaz: `Task<string?> GetTdn2PromptByFamiliaAsync(string tdn1Codigo, CancellationToken cancellationToken = default)`
- [ ] Implementación en `CatalogoTdnRepository` que:
  - Normaliza el código (Trim + ToUpperInvariant)
  - Consulta `CatalogoTdn1` por `Codigo`
  - Retorna `TDN2_Prompt` (puede ser NULL)
  - Usa `AsNoTracking()` y `FirstOrDefaultAsync()`
- [ ] Tests unitarios:
  - Familia existente con prompt NULL → retorna NULL
  - Familia existente con prompt poblado → retorna el prompt
  - Familia inexistente → retorna NULL
  - Código vacío/nulo → ArgumentException

**Archivos Afectados**:
- `src/backend/DocumentIA.Data/Repositories/ICatalogoTdnRepository.cs`
- `src/backend/DocumentIA.Data/Repositories/CatalogoTdnRepository.cs`
- `src/backend/DocumentIA.Tests.Unit/Repositories/CatalogoTdnRepositoryTests.cs` (nuevos tests)

**Estimación**: 3 SP  
**Prioridad**: Alta

---

### Feature 2: Lógica de Prompts con Fallback

**Feature ID**: *(por asignar)*  
**Título**: Implementar fallback entre prompt custom y generación dinámica en ClassificationTipologiaPromptBuilder

#### User Story 2.1: Modificar BuildTdn2CatalogByFamilia con lógica de fallback

**Descripción**:
```
Como sistema de clasificación GPT
Quiero que BuildTdn2CatalogByFamilia consulte primero el TDN2_Prompt custom
Para usar prompt personalizado si existe, o generar dinámicamente si no
```

**Criterios de Aceptación**:
- [ ] `BuildTdn2CatalogByFamilia(string tdn1Codigo)` implementa lógica:
  1. Consultar `ICatalogoTdnRepository.GetTdn2PromptByFamiliaAsync(tdn1Codigo)`
  2. Si retorna valor NOT NULL → retornar ese prompt directamente
  3. Si retorna NULL → ejecutar lógica actual de generación dinámica
- [ ] Caché existente (TTL 5 min) sigue funcionando igual
- [ ] Logging cuando se usa prompt custom vs dinámico (nivel Debug)
- [ ] Sin cambios en firma pública del método
- [ ] Tests unitarios:
  - Familia con prompt custom → retorna prompt custom
  - Familia con prompt NULL → retorna catálogo generado dinámicamente
  - Caché funciona correctamente en ambos casos

**Archivos Afectados**:
- `src/backend/DocumentIA.Core/Configuration/ClassificationTipologiaPromptBuilder.cs`
- `src/backend/DocumentIA.Tests.Unit/Services/Classification/*` (tests existentes + nuevos)

**Notas Técnicas**:
```csharp
// Pseudocódigo de la lógica
public string BuildTdn2CatalogByFamilia(string tdn1Codigo)
{
    var cacheKey = $"clasificacion:catalogo:tdn2:{normalizedFamily}";
    
    return _cache.GetOrCreate(cacheKey, entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogoTdnRepository>();
        
        // 1. Intentar obtener prompt custom
        var customPrompt = repository.GetTdn2PromptByFamiliaAsync(tdn1Codigo)
            .GetAwaiter()
            .GetResult();
        
        if (!string.IsNullOrWhiteSpace(customPrompt))
        {
            _logger.LogDebug("Using custom TDN2 prompt for family {Tdn1Codigo}", tdn1Codigo);
            return customPrompt;
        }
        
        // 2. Fallback: generación dinámica (código actual)
        _logger.LogDebug("Using dynamic TDN2 catalog generation for family {Tdn1Codigo}", tdn1Codigo);
        var tipologiaRepo = scope.ServiceProvider.GetRequiredService<ITipologiaRepository>();
        var db = scope.ServiceProvider.GetRequiredService<DocumentIADbContext>();
        
        // ... (lógica actual de generación dinámica)
    });
}
```

**Estimación**: 5 SP  
**Prioridad**: Alta

---

#### User Story 2.2: Validar integración en GptClasificarDataProvider

**Descripción**:
```
Como sistema de clasificación GPT
Quiero que el flujo Phase 1 → Phase 2 use correctamente el nuevo prompt builder
Para clasificar documentos con prompts custom o dinámicos sin cambios en el provider
```

**Criterios de Aceptación**:
- [ ] `GptClasificarDataProvider` NO requiere cambios (confirmación via análisis)
- [ ] El método `CompleteChatAsync` en Phase 2 recibe el catálogo correcto (custom o dinámico)
- [ ] Variables inyectadas automáticamente funcionan:
  - `tdn1Code` inyectado en `phase2UserText`
  - `contextoTexto` inyectado en `phase2UserText`
  - `phase2ResponseInstruction` inyectado en `phase2SystemText`
- [ ] Test E2E manual con:
  - Familia con prompt NULL → clasificación funciona (comportamiento actual)
  - Familia con prompt custom → clasificación usa nuevo prompt

**Archivos Afectados**:
- `src/backend/DocumentIA.Functions/Services/GptClasificarDataProvider.cs` (verificación, sin cambios esperados)
- Scripts de test E2E (manual)

**Estimación**: 3 SP  
**Prioridad**: Media

---

### Feature 3: Tests Automatizados

**Feature ID**: *(por asignar)*  
**Título**: Batería de tests E2E para validar clasificación con prompts custom

#### User Story 3.1: Tests E2E con prompts custom vs dinámicos

**Descripción**:
```
Como QA/Developer
Quiero tests E2E que validen la clasificación Phase 2 con ambos tipos de prompts
Para garantizar que el fallback funciona correctamente en escenarios reales
```

**Criterios de Aceptación**:
- [ ] Test E2E 1: Familia ESCR con `TDN2_Prompt = NULL` → usa generación dinámica → clasifica correctamente
- [ ] Test E2E 2: Familia ESCR con `TDN2_Prompt` custom simple → usa prompt custom → clasifica correctamente
- [ ] Test E2E 3: Familia DOCN con prompt custom que incluye contexto especial → clasifica correctamente
- [ ] Tests documentados en suite E2E existente
- [ ] Casos de prueba incluyen verificación de logs (prompt custom vs dinámico)

**Archivos Afectados**:
- `tests/E2E/*` (nuevos tests o extensión de existentes)
- `docs/auxiliares/tests/test-tdn2-custom-prompts.md` (documentación casos)

**Estimación**: 5 SP  
**Prioridad**: Media

---

### Feature 4: Documentación

**Feature ID**: *(por asignar)*  
**Título**: Actualizar documentación técnica y funcional con TDN2 Custom Prompts

#### User Story 4.1: Actualizar anexo de clasificación dos fases

**Descripción**:
```
Como arquitecto/desarrollador futuro
Quiero documentación actualizada del sistema de clasificación
Para entender cómo funcionan los prompts custom por familia
```

**Criterios de Aceptación**:
- [ ] `ANEXO_PROVIDER_GPT_CLASIFICACION_DOS_FASES.md` actualizado con:
  - Nueva sección "2.3 Custom Prompts por Familia"
  - Diagrama de decisión (custom vs dinámico)
  - Ejemplo de prompt custom
  - Explicación de variables auto-inyectadas
- [ ] `03_DISENO_TECNICO_DETALLADO.md` actualizado con:
  - Nuevo campo `TDN2_Prompt` en sección de modelo de datos
  - Actualización en `ClassificationTipologiaPromptBuilder` (lógica fallback)
- [ ] `07_ROADMAP_PENDIENTES.md` actualizado con entrada DONE para esta feature

**Archivos Afectados**:
- `docs/ANEXO_PROVIDER_GPT_CLASIFICACION_DOS_FASES.md`
- `docs/03_DISENO_TECNICO_DETALLADO.md`
- `docs/07_ROADMAP_PENDIENTES.md`

**Estimación**: 3 SP  
**Prioridad**: Baja

---

### Feature 5: UI de Gestión de Catálogos (FUTURO - NO IMPLEMENTAR AHORA)

**Feature ID**: *(por asignar)*  
**Título**: CRUD de CatalogoTdn1 y CatalogoTdn2 en DocumentIA.Admin

#### User Story 5.1: Interfaz de gestión de CatalogoTdn1

**Descripción**:
```
Como administrador del sistema
Quiero una interfaz web para gestionar las familias TDN1
Para editar códigos, nombres, descripciones y prompts custom sin tocar la BD directamente
```

**Criterios de Aceptación**:
- [ ] Página `/admin/catalogos/tdn1` con lista de familias
- [ ] Formulario de edición con campos:
  - Codigo (readonly, clave)
  - Nombre (requerido)
  - Descripcion (opcional)
  - TDN2_Prompt (textarea grande, opcional, con vista previa)
- [ ] Validaciones:
  - Código único
  - Longitud máxima respetada
  - Prompt JSON bien formateado (opcional)
- [ ] Invalidación de caché al guardar cambios
- [ ] Auditoría de cambios (usuario, fecha, cambio)

**Archivos Afectados**:
- `src/frontend/DocumentIA.Admin/*` (nuevas páginas/componentes)
- Backend API: nuevos endpoints en Functions o Admin backend

**Estimación**: 13 SP  
**Prioridad**: Baja (FUTURO)  
**Estado**: NOT STARTED - Pendiente de planificación detallada

---

#### User Story 5.2: Interfaz de gestión de CatalogoTdn2

**Descripción**:
```
Como administrador del sistema
Quiero una interfaz web para gestionar los subtipos TDN2
Para editar códigos, nombres y descripciones sin tocar la BD directamente
```

**Criterios de Aceptación**:
- [ ] Página `/admin/catalogos/tdn2` con lista filtrable por familia TDN1
- [ ] Formulario de edición con campos:
  - Codigo (readonly, clave)
  - Nombre (requerido)
  - Descripcion (opcional)
  - CodigoTdn1 (dropdown con familias TDN1)
- [ ] Validaciones:
  - Código único
  - Longitud máxima respetada
  - CodigoTdn1 debe existir
- [ ] Invalidación de caché al guardar cambios

**Archivos Afectados**:
- `src/frontend/DocumentIA.Admin/*` (nuevas páginas/componentes)
- Backend API: nuevos endpoints en Functions o Admin backend

**Estimación**: 13 SP  
**Prioridad**: Baja (FUTURO)  
**Estado**: NOT STARTED - Pendiente de planificación detallada

---

## 📐 Arquitectura de la Solución

### Diagrama de Flujo: Resolución de Prompt Phase 2

```
┌──────────────────────────────────────────────────────────────┐
│ GptClasificarDataProvider.ClasificarDocumentoAsync()         │
└────────────────────────┬─────────────────────────────────────┘
                         │
                         ▼
          ┌──────────────────────────────┐
          │ PHASE 1: Resolver TDN1       │
          │ → tdn1Code = "ESCR"          │
          └──────────────┬───────────────┘
                         │
                         ▼
          ┌──────────────────────────────────────────────────┐
          │ ClassificationTipologiaPromptBuilder             │
          │   .BuildTdn2CatalogByFamilia("ESCR")             │
          └──────────────┬───────────────────────────────────┘
                         │
                         ▼
          ┌──────────────────────────────────────────────────┐
          │ Consultar caché IMemoryCache                     │
          │ Key: "clasificacion:catalogo:tdn2:ESCR"          │
          │ TTL: 5 minutos                                   │
          └──────────────┬───────────────────────────────────┘
                         │
                ┌────────┴─────────┐
                │ ¿Cache HIT?      │
                └────────┬─────────┘
                         │
         ┌───────────────┼───────────────┐
         │ SÍ                             │ NO
         ▼                                ▼
   ┌─────────────┐           ┌────────────────────────────────┐
   │ Retornar    │           │ ICatalogoTdnRepository          │
   │ catálogo    │           │   .GetTdn2PromptByFamiliaAsync  │
   │ cacheado    │           │   ("ESCR")                      │
   └─────────────┘           └────────────┬───────────────────┘
                                          │
                             ┌────────────┴──────────────┐
                             │ ¿TDN2_Prompt IS NULL?     │
                             └────────────┬──────────────┘
                                          │
                          ┌───────────────┼──────────────┐
                          │ NO                            │ SÍ
                          ▼                               ▼
            ┌─────────────────────────┐    ┌─────────────────────────────┐
            │ Usar Prompt Custom      │    │ Generación Dinámica         │
            │ (retornar TDN2_Prompt)  │    │ - Consultar ITipologiaRepo  │
            │                         │    │ - Filtrar por ResolvedTdn1  │
            │ Log: "Using custom..."  │    │ - Construir catálogo        │
            └─────────────┬───────────┘    │                             │
                          │                │ Log: "Using dynamic..."     │
                          │                └─────────────┬───────────────┘
                          │                              │
                          └──────────────┬───────────────┘
                                         │
                                         ▼
                          ┌──────────────────────────────┐
                          │ Cachear resultado (5 min)    │
                          │ Retornar catálogo            │
                          └──────────────┬───────────────┘
                                         │
                                         ▼
          ┌──────────────────────────────────────────────────┐
          │ GptClasificarDataProvider                        │
          │ Construir phase2UserText:                        │
          │ - "Familia TDN1 resuelta: ESCR"                  │
          │ - "Tipologías disponibles:\n{catálogo}"          │
          │ - "CONTENIDO DEL DOCUMENTO:\n{contextoTexto}"    │
          └──────────────┬───────────────────────────────────┘
                         │
                         ▼
          ┌──────────────────────────────┐
          │ PHASE 2: Resolver TDN2       │
          │ → CompleteChatAsync()        │
          │ → tdn2Code = "ESCR-06"       │
          └──────────────────────────────┘
```

### Estructura de Prompt Custom (Ejemplo)

**Escenario**: Familia ESCR (Escrituras) con requisitos especiales de clasificación

```markdown
## CatalogoTdn1.TDN2_Prompt para ESCR:

Tipologías de escrituras disponibles:

- ESCR-01 [Escritura: compraventa] Documento notarial que formaliza la transmisión de un bien inmueble mediante pago de precio. Debe contener: identificación de comprador/vendedor, descripción del inmueble, precio pactado.

- ESCR-06 [Escritura: venta] Similar a compraventa, pero puede incluir condiciones especiales de pago o transmisión diferida. Si el documento menciona "venta con reserva" o "venta con condición suspensiva", clasificar aquí.

- ESCR-10 [Escritura: hipoteca] Constitución de garantía real sobre inmueble. Debe contener: identificación de deudor/acreedor hipotecario, capital garantizado, plazo de amortización, descripción de la finca hipotecada.

REGLA ESPECIAL ESCR:
- Si el documento es una escritura de compraventa CON constitución de hipoteca en el mismo acto, priorizar ESCR-01 (compraventa), no ESCR-10.
- Si el documento es SOLO constitución de hipoteca sobre inmueble ya en propiedad, clasificar como ESCR-10.

Si ninguna tipología encaja exactamente, devolver la más cercana y justificar en máximo 200 caracteres.
```

**Variables Auto-Inyectadas** (el sistema las añade automáticamente al construir `phase2UserText`):
```
Familia TDN1 resuelta: ESCR

{contenido del TDN2_Prompt de arriba}

CONTENIDO DEL DOCUMENTO (texto/markdown):
{contextoTexto extraído del PDF}
```

---

## 🔄 Flujo de Trabajo Propuesto

### Fase 1: Implementación Core (Sprint 1)
1. ✅ Crear rama feature desde develop
2. ✅ Implementar US 1.1 (modelo + migración)
3. ✅ Implementar US 1.2 (repositorio + tests)
4. ✅ Implementar US 2.1 (builder + fallback)
5. ✅ Validar US 2.2 (provider sin cambios)
6. ✅ PR → develop
7. ✅ Deploy a DEV para validación

### Fase 2: Testing y Documentación (Sprint 1-2)
8. ✅ Implementar US 3.1 (tests E2E)
9. ✅ Implementar US 4.1 (documentación)
10. ✅ PR → develop
11. ✅ Deploy a DEV/QA

### Fase 3: Población Gradual de Prompts (Fuera de sprint)
12. 🔄 Identificar familias TDN1 que requieren prompts custom
13. 🔄 Crear prompts custom por familia (colaboración con expertos dominio)
14. 🔄 Validar clasificación con prompts custom via tests E2E
15. 🔄 UPDATE scripts SQL para poblar `TDN2_Prompt` en producción

### Fase 4: UI de Gestión (FUTURO - Sprint pendiente)
16. ⏸️ Planificación detallada de US 5.1 y 5.2
17. ⏸️ Implementación de CRUD en Admin
18. ⏸️ Testing de UI
19. ⏸️ Deploy

---

## 📊 Estimaciones

| Feature | User Stories | Story Points | Días (estimado) |
|---------|-------------|--------------|-----------------|
| Feature 1 | US 1.1, 1.2 | 5 SP | 1-2 días |
| Feature 2 | US 2.1, 2.2 | 8 SP | 2-3 días |
| Feature 3 | US 3.1 | 5 SP | 1-2 días |
| Feature 4 | US 4.1 | 3 SP | 1 día |
| **TOTAL IMPLEMENTACIÓN** | **7 US** | **21 SP** | **5-8 días** |
| Feature 5 (Futuro) | US 5.1, 5.2 | 26 SP | 6-8 días |

**Nota**: Los tiempos son estimaciones para 1 developer full-time. Ajustar según disponibilidad y complejidad real.

---

## ⚠️ Riesgos y Mitigaciones

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|--------------|---------|------------|
| Prompts custom mal formateados causan errores en LLM | Media | Alto | - Validación de formato en UI (futuro)<br>- Caché permite rollback rápido<br>- Logging detallado de errores |
| Pérdida de rendimiento por consulta extra a BD | Baja | Medio | - Caché de 5 min absorbe consultas repetidas<br>- Query simple (índice en Codigo) |
| Inconsistencias entre prompt custom y tipologías reales | Media | Medio | - Documentación clara de formato esperado<br>- Tests E2E detectan inconsistencias<br>- Revisión manual de prompts custom |
| Confusión sobre qué variables están auto-inyectadas | Media | Bajo | - Documentación explícita de variables disponibles<br>- Ejemplo de prompt en docs<br>- UI futura muestra vista previa |

---

## 📝 Checklist de Definición de Hecho (DoD)

### Para cada User Story:
- [ ] Código implementado y funcional
- [ ] Tests unitarios escritos y pasando (>80% cobertura en nuevas líneas)
- [ ] Tests E2E actualizados (si aplica)
- [ ] Sin errores de compilación ni warnings críticos
- [ ] Code review aprobado por al menos 1 peer
- [ ] Documentación técnica actualizada (si aplica)
- [ ] Commit con referencia a WI (ej: `AB#12345`)

### Para el Feature completo:
- [ ] Todas las US del feature completadas y merged
- [ ] Tests E2E pasando en DEV
- [ ] Documentación de arquitectura actualizada
- [ ] Demo funcional realizada (si aplica)
- [ ] Migration scripts validados en DEV

---

## 📚 Referencias

- [ANEXO_PROVIDER_GPT_CLASIFICACION_DOS_FASES.md](../ANEXO_PROVIDER_GPT_CLASIFICACION_DOS_FASES.md)
- [03_DISENO_TECNICO_DETALLADO.md](../03_DISENO_TECNICO_DETALLADO.md)
- [07_ROADMAP_PENDIENTES.md](../07_ROADMAP_PENDIENTES.md)
- Azure DevOps: [AI DocClassExt Project](https://sareb.visualstudio.com/AI%20DocClassExt/)

---

## ✅ Aprobaciones

| Rol | Nombre | Fecha | Estado |
|-----|--------|-------|--------|
| Product Owner | TBD | TBD | ⏳ Pendiente |
| Tech Lead | TBD | TBD | ⏳ Pendiente |
| Desarrollador | TBD | TBD | ⏳ Pendiente |

---

**Fin del documento**
