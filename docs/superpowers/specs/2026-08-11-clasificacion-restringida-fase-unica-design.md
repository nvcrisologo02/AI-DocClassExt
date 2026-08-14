# Clasificación restringida en fase única — Diseño

**Fecha:** 2026-08-11
**Estado:** Aprobado
**Contexto:** Evolución de la clasificación restringida (AB#100046, spec `2026-08-11-clasificacion-restringida-tipologias-design.md`), motivada por el E2E en dev con documentos reales de Colabora.

## Problema

La primera iteración de la restricción reutiliza la jerarquía TDN1→TDN2: la Fase 1 elige familia sobre un catálogo de familias filtrado y la Fase 2 elige tipología dentro de la familia. El E2E (rondas 2 y 4, 11 documentos) evidenció dos modos de fallo estructurales:

1. **Fase 1 devuelve `null`**: el modelo identifica la familia *natural* del contenido (acta→ACTR, presupuesto→PRES, informe→ESIN), que no pertenece al conjunto restringido, y la instrucción de "no forzar" hace el resto. La equivalencia de negocio (un acta del CTO *es* la "Aprobación/denegación operación") está escrita en la `gptDescripcion` de la tipología, que vive en Fase 2 — adonde el documento nunca llega.
2. **Compromiso prematuro de familia sin backtracking**: la Fase 1 elige una familia del conjunto (sello→ACUI por la sanción del comité) y la Fase 2 de esa familia, correctamente estricta, devuelve `null`; la tipología correcta (`fich.24`, familia FICH) nunca compite.

Resultado: 0 aciertos "duros" en 11 documentos pese a catálogos y descripciones correctos.

## Decisión (cerrada con el usuario, 2026-08-11)

- **Con restricción activa, se clasifica DIRECTAMENTE contra las tipologías acotadas** en una única pasada, con un catálogo plano que incluye la `gptDescripcion` completa de cada tipología permitida. Sin fase de familia.
- **Si el resultado es `Desconocido` y `proponerSiDesconocido=true`, la propuesta informativa se obtiene con el flujo jerárquico TDN1→TDN2 completo de siempre** (es lo que ya hace la pasada libre del router con `OmitirRestriccionTipologias=true`; no cambia).
- El flujo normal sin restricción (jerárquico, catálogo completo) queda intacto.

## Diseño

### 1. Catálogo plano restringido (`ClassificationTipologiaPromptBuilder`)

Nuevo método `BuildCatalogoPlanoRestringido(IReadOnlyCollection<string> codigosPermitidos) : string`:

- Una línea por tipología permitida publicada: `- <codigo> [<tdn1> / <tdn2>: <nombreTdn2>] <gptDescripcion>` (mismo formato que el catálogo completo `Build()`, filtrado al conjunto).
- Descripción: `ResolvedGptDescripcion` con fallback al nombre (comportamiento existente).
- Caché `IMemoryCache` 5 min con clave `clasificacion:catalogo:plano:r:{hash}` (mismo `ComputeSetHash` existente).

### 2. Respuesta y parser

Nuevo formato de respuesta de la pasada plana:

```json
{"tipologia": "<codigo>" | null, "propuesta": "texto libre", "resumen": "...", "confianza": 0.0-1.0}
```

- `resumen` solo se solicita cuando `GenerarResumenPorDefecto` (mismo mecanismo que la Fase 1 actual).
- Nuevo método `GptHierarchicalClassificationParser.ParseRestringido(...)` que devuelve `(Tipologia|null, Propuesta, Resumen, Confianza)`; `tipologia: null` = ninguna encaja (no es error de parseo). JSON inválido → error de parseo como en las fases actuales.

### 3. Camino plano en `GptClasificarDataProvider`

Cuando `ResolverRestriccion(input)` devuelve conjunto (no null):

- Se construye el prompt con la plantilla de Fase 1 de BD (`Phase1SystemPrompt`/`Phase1UserPrompt`), interpolando `{TDN1_CATALOG}` con el **catálogo plano** y `{DOCUMENT_TEXT}` como hoy, más una instrucción de restricción nueva:
  - El caller garantiza que el documento debería corresponder a una de las tipologías listadas; **elegir la más compatible** por contenido aunque el documento pudiera pertenecer "naturalmente" a otra categoría documental; responder `tipologia: null` SOLO si el contenido no guarda relación razonable con ninguna. (Corrige el exceso de conservadurismo observado.)
  - Formato de respuesta del punto 2 (con o sin `resumen`).
- Una única llamada (`stage "classification.restricted"` para trazas/telemetría).
- Parseo: código devuelto → validar pertenencia al conjunto (case-insensitive) y existencia publicada; válido → `ResultadoClasificacion` normal (`TipologiaDetectada=<codigo canónico>`, `Confianza` self-reported, `Tdn2Detectado` del config de la tipología, `ResumenCombinado`, `PropuestaTipologia=propuesta`). `null`, fuera de conjunto o parseo fallido → `BuildRestriccionDesconocidoResult` (existente) con la propuesta y el resumen si los hay.
- **El camino jerárquico deja de ejecutarse con restricción**: los tres interceptores restringidos de Fase 1/Fase 2 introducidos en la iteración anterior se eliminan (código muerto al no ser alcanzables). `Fase2NingunaTipologiaReason` y su normalización legacy en la degradación de Fase 2 se conservan (protegen el flujo normal).
- `OmitirRestriccionTipologias` se conserva: la pasada libre del router sigue entrando por el camino jerárquico completo.

### 4. Sin cambios

- Trigger y validación (400s, normalización, `codigosIgnorados`).
- Router `ConfigurableClasificarDataProvider`: pertenencia en `IsSatisfactory`, post-proceso, eco `RestriccionTipologias`, propuesta libre jerárquica.
- Contratos de entrada/salida.
- Flujo sin restricción: byte-idéntico.

### 5. Testing

- Unit: catálogo plano (formato, filtrado, caché por hash), `ParseRestringido` (código válido, null, JSON inválido, confianza), provider plano (dentro del conjunto, null→Desconocido, código fuera de conjunto devuelto por el modelo→Desconocido, resumen presente/ausente), instrucción contiene la garantía del caller y el `null`.
- Regresión: suite completa sin cambios en flujo normal; tests del router intactos.

## Referencia E2E

Rondas de prueba en dev (SubmittedBy `test-restriccion-100046-r2`/`-r4`, documentos Colabora): resultados esperados tras esta evolución — actas CTO→`acui.02`, checklist RAC-0→`acui.08`, sellos→`fich.24`, PPTOs→`inli.13`, informes de estado y solicitud de reconsideración→`Desconocido` con propuesta jerárquica (`esin.34`/COMU).
