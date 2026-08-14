# Clasificación restringida a un conjunto de tipologías candidatas — Diseño

**Fecha:** 2026-08-11
**Estado:** Aprobado (pendiente de work items en Azure DevOps antes de implementar)
**Alcance:** Solo backend/API (DocumentIA.Functions). Cliente Batch y webapp Admin quedan fuera de esta iteración.

## Problema

Hoy la clasificación siempre opera contra el catálogo completo de tipologías publicadas. Hay escenarios en los que el caller sabe de antemano que el documento solo puede ser de un conjunto acotado de tipos (p. ej. un lote procedente de un contexto concreto) y quiere: (a) que el sistema solo intente clasificar dentro de ese conjunto, y (b) un resultado explícito de "no encaja" en lugar de una clasificación forzada fuera de su lista.

`Instrucciones.ExpectedType` existe como hint blando de **una** tipología, pero no restringe nada. Esta funcionalidad es su generalización: un **conjunto** de candidatas con semántica de restricción dura.

## Decisiones de producto (cerradas con el usuario)

1. **Semántica**: restricción estricta. El clasificador solo ve el subconjunto y puede responder DESCONOCIDO. Opcionalmente (flag por petición), si el resultado es DESCONOCIDO se hace una pasada libre contra el catálogo completo y se devuelve como propuesta informativa, sin actuar sobre ella.
2. **Granularidad**: códigos de tipología concretos (columna `Codigo`, ej. `SERE-25`, `ESCR-01`, `nota-simple`). Las familias TDN1 para la Fase 1 se derivan automáticamente.
3. **Downstream con DESCONOCIDO**: igual que un documento sin clasificar (flujo de tipología virtual existente): se permite resumen documental y operaciones de prompt; no hay extracción específica, ni AssetResolver, ni subida a GDC, ni integración.
4. **Proveedores**: universal. Filtrado real de catálogos en la vía GPT + validación de pertenencia del resultado sea cual sea el proveedor (DI, híbrido, reglas, mock).
5. **Códigos inválidos**: se ignoran con aviso (log/auditoría + lista en la salida). Si todos son inválidos → 400.
6. **`ExpectedType`**: independiente; sigue siendo hint blando y puede convivir con la restricción sin regla cruzada.

## Diseño

### 1. Contrato de entrada

Nuevo objeto opcional en `Instrucciones` (`src/backend/DocumentIA.Core/Models/ContratoEntrada.cs`):

```json
"instrucciones": {
  "restriccionTipologias": {
    "codigos": ["SERE-25", "ESCR-01", "nota-simple"],
    "proponerSiDesconocido": true
  }
}
```

- `restriccionTipologias` `null`/ausente = comportamiento actual sin cambios (compatibilidad total hacia atrás).
- `codigos`: lista de códigos de tipología. Comparación case-insensitive con trim.
- `proponerSiDesconocido` (default `false`): activa la propuesta informativa cuando el resultado final es DESCONOCIDO.

**Validación en `IngestAPITrigger`:**

- Se resuelven los códigos contra las tipologías publicadas (consulta cacheada, misma pauta que los catálogos de prompt: caché de 5 min).
- Códigos no reconocidos → se descartan, se registra aviso y se devuelven en la salida como `codigosIgnorados`.
- Si tras filtrar la lista queda vacía (todos inválidos, o lista vacía enviada explícitamente) → HTTP 400 con detalle de los códigos rechazados.
- El conjunto normalizado (trim, canonicalizado al `Codigo` de BD) viaja en el `ContratoEntrada` hacia el orquestador; `ClasificacionInput.Entrada` ya transporta `Instrucciones` completas, por lo que llega a todos los proveedores sin cambios de fontanería.

### 2. Vía GPT: catálogos filtrados y prompt

Cambios en `GptClasificarDataProvider` y `ClassificationTipologiaPromptBuilder` (`src/backend/DocumentIA.Core/Configuration/ClassificationTipologiaPromptBuilder.cs`), solo cuando hay restricción activa:

- **Fase 1 (TDN1)**: las familias se derivan del `ResolvedTdn1` de las tipologías permitidas; `{TDN1_CATALOG}` solo lista esas familias. El prompt instruye explícitamente que si el documento no encaja en ninguna familia listada debe responder `"tdn1": null` (el formato de respuesta actual ya lo admite).
- **Fase 2 (TDN2)**: `{TDN2_CATALOG}` solo lista las tipologías permitidas de la familia elegida. El atajo de `TDN2_Prompt` custom por familia **se omite en modo restringido** (ese prompt fijo lista la familia completa y violaría la restricción); se usa siempre la generación dinámica filtrada. El formato de respuesta de Fase 2 se amplía para admitir `"tdn2": null` = ninguna de las listadas encaja.
- **Caché**: las claves de caché de catálogos incorporan un hash del conjunto normalizado (ordenado, uppercase). Peticiones con el mismo conjunto reutilizan catálogo; el flujo sin restricción conserva sus claves actuales intactas.

### 3. Router: pertenencia, fin de cadena y propuesta

Cambios en `ConfigurableClasificarDataProvider` (`src/backend/DocumentIA.Functions/Services/ConfigurableClasificarDataProvider.cs`):

- **`IsSatisfactory`** añade una condición cuando hay restricción: el código detectado debe pertenecer al conjunto. Si DI o reglas devuelven algo fuera de la lista, la cadena continúa con el siguiente proveedor (mismo mecanismo que hoy con confianza insuficiente). El descarte queda trazado en `DetalleProveedores` con motivo `fuera_de_conjunto_restringido:<código>`.
- **Fin de cadena**: si se agota el flujo (incluido el fallback global, que en la vía GPT también opera restringido) sin resultado dentro del conjunto → resultado final `TipologiaDetectada = "Desconocido"`, `Confianza = 0.0`, `FallbackRazon = "fuera_de_conjunto_restringido"`. Se aprovecha que `"Desconocido"` ya es valor centinela reconocido en el pipeline.
- **Propuesta opcional** (`proponerSiDesconocido = true` y resultado final DESCONOCIDO):
  - Si algún proveedor de la cadena ya propuso un código fuera del conjunto con confianza ≥ umbral efectivo, se reutiliza como propuesta sin llamada extra.
  - En caso contrario, una única llamada GPT sin restricción rellena la propuesta.
  - El resultado va a `PropuestaTipologia` (campo existente en el contrato de salida) y se anota en `DetalleProveedores` como `propuesta_libre` con su confianza. No se actúa sobre ella (sin extracción, sin GDC, sin integración).

### 4. Salida y comportamiento aguas abajo

- DESCONOCIDO reutiliza el flujo existente de documento no clasificado/tipología virtual: la ejecución se persiste con su resultado; se permite resumen documental y operaciones de prompt; no hay extracción específica de tipología, ni AssetResolver, ni subida a GDC, ni integración.
- El detalle de ejecución de la salida incluye: el conjunto solicitado, `codigosIgnorados`, el resultado (dentro del conjunto o DESCONOCIDO) y, si aplica, `PropuestaTipologia` con su confianza.

### 5. Casos límite

- Lista con un solo código = modo "confirma si es X o dime que no". Válido, sin lógica especial.
- Restricción + `ClassificationOnly` conviven sin regla extra.
- Restricción + `nivelClasificacion = TDN1` → HTTP 400: el nivel TDN1 devuelve familias, no tipologías, y no puede honrar una restricción expresada a granularidad de código de tipología. La restricción requiere `TDN1_TDN2`.
- 429/rate-limit agotado sigue propagando `PENDIENTE_REINTENTO` (no se degrada a DESCONOCIDO), como hoy.
- Chequeo de duplicados: sin cambios (va por hash del documento); un mismo documento con conjuntos distintos sigue la política actual de duplicados.
- Provider `mock` pasa por la misma validación de pertenencia en el router (útil para E2E).

### 6. Testing

- **Unit**:
  - Builder de catálogos filtrados: derivación de familias TDN1 desde códigos, filtrado de Fase 2, exclusión del `TDN2_Prompt` custom en modo restringido, claves de caché por conjunto.
  - Validación del trigger: inválidos parciales (aviso + continúa), todos inválidos (400), lista vacía (400), normalización case-insensitive.
  - `IsSatisfactory` con pertenencia: proveedor fuera del conjunto → cadena continúa; dentro del conjunto y confianza suficiente → satisfactorio.
  - Propuesta opcional: con reutilización de resultado previo de la cadena y con llamada GPT extra; flag desactivado → sin propuesta.
- **E2E de provider** (con mocks de OpenAI, patrón de `GptClasificarDataProviderE2eTests`): documento dentro del conjunto; fuera del conjunto → DESCONOCIDO; fuera del conjunto + `proponerSiDesconocido` → DESCONOCIDO con `PropuestaTipologia`.

## Fuera de alcance

- Exposición de la funcionalidad en el cliente Batch y en la webapp Admin.
- Estados de salida nuevos (se reutiliza el flujo de no clasificado existente).
- Restricción por familias TDN1 o listas mixtas familia+tipología.
