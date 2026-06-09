# AB#99750 [OPT-1]: Plan de Caching JSON — Optimización de Configuración

**Fecha:** 2026-06-09  
**Rama:** `feature/opt-1-json-caching-99750`  
**Work Item:** AB#99750  
**Objetivo:** Implementar caching eficiente para configuraciones JSON que se leen frecuentemente en el pipeline de clasificación.

---

## 1. Motivación y Contexto

- **Problema:** Las configuraciones JSON se leen del almacenamiento en cada invocación de Functions, causando latencia I/O repetida.
- **Solución:** Caching en memoria con TTL configurable y validación de integridad.
- **Impacto esperado:** Reducción de latencia de lectura, menor carga en Storage.

---

## 2. Arquitectura de Caching

### 2.1 Componentes principales

| Componente | Responsabilidad |
|---|---|
| `IConfigurationCache` | Interfaz de contrato de caching (GET/SET/REMOVE/CLEAR/STATS) |
| `MemoryCacheProvider` | Implementación con `MemoryCache` de .NET |
| `RedisCacheProvider` | Implementación distribuida (Future: AB#99XXX) |
| `CacheKeyBuilder` | Generador estándar de claves de cache |
| `CacheManager` | Coordinador de invalidación y refresh |

### 2.2 Claves de cache

```
cfg:classification:{environment}:{version}
cfg:typology:{version}
cfg:rules:{ruleSet}:{version}
```

---

## 3. Ciclo de vida del cache

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Leer desde storage (Azure Blob/JSON)                      │
│    ↓                                                         │
│ 2. Deserializar y validar                                    │
│    ↓                                                         │
│ 3. Almacenar en cache con TTL (ej: 5 min - 1 hora)          │
│    ↓                                                         │
│ 4. Servir desde cache en próximas invocaciones               │
│    ↓                                                         │
│ 5. Invalidar por cambio/expiración/manual                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. TTL (Time To Live) por tipo de configuración

| Configuración | TTL Recomendado | Justificación |
|---|---|---|
| Clasificación (rules) | 1 hora | Frecuente lectura, cambios raros |
| Tipología | 4 horas | Estable, cambios excepcionales |
| Modelos (ClassificationModel) | 8 horas | Pesado, cambios programados |
| Reglas dinámicas | 15 minutos | Alta volatilidad esperada |

---

## 5. Plan de implementación

### Fase 1: Scaffolding (ESTA RAMA)
- [x] Interfaz `IConfigurationCache` con métodos base
- [x] Clase `CacheStats` para métricas
- [x] Documentación de arquitectura
- [ ] Interfaz `ICacheKeyBuilder`
- [ ] Enums de tipo de configuración y entorno

### Fase 2: Implementación en memoria (próxima rama)
- [ ] `MemoryCacheProvider` con `System.Runtime.Caching.MemoryCache`
- [ ] `CacheKeyBuilder` estándar
- [ ] Tests unitarios de hit/miss/expiration
- [ ] Integración en `AzureContentUnderstandingProvider` (piloto)

### Fase 3: Validación y ajustes
- [ ] Tests E2E con carga real
- [ ] Métricas en Application Insights
- [ ] Tuning de TTLs según observabilidad
- [ ] Documentación de configuración

### Fase 4: Escalabilidad (Future)
- [ ] Implementación con Redis (distribuida)
- [ ] Propagación de invalidaciones entre instancias
- [ ] Policy de evicción (LRU, LFU)

---

## 6. Interfaces de extensión (previsión)

```csharp
// Invalidación reactiva por cambios en Blob
public interface ICacheInvalidationStrategy
{
    Task OnStorageChangedAsync(string configKey);
}

// Pre-warming y calidez de cache
public interface ICacheWarmingStrategy
{
    Task WarmCacheAsync();
}
```

---

## 7. Configuración esperada en appsettings.json

```json
{
  "Caching": {
    "Enabled": true,
    "Provider": "Memory",
    "DefaultTtl": "01:00:00",
    "Configs": {
      "Classification": { "Ttl": "01:00:00" },
      "Typology": { "Ttl": "04:00:00" },
      "Models": { "Ttl": "08:00:00" },
      "DynamicRules": { "Ttl": "00:15:00" }
    },
    "MaxCacheSize": "104857600",
    "EnableMetrics": true
  }
}
```

---

## 8. Métricas y observabilidad

### Contadores en Application Insights

- `cache_hit_count` (por tipo de config)
- `cache_miss_count` (por tipo de config)
- `cache_eviction_count` (por razón: ttl, size, manual)
- `cache_load_time_ms` (tiempo de lectura + deserialización)

### Alertas propuestas

- Hit rate < 50% → puede indicar TTL muy corto o invalidaciones frecuentes
- Tamaño de cache > MaxSize → revisar políticas de evicción

---

## 9. Criterios de aceptación

- [x] Interfaz clara y sin dependencias externas
- [x] Documentación de tipos y responsabilidades
- [ ] Implementación memory con >80% cobertura unitaria
- [ ] Tests E2E demostrando reducción de latencia
- [ ] Métricas reportadas en Application Insights
- [ ] TTLs calibrados según carga real

---

## 10. Referencias y versionado

- **RFC/ADR:** Pending review
- **Impacto:** Core, Functions, Admin (lectura)
- **Ruptura de API:** No
- **Versionado:** Semver compatible, no requiere migración

---

## Próximos pasos

1. Crear rama `feature/opt-1-json-caching-cache-providers` para implementar providers
2. PR con reviewers: @arquitecto, @devops-lead
3. Integración piloto en AzureContentUnderstandingProvider
4. Validación E2E y métricas (AB#99750-E2E)

---

**Estado del plan:** SCAFFOLDING_COMPLETE  
**Última actualización:** 2026-06-09 — Rama creada con interfaz base
