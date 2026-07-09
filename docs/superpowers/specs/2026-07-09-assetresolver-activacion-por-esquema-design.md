# AssetResolver — Activación por esquema de extracción (refcat / IDUFIR)

**Fecha:** 2026-07-09
**Estado:** Aprobado (pendiente de plan de implementación)

## Problema

El AssetResolver (actividad `ObtenerActivo`, plugin `DM_POSICION_AAII/AACC`) solo se
ejecuta para una tipología si `Instrucciones.AssetResolver.Enabled ?? tipologiaResuelta.AssetResolverEnabled`
es `true` (`DocumentProcessOrchestrator`, paso 5.5). `AssetResolverEnabled` proviene de
`ConfiguracionJson.assetResolver.enabled` (default **false**), resuelto en
`TipologiaVersionResolver`.

Varias tipologías declaran en su esquema de extracción campos de **referencia catastral**
y/o **IDUFIR** (`nota.simple` → `ReferenciaCatastral` + `IDUFIR_CRU`; `IBI`, `IBI_1.1`,
`tasacion` → `ReferenciaCatastral`), pero **ningún seed `*.validation.json` incluye un
bloque `assetResolver`**. La activación real vive en la BBDD (`Tipologias.ConfiguracionJson`,
almacenado verbatim desde el payload de Admin, en camelCase). Donde no se haya configurado
a mano, el AssetResolver **no se dispara** aunque el documento traiga catastral/IDUFIR.

**Objetivo:** para toda tipología **Publicada y Activa** cuyo esquema de extracción declare
un campo de refcat o IDUFIR (en plano o dentro de una colección de objetos), dejar activado
el AssetResolver con el mapeo correcto, mediante un script SQL idempotente sobre
`Tipologias.ConfiguracionJson`. Sin cambios de código.

## Decisiones acordadas

| Decisión | Valor |
| --- | --- |
| Interpretación | **Config por tipología** (migración de datos en BBDD), no gating en runtime |
| Entrega | **Script SQL idempotente** (`UPDATE ... JSON_MODIFY`), parametrizable por entorno, sin credenciales embebidas |
| Alcance | **Auto-descubrimiento por esquema**, filtrado a `Estado = Published (1) AND Activa = 1` |
| Casing | `ConfiguracionJson` se guarda verbatim (camelCase); lectura case-insensitive → el script usa paths camelCase |
| Cambios de código | Ninguno (orquestador y plugin intactos) |

## Contexto técnico verificado

- **Activación:** `ConfiguracionJson.assetResolver.enabled` (default false).
- **Aliases por defecto del plugin** (`FieldAliasesConfig`): IDUFIR = `["IDUFIR","CRU","CodigoRegistroUnico"]`;
  RefCatastral = `["ReferenciaCatastral","RefCatastral","Catastral"]`. El plugin usa el
  `Mapeo*` de la tipología **solo si viene informado**; si no, cae a estos defaults.
- **`DetectarValor` hace match EXACTO** (case-insensitive) sobre el nombre de campo. Por
  tanto el campo `IDUFIR_CRU` **no** lo detecta ningún alias por defecto → hay que mapearlo
  explícitamente. `ReferenciaCatastral` sí coincide con un alias por defecto.
- **Regla "indicated" del plugin (`ResolverGrupoAsync`):** la autodetección por aliases solo
  ocurre si **ambos** criterios vienen vacíos. Si se indica/mapea **uno**, la búsqueda se
  hace **exclusivamente** por ese y el otro se ignora. Consecuencia: una tipología con
  **ambos** campos debe mapear **ambos**; con `enabled=true` a secas, IDUFIR nunca se buscaría.
- **Colecciones (`MapeoColeccionActivos`, spec 2026-07-08):** si un campo `type:"array"` de
  objetos declara sub-propiedades de activo, la activity expande cada elemento a un grupo y
  el plugin resuelve las sub-propiedades con los mismos aliases `Mapeo*`.
- **Estado actual de los seeds:** todos los campos refcat/IDUFIR están en **plano**; ninguno
  anidado en colecciones. `nota.simple.1_3` usa regla `regex` (no `catastral`) para
  `ReferenciaCatastral` → la detección debe ser **por nombre además de por regla**.

## Diseño

### 1. Heurística de descubrimiento

Escaneo en **dos niveles** de `ConfiguracionJson`, vía `OPENJSON`:

1. **Plano:** `$.fields[*]`.
2. **Colección:** `$.fields[*].items.properties[*]` cuando el campo padre es `type = "array"`.

Un campo (plano o sub-propiedad) **cualifica** como:

- **Refcat** si `name` ∈ {`ReferenciaCatastral`, `RefCatastral`, `Catastral`} (case-insensitive)
  **o** tiene una regla con `ruleType = 'catastral'`.
  - Exacto a propósito: **no** debe matchear `ValoracionCatastral`, `ValorCatastralSuelo`,
    `ValorCatastralConstruccion`, `ValorCatastralTotal` (son importes, no referencias).
- **IDUFIR** si `name` ∈ {`IDUFIR_CRU`, `IDUFIR`, `CRU`, `CodigoRegistroUnico`} (case-insensitive).

Una tipología es **objetivo** si tiene ≥1 campo refcat **o** ≥1 campo IDUFIR (en cualquiera
de los dos niveles), y cumple `Estado = 1 AND Activa = 1`.

### 2. Cálculo del mapeo por tipología (semántica de unión)

Para cada tipología objetivo, a partir de los nombres **reales** descubiertos:

- `mapeoReferenciaCatastral` = `DISTINCT`(nombres de campo refcat planos ∪ nombres de
  sub-propiedad refcat anidadas). Se omite si no hay ninguno.
- `mapeoIdufir` = `DISTINCT`(nombres IDUFIR planos ∪ nombres de sub-propiedad IDUFIR anidadas).
  Se omite si no hay ninguno.
- `mapeoColeccionActivos` = `DISTINCT`(nombres de campos `array` que contienen alguna
  sub-propiedad cualificante). Se omite si no hay colecciones con activos.

### 3. Bloque `assetResolver` resultante

```jsonc
"assetResolver": {
  "enabled": true,
  "mapeoReferenciaCatastral": ["<nombres reales>"],   // solo si hay refcat
  "mapeoIdufir": ["<nombres reales>"],                // solo si hay IDUFIR
  "mapeoColeccionActivos": ["<nombres de arrays>"],   // solo si hay colección
  "busquedaReferenciaCatastralHabilitada": true,
  "busquedaIdufirHabilitada": true
}
```

Ejemplos con los datos actuales:

- `nota.simple.*` → `enabled:true`, `mapeoReferenciaCatastral:["ReferenciaCatastral"]`,
  `mapeoIdufir:["IDUFIR_CRU"]` (ambos, por la regla "indicated").
- `IBI`, `IBI_1.1`, `tasacion` → `enabled:true`, `mapeoReferenciaCatastral:["ReferenciaCatastral"]`
  (sin `mapeoIdufir`, no declaran IDUFIR).

### 4. Idempotencia y preservación

- Operación por fila con `JSON_MODIFY`:
  - `$.assetResolver.enabled = true`.
  - `$.assetResolver.mapeoReferenciaCatastral` / `mapeoIdufir` / `mapeoColeccionActivos`
    ← se **fijan** (overwrite) a los arrays descubiertos (vía `JSON_QUERY` para insertar
    como array JSON, no string).
  - `busquedaReferenciaCatastralHabilitada` / `busquedaIdufirHabilitada = true`.
- Si ya existe un `assetResolver` (p.ej. con dirección configurada), **se preservan** las
  demás claves; solo se fuerzan las anteriores. Overwrite de los `mapeo*` con los nombres
  del esquema es **determinista**: reejecutar el script no produce cambios.
- Todo dentro de una **transacción**, con:
  - `SELECT` previo (dry-run) listando `Id, Codigo, Version, Estado` y el `assetResolver`
    resultante para revisión.
  - Parámetro `@WhatIf` (default 1) que hace `ROLLBACK`; con `@WhatIf = 0` se hace `COMMIT`.

### 5. Entrega y operación

- Fichero: `scripts/database/enable-assetresolver-by-schema.sql`.
- Sin credenciales embebidas: la conexión la aporta quien ejecuta (sqlcmd / SSMS / pipeline)
  contra la BBDD DocumentIA del entorno correspondiente.
- Ejecución **por entorno** en orden dev → pre → prod, revisando el dry-run en cada uno
  antes de `COMMIT`.
- No toca `PluginTipologiaConfigs` ni código.

## Manejo de bordes

- **Sin campos cualificantes** → la fila no se selecciona; no se modifica.
- **Solo refcat** (IBI, tasacion) → se mapea solo refcat; el plugin busca solo por refcat.
- **Solo IDUFIR** → se mapea solo IDUFIR.
- **Ambos** → se mapean ambos (evita que la regla "indicated" descarte uno).
- **Anidado en colección** → se puebla `mapeoColeccionActivos` + el `mapeo*` con la
  sub-propiedad.
- **JSON inválido / `ConfiguracionJson` nulo** → la fila se ignora (no se puede hacer
  `OPENJSON`); se reporta en el dry-run como no procesable.
- **Mezcla plano + colección en la misma tipología:** el script puebla ambos mapeos y
  `mapeoColeccionActivos`; el comportamiento en runtime (colección presente ⇒ modo
  multi-grupo, `ExtractedData` plano ignorado) es el existente del plugin y **no** se altera.

## Fuera de alcance

- Gating en runtime (no invocar `ObtenerActivo` cuando no hay valor extraído) — descartado
  en la decisión de interpretación.
- Búsqueda por dirección (`busquedaDireccionHabilitada`) — se deja como esté por tipología.
- Tipologías en `Draft`/`Retired` o `Activa = 0`.
- Cambios en `PluginTipologiaConfigs`, orquestador, plugin o seeds del repo.
- Cambios en la mecánica de resolución multi-grupo del plugin.

## Testing / validación

- **Dry-run** del script en cada entorno: revisar el listado de tipologías afectadas y el
  `assetResolver` resultante antes de `COMMIT`.
- **Idempotencia:** segunda ejecución no reporta cambios (mismo JSON resultante).
- **Verificación funcional post-cambio:** ingesta de un documento por tipología afectada y
  comprobación de que `DetalleEjecucion.AssetResolver.Ejecutado = true` y que
  `CriteriosUsados` refleja refcat/IDUFIR (según lo declarado).
- **No regresión:** tipologías sin refcat/IDUFIR (`resumen.documental`, `tdn.clasificacion`,
  `nota.simple.1_0`) no aparecen en el dry-run.
