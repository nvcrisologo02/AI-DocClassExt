# Prioridad de umbrales — Orquestación por actividad

Abre el fichero `umbrales_prioridad.mmd` con un visor Mermaid o pega el siguiente bloque en un Markdown con preview Mermaid.

```mermaid
flowchart TD
  Req["Petición HTTP<br/>(instrucciones...)"] --> Orch["Orquestador<br/>(resuelve umbrales por actividad)"]
  Orch --> Clasif["ClasificarActivity"]
  Orch --> Extrac["ExtraerActivity"]
  Orch --> Valid["ValidarActivity"]

  Clasif --> Cresolve["Resolver umbralClasifFallback"]
  Cresolve --> C1["1) Petición: instrucciones.classification.umbral"]
  C1 --> C2["2) Tipología: confidenceConfig.clasifUmbralFallback"]
  C2 --> C3["3) Modelo/Servidor: Classification:GptFallback:FallbackThreshold"]
  C3 --> C4["4) Default: _gptClasifSettings.FallbackThreshold"]

  Cresolve --> Cdec{ConfianzaDI >= umbral?}
  Cdec -->|Sí| Cpass["Usar ConfianzaDI → ConfianzaClasificacion"]
  Cdec -->|No| Cfb["Fallback a GPT → ConfianzaGPT"]
  Cfb --> Cprop["ConfigurableClasificarDataProvider propaga ConfianzaDI + ConfianzaGPT"]

  Extrac --> Eresolve["Resolver umbrales de extracción"]
  Eresolve --> Ecomp["Umbral completitud"]
  Eresolve --> Econf["Umbral confianza"]
  Ecomp --> Ecomp_chain["Petición → Tipología → Legado → Servidor"]
  Econf --> Econf_chain["Petición → Tipología → Legado → Servidor"]
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
  EstadoResolve --> Sreq["Petición (si existe)"]
  Sreq --> Stop["Tipología: confidenceConfig.umbralOK / umbralRevision"]
  Stop --> Sdef["Defaults: confidenceConfig global (ej. 0.85 / 0.70)"]

  EstadoResolve --> Estado{ConfianzaGlobal >= umbralOK?}
  Estado -->|Sí| OK["EstadoCalidad = OK"]
  Estado -->|No| Estado2{ConfianzaGlobal >= umbralRevision?}
  Estado2 -->|Sí| REV["EstadoCalidad = REVISION"]
  Estado2 -->|No| ERR["EstadoCalidad = ERROR"]
```

Notas rápidas:
- La jerarquía por criterio es: **Petición → Tipología → Modelo/Servidor → Default**.
- En extracción hay dos criterios independientes (completitud y confianza) y **si cualquiera falla** se activa el fallback LLM.
- `confidenceConfig` por tipología puede sobrescribir umbrales y pesos (ver `*.validation.json`).

¿Quieres que genere también un SVG/PNG del diagrama y lo añada al repositorio?