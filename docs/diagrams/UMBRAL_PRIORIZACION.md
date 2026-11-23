# Prioridad de umbrales — Orquestación por actividad

> Última revisión: 2026-11-22 (AB#101447, contraste con `DocumentProcessOrchestrator.cs`, `ConfigurableClasificarDataProvider.cs` y `ConfigurableExtraerDataProvider.cs`). El diagrama original citaba `Classification:GptFallback:FallbackThreshold` y un nivel «Default» de ajustes que no se leen en el código, y un umbral OK/REVISION por petición que no existe en `ContratoEntrada`; se han corregido las cajas. La referencia vigente es [CONFIANZA_AGREGADA.md](../referencias/CONFIANZA_AGREGADA.md). El fichero `umbrales_prioridad.mmd` no está en el repositorio.

Pega el siguiente bloque en un Markdown con preview Mermaid.

```mermaid
flowchart TD
  Req["Petición HTTP<br/>(instrucciones...)"] --> Orch["Orquestador<br/>(resuelve umbrales por actividad)"]
  Orch --> Clasif["ClasificarActivity"]
  Orch --> Extrac["ExtraerActivity"]
  Orch --> Valid["ValidarActivity"]

  Clasif --> Cresolve["Decisión DI→GPT: instrucciones.classification.umbral, o 0.6 fijo (IsSatisfactory)"]
  Clasif --> Cbaja["Check BAJA_CONFIANZA: 1) petición → 2) confidenceConfig.clasifUmbralFallback → 3) UmbralFallbackAplicado (petición → FallbackThreshold del modelo en ModeloConfigs, default 0.6) → 0.6"]

  Cresolve --> Cdec{Tipología válida (no Desconocido/RESTO, dentro del conjunto restringido) y confianza >= umbral?}
  Cdec -->|Sí| Cpass["Usar ConfianzaDI → ConfianzaClasificacion"]
  Cdec -->|No| Cfb["Fallback a GPT → ConfianzaGPT"]
  Cfb --> Cprop["ConfigurableClasificarDataProvider propaga ConfianzaDI + ConfianzaGPT"]

  Extrac --> Eresolve["Resolver umbrales de extracción"]
  Eresolve --> Ecomp["Umbral completitud"]
  Eresolve --> Econf["Umbral confianza"]
  Ecomp --> Ecomp_chain["Petición → Tipología → Legado → MinFieldsRatio del modelo del registro (default 0.5)"]
  Econf --> Econf_chain["Petición → Tipología → Legado → MinFieldsRatio del modelo del registro (default 0.5)"]
  Eresolve --> Edec{ratioCompletitud >= umbralCompletitud AND confianzaCU >= umbralConfianza?}
  Edec -->|Sí| Epass["No fallback → ConfianzaExtraccion"]
  Edec -->|No| Efb["Fallback a GPT → confianza_extraccion (self-report) o ExtracCU calculada"]
  Efb --> Eprop["Propagar ConfianzaExtraccion + ProveedorExtrac"]
  Extrac -.->|"extraction.enabled = false"| Eskip["Omitir ConfianzaExtraccion"]

  Valid --> Vcalc["ValidarActivity<br/>ConfianzaValidacion = 1 - errores/TotalReglas"]

  Cprop --> Global["ConfidenceCalculator.Global<br/>ConfianzaGlobal = MIN(Clasif, Extrac?, Valid)"]
  Eprop --> Global
  Vcalc --> Global

  Global --> EstadoResolve["Resolver umbralOK / umbralRevision"]
  EstadoResolve --> Stop["Tipología: confidenceConfig.umbralOK / umbralRevision (no hay umbral OK/REVISION por petición)"]
  Stop --> Sdef["Defaults: new ConfidenceConfig() (0.85 / 0.70)"]

  EstadoResolve --> Estado{ConfianzaGlobal >= umbralOK?}
  Estado -->|Sí| OK["EstadoCalidad = OK"]
  Estado -->|No| Estado2{ConfianzaGlobal >= umbralRevision?}
  Estado2 -->|Sí| REV["EstadoCalidad = REVISION"]
  Estado2 -->|No| ERR["EstadoCalidad = ERROR"]
```

Notas rápidas:
- La jerarquía por criterio es: **Petición → Tipología → Modelo del registro (`ModeloConfigs`)**; el último recurso en clasificación es 0.6 fijo y en extracción `MinFieldsRatio` (0.5).
- En extracción hay dos criterios independientes (completitud y confianza) y **si cualquiera falla** se activa el fallback LLM.
- `confidenceConfig` por tipología puede sobrescribir umbrales y pesos (ver `*.validation.json`).
