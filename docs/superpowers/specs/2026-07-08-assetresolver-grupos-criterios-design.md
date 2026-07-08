# AssetResolver — Grupos de criterios (multi-activo por colección extraída)

**Fecha:** 2026-07-08
**Estado:** Aprobado (pendiente de plan de implementación)

## Problema

Hay tipologías cuyos datos extraídos contienen una **colección de objetos** donde cada
elemento representa un activo distinto (p. ej. `DireccionPropiedades`: array de objetos
con `ReferenciaCastatral`, `Direccion`, etc.). El flujo actual solo resuelve **un** juego
de criterios por documento:

- `ObtenerActivoActivity.FlattenToString` aplana los arrays de `DatosExtraidos`
  quedándose **solo con el primer elemento**.
- `ExtractedData` del contrato del plugin es un `Dictionary<string, string?>` plano.
- `AssetResolverService.BuscarActivosAsync` detecta un IDUFIR / una referencia
  catastral / una dirección y devuelve una lista fusionada de activos.

Objetivo: dado un array de N elementos, devolver los activos referenciados **por cada
elemento**, manteniendo la asociación elemento→activos, con el menor impacto posible y
sin romper el caso actual de campos planos.

## Decisiones de diseño (acordadas)

| Decisión | Valor |
| --- | --- |
| Semántica de resolución | Un activo (o conjunto) **por elemento del array**, resultado agrupado por elemento |
| Combinación intra-grupo | Se reutiliza `ModoCombinacionCriterios` (AND/OR) existente; sin regla nueva |
| Forma de salida | **Aditiva**: nuevo campo agrupado + se mantienen las listas planas actuales |
| Dedup entre grupos | **No**: si un activo responde a dos grupos, aparece en ambos grupos y dos veces en la lista plana. La dedup interna de cada grupo se mantiene como hoy |
| Caso plano (sin array) | Debe seguir funcionando idéntico a hoy (grupo único implícito) |
| Dónde se expande el array | En el backend (activity), que es donde el dato está tipado. El plugin recibe grupos ya planos |
| Config nueva de tipología | Solo `MapeoColeccionActivos`; los `Mapeo*` existentes se reutilizan para resolver sub-propiedades dentro de cada elemento |

## Modelo conceptual

La unidad de búsqueda pasa de *un juego de criterios* a *una lista de grupos de
criterios*. Un **grupo** es estructuralmente lo mismo que hoy es `ExtractedData`: un
`Dictionary<string, string?>` campo→valor sobre el que ya operan `DetectarValor`,
`ResolverDireccion` y `ResolverDireccionTipificada`.

- **Caso plano (actual):** 1 grupo implícito = `ExtractedData`. Comportamiento idéntico.
- **Caso colección:** N grupos, uno por elemento del array declarado en
  `MapeoColeccionActivos`.

## Cambios por componente

### 1. Contrato del plugin (`AssetResolverController`) — aditivo

`GetAAIIInfoRequest`:

```csharp
/// <summary>
/// Grupos de criterios de búsqueda (uno por activo potencial). Cada grupo es un
/// diccionario campo→valor que se resuelve con los mismos aliases Mapeo* que
/// ExtractedData. Si es null o vacío, se usa ExtractedData como grupo único
/// (comportamiento actual).
/// </summary>
public List<Dictionary<string, string?>>? Grupos { get; set; }
```

`GetAAIIInfoResponse` (se mantienen todos los campos actuales):

```csharp
/// <summary>Resultados agrupados por grupo de criterios de entrada.</summary>
public List<GrupoResultado> ActivosPorGrupo { get; set; } = [];

public class GrupoResultado
{
    /// <summary>Índice del grupo en la petición (0-based).</summary>
    public int Indice { get; set; }
    /// <summary>Criterios de entrada del grupo (eco para trazabilidad).</summary>
    public Dictionary<string, string?> CriteriosEntrada { get; set; } = new();
    /// <summary>Criterios efectivamente resueltos/usados para este grupo.</summary>
    public AssetResolverService.CriteriosUsados? CriteriosUsados { get; set; }
    public List<AssetResolverService.ActivoEncontrado> ActivosAAII { get; set; } = [];
    public List<AssetResolverService.ActivoEncontrado> ActivosAACC { get; set; } = [];
    public int Count { get; set; }
    public string? CriterioUtilizado { get; set; }
    public string? Mensaje { get; set; }
}
```

Semántica de los campos existentes en modo multi-grupo:

- `Activos` / `ActivosAAII` / `ActivosAACC`: concatenación de los resultados de todos
  los grupos **sin dedup entre grupos** (cada grupo deduplicado internamente como hoy).
- `Count*` / `Found`: sobre las listas planas concatenadas.
- `CriteriosUsados` (top-level): se rellena solo en modo grupo único (compatibilidad);
  en modo multi-grupo va a `null` y el detalle vive en `ActivosPorGrupo[i].CriteriosUsados`.
- `CriterioUtilizado` (top-level): en multi-grupo, resumen agregado
  (p. ej. `"Grupo0:{...} | Grupo1:{...}"`).
- `ActivosPorGrupo` se rellena **siempre** (en modo plano contiene 1 grupo), para que
  los consumidores nuevos tengan una única forma de leer el detalle.

### 2. Plugin (`AssetResolverService`) — refactor mínimo

Extraer el cuerpo actual de `BuscarActivosAsync` (detección de criterios → consultas
AAII/AACC → combinación AND/OR → dedup) a un método privado:

```csharp
private Task<ResultadoGrupo> ResolverGrupoAsync(
    Dictionary<string, string?> grupo, GetAAIIInfoRequest request, CancellationToken ct)
```

El nuevo `BuscarActivosAsync`:

1. `grupos = request.Grupos is { Count: > 0 } ? request.Grupos : [request.ExtractedData]`
2. Ejecuta `ResolverGrupoAsync` secuencialmente por grupo → llena `ActivosPorGrupo`.
3. Concatena los resultados de todos los grupos en las listas planas (sin dedup
   entre grupos) y calcula `Count*`, `Found`, `Message`.

Notas:

- Los overrides globales (`IdufirOverride`, `ReferenciaCatastralOverride`) y
  `DireccionTipificada` solo aplican al **modo grupo único**; en multi-grupo cada
  grupo se resuelve exclusivamente por aliases `Mapeo*` sobre sus propias claves.
  Razón: un override único no puede identificar N activos distintos.
- Los flags `Busqueda*Habilitada`, `AAII_Search`/`AACC_Search`,
  `UmbralScoreDireccion` y `RequestedFields` aplican igual a todos los grupos.
- La maquinaria de búsqueda (consultas EF, scoring de dirección, `CombinarResultados`,
  `DeduplicarPorActivo*`) **no se modifica**.
- Si un grupo no aporta ningún criterio resoluble, su `GrupoResultado` queda con
  `Count = 0` y `Mensaje` explicativo; no aborta el resto de grupos.

### 3. Config de tipología (`TipologiaAssetResolverConfig`) — una adición

```csharp
/// <summary>
/// Nombres de campos de DatosExtraidos que son colecciones (array de objetos) donde
/// cada elemento representa un activo. Las sub-propiedades de cada elemento se
/// resuelven con los mismos aliases Mapeo* existentes (MapeoReferenciaCatastral,
/// MapeoDireccion*, MapeoIdufir).
/// </summary>
public List<string> MapeoColeccionActivos { get; set; } = new();
```

Propagación por la cadena existente: `TipologiaAssetResolverConfig` →
`TipologiaVersionResolver` → `ResolvedTipologia` → `BuildObtenerActivoInput` →
`ObtenerActivoInput` → payload del plugin.

### 4. Backend — expansión del array en `ObtenerActivoActivity`

Al construir el payload:

1. Buscar en `DatosExtraidos` (case-insensitive) el primer campo listado en
   `MapeoColeccionActivos` cuyo valor sea un **array JSON de objetos** no vacío.
2. Si existe: por cada elemento del array, emitir un `Dictionary<string, string?>`
   (sub-propiedad → valor plano vía `FlattenToString` por propiedad) → `Grupos`.
   El campo colección se excluye del `ExtractedData` plano que se envía (para no
   mandar el array serializado como ruido).
3. Si no existe o no es array de objetos: **no** se envía `Grupos` → el plugin opera
   en modo grupo único (comportamiento actual, sin regresión).

`FlattenToString` no cambia su comportamiento para el resto de campos.

### 5. Consumidores backend (`ResultadoAssetResolver`)

`ResultadoAssetResolver` gana un campo opcional `ActivosPorGrupo` (mismo shape que el
del plugin) que la activity mapea si viene informado. `Activos`, `CriteriosUsados`,
`Count`, etc. se mantienen y siguen llenándose desde las listas planas, por lo que
persistencia, Admin/Monitor y Desktop no requieren cambios (pueden adoptarse después).

## Flujo de datos (caso colección)

```
DatosExtraidos["DireccionPropiedades"] = [ {RefCat: A, Direccion: X}, {RefCat: B, Direccion: Y} ]
        │  (activity: MapeoColeccionActivos → expansión)
        ▼
Grupos = [ {ReferenciaCastatral:"A", Direccion:"X", ...}, {ReferenciaCastatral:"B", Direccion:"Y", ...} ]
        │  (plugin: ResolverGrupoAsync por grupo, aliases Mapeo* + ModoCombinacionCriterios)
        ▼
ActivosPorGrupo = [ {Indice:0, Activos:[act1]}, {Indice:1, Activos:[act2, act3]} ]
Activos (plana)  = [ act1, act2, act3 ]   ← sin dedup entre grupos
```

## Manejo de errores

- Grupo sin criterios resolubles → `GrupoResultado` vacío con `Mensaje`; el resto de
  grupos se procesa igual.
- Error de BD/excepción durante un grupo → el controller ya captura excepciones
  globalmente y devuelve `Found=false` con `Error`; no se cambia esa política.
- Array declarado en `MapeoColeccionActivos` pero con elementos que no son objetos →
  se ignoran los elementos no-objeto (log de warning en la activity).

## Testing

**Plugin (`AssetResolver.Tests`):**

- `Grupos` con 2 elementos con refcat distintas → 2 entradas en `ActivosPorGrupo`,
  lista plana concatenada sin dedup entre grupos.
- Mismo activo respondiendo a 2 grupos → aparece en ambos grupos y 2 veces en la plana.
- `Grupos = null` → respuesta byte-a-byte equivalente a la actual (no regresión) +
  `ActivosPorGrupo` con 1 grupo.
- Grupo sin criterios → grupo vacío con mensaje, otros grupos intactos.
- Combinación AND/OR intra-grupo reutilizando `ModoCombinacionCriterios`.
- Overrides globales ignorados en modo multi-grupo.

**Backend (`Tests.Unit`):**

- Activity expande array de objetos a N `Grupos` (con `FlattenToString` por sub-propiedad).
- Campo colección ausente / vacío / no-array → no se envía `Grupos`.
- `BuildObtenerActivoInput` propaga `MapeoColeccionActivos` desde la tipología.
- Mapeo de `ActivosPorGrupo` de la respuesta del plugin a `ResultadoAssetResolver`.

## Fuera de alcance

- Cambios en Desktop/Admin para visualizar el agrupado (adoptan la lista plana actual).
- Dedup o ranking cross-grupo.
- Overrides por grupo desde Instrucciones.
- Paralelización de grupos en el plugin (secuencial; N esperado pequeño).
