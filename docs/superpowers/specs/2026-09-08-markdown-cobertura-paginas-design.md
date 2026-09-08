# Política única de obtención y persistencia de markdown por cobertura de páginas

**Fecha:** 2026-09-08
**Estado:** Diseño aprobado, pendiente de plan de implementación
**Work items:** elemento padre [100245](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/100245), tareas 100246 a 100255; se incorpora al mismo esfuerzo el bug [100256](https://sareb.visualstudio.com/AI%20DocClassExt/_workitems/edit/100256) (aserciones de la batería E2E).

## Problema

El markdown de un documento es el texto sobre el que trabajan la clasificación GPT, el resumen, el prompt libre y la extracción GPT. Hoy se obtiene desde **diez puntos distintos** del pipeline, cada uno con su propia lógica: seis en el orquestador (pasos 2.8 y 2.8b, layout de resumen en `ClassificationOnly`, regeneración a documento completo tras clasificar, paso 3.5 previo a extracción, fallback tras extraer y obtención bajo demanda del prompt), dos dentro de actividades y fuera del alcance del orquestador (`HybridTdnClasificarProvider` y `ConfigurableExtraerDataProvider`, que llaman a `ILayoutMarkdownProvider` por su cuenta), y el propio clasificador DI/CU, que devuelve texto en `ContentExtraido`.

Tres consecuencias medidas:

1. **Nadie sabe cuántas páginas cubre el markdown que tiene.** La cobertura es implícita: base64 significa "recorte del paso 2.7", `BlobPath` significa "documento completo". Dos de los diez puntos no pasan `BlobPath` ([L1641](../../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L1641) y [L2012](../../../src/backend/DocumentIA.Functions/Orchestrators/DocumentProcessOrchestrator.cs#L2012)), el mismo patrón que corrigió AB#100045 en el paso 2.8. El respaldo desde BD (AB#99986) puede devolver un markdown de tres páginas y darlo por bueno para extraer el documento entero.
2. **Se paga Document Intelligence Layout de más.** El markdown persistido solo se consulta cuando el layout falla, nunca antes. En un flujo con tipología conocida se llama a layout dos veces: recorte y luego completo. El layout es la partida mayor de una ejecución: 0,0258 € frente a 0,0034 € de clasificación en la ejecución 8025 de DEV.
3. **La fuente de verdad se corrompe sola.** `PersistirActivity` escribe `Documentos.NormalizacionMarkdownGzip` sin condición al cierre de la ejecución ([L153](../../../src/backend/DocumentIA.Functions/Activities/PersistirActivity.cs#L153)) y `Compress(null)` devuelve `null`. Cualquier reejecución que acabe en `SIN_CONTENIDO_DOCUMENTO`, `NO_CLASIFICADO` o `PAGINAS_EXCEDIDAS` **borra** el markdown bueno de la ejecución anterior. En el mismo método, `RutaBlobStorage`, `IdGDC` y `Tdn1` sí están protegidos con "solo si viene informado".

## Objetivo

Una única política, en un único sitio, expresada en los términos del negocio: **documento + número de páginas necesario**. Cada actividad declara lo que necesita; un resolutor decide si sirve lo que ya hay (en la ejecución o en base de datos), si hay que llamar a Layout, y persiste lo obtenido en el momento de obtenerlo.

### Reglas acordadas

1. Si la ejecución ya lleva markdown con cobertura suficiente, se usa.
2. Si en base de datos hay markdown con cobertura mayor o igual a la necesaria, se recupera de ahí. Coste cero.
3. Si la cobertura de base de datos es insuficiente, se extrae con Layout. Si el layout falla, el de base de datos sirve de fallback aunque no cubra. Si el layout va bien y su cobertura es mayor, se persiste.
4. **`ForceReprocess` reinicia el documento.** Ignora base de datos al leer y sobrescribe siempre al escribir, como si fuera la primera vez que se procesa. Consecuencia asumida: un `ForceReprocess` en un flujo que solo necesita tres páginas deja en base de datos un markdown de tres páginas aunque antes hubiera uno completo.
5. El markdown que llega en `Instrucciones.Classification.Markdown` gana durante toda la ejecución y **no se persiste**. Su validez es la de esa petición; el sistema no conoce su cobertura ni su calidad.
6. Prompt libre, resumen dedicado y extracción GPT exigen el **documento completo**. Clasificación y resumen combinado exigen el **mínimo de páginas configurado** (o el indicado en la petición).
7. **Retención indefinida.** El markdown es derivado del binario y el SHA256 no cambia. El tamaño se gobierna con la política general de AB#100165, no aquí.

### El peaje que se asume

Cuando la tipología no se conoce de antemano, la primera petición clasifica sobre el recorte y después, si el flujo sigue, hace falta el documento completo: dos llamadas a Layout. Es inevitable sin saber aún si habrá que clasificar. Solo se optimiza el caso en que la tipología viene dada por `ExpectedType` y ya se sabe que alguna actividad posterior necesitará el completo: entonces se pide completo de una sola vez.

## Alcance

### Dentro

- Modelos de necesidad y resultado de markdown.
- Servicio `MarkdownResolver` con la política completa, y actividad `ObtenerMarkdownActivity` como entrada única desde el orquestador.
- Parámetro `pages` en el proveedor de Layout (PDF y TIFF).
- Dos columnas nuevas en `Documentos` y su backfill del histórico.
- Sustitución de los seis puntos del orquestador y de los dos de dentro de actividades.
- Retirada de la escritura de markdown de `PersistirActivity`.
- Trazabilidad de cobertura y fuente en `DetalleEjecucion`.
- Fortalecimiento de las aserciones de la batería E2E (bug 100256).

### Fuera

- El paso 2.7 (recorte local) y los clasificadores DI/CU. El recorte sigue existiendo: lo consumen esos clasificadores y de ahí sale `CharsTextoNativo`.
- Caducidad o purga del markdown persistido.
- Retirar la columna `NormalizacionMarkdownCompressed` en Base64 (escritura dual de AB#100169; fase aparte).
- Cambios en el contrato de entrada.

## Enfoques evaluados

**A — Resolutor en el orquestador.** Una función local que encadena las actividades existentes. Mínimo cambio de piezas, pero la política queda en un fichero de 2.500 líneas, cuesta tres saltos de actividad por resolución y **no alcanza** a los dos llamadores de layout que viven dentro de actividades.

**B — Servicio `MarkdownResolver` + una actividad.** La política vive en un servicio, lo consume una actividad desde el orquestador **y** se inyecta en `HybridTdnClasificarProvider` y `ConfigurableExtraerDataProvider`. Una política, tres consumidores, testable en unitario sin orquestador.

**C — B, y además "N páginas" se lo pide a Document Intelligence.** Con `pages=1-N` sobre el blob completo, sin depender de un base64 recortado. Elimina la dualidad base64/BlobPath de raíz.

**Elegido: B con C incluido.** B es el corazón; sin el servicio compartido volverían a convivir dos comportamientos. C es un incremento pequeño sobre B (un parámetro en la URL y una rama por formato) que cierra la clase de bug "olvidé pasar BlobPath" y hace que la necesidad sea de verdad "documento + N páginas".

## Necesidades por actividad

| Paso | Actividad | Necesidad | Notas |
| --- | --- | --- | --- |
| 2.8 | Clasificación GPT, Hybrid, Reglas | `Páginas(N)` con N = máximo de páginas de clasificación (`ClassificationPreparationSettings`, override por tipología o familia; `MaxPagesForClassificationOnly` si viene) | Solo cuando `ExpectedType` está vacío tras la validación del paso 2.75 y el provider no es DI/CU |
| 2.8 | Clasificación DI, CU | *ninguna* | Generan su propio texto; ese texto **entra en la caché** con cobertura = `PagesProcessed` |
| 3 | Atajo por `ExpectedType` | *ninguna* | No se clasifica |
| — | Resumen combinado de clasificación | *implícita en la anterior* | Sale de la Fase 1 sobre las mismas N páginas; conserva el sufijo "basado en las primeras N páginas" |
| 3.5 / 4 | Extracción GPT-directo y fallback GPT | `Completo` | La extracción CU y DI a medida **no** declaran necesidad: analizan el binario |
| 4.5 | Prompt libre y resumen dedicado | `Completo` | Incluye el resumen forzado en `ClassificationOnly` (hoy usa el recorte: cambio de comportamiento deliberado, regla 6) |
| — | Salidas tempranas con prompt (NO_CLASIFICADO) | `Completo` | Misma vía que 4.5 |

**Optimización de tipología conocida.** Tras el paso 2.75, si `ExpectedType` sigue informado y la tipología resuelta tiene prompt habilitado, extracción GPT-directo, o la petición trae prompt ad hoc o `ForzarResumenPorDefecto`, la primera necesidad del flujo ya es `Completo`. No hay recorte previo porque no hay clasificación.

## Contrato

Tres tipos nuevos en `DocumentIA.Core.Models`.

```csharp
/// Qué necesita quien pide. Inmutable.
public sealed record NecesidadMarkdown(bool DocumentoCompleto, int PaginasMinimas)
{
    public static NecesidadMarkdown Completo() => new(true, 0);
    public static NecesidadMarkdown Paginas(int n) => new(false, Math.Max(1, n));
}

public enum FuenteMarkdown { Ninguna, Caller, CacheEjecucion, BaseDatos, Layout, Clasificador }

public sealed class ResultadoMarkdown
{
    public string? Markdown { get; set; }
    public int Paginas { get; set; }            // páginas que cubre; 0 si no se sabe
    public bool Completo { get; set; }          // cubre el documento entero
    public FuenteMarkdown Fuente { get; set; }
    public bool Persistido { get; set; }        // se escribió en BD en esta resolución
    public List<ConsumoIA> Consumos { get; set; } = new();
}
```

`ResultadoMarkdown.Cubre(NecesidadMarkdown)` es la única función que decide si un markdown sirve: `Completo`, o bien `!necesidad.DocumentoCompleto && Paginas >= necesidad.PaginasMinimas`.

`ExtraerMarkdownLayoutInput` gana `int? PaginasSolicitadas`. `null` = documento entero. Se conserva `DocumentoBase64` para el caso en que no hay blob (flujo legado); en blob-first el resolutor siempre pasa `BlobPath`.

## `MarkdownResolver`

Servicio en `DocumentIA.Functions.Services`, registrado como **Singleton**. Depende de `ILayoutMarkdownProvider` (Singleton) y de `IServiceScopeFactory` para abrir un scope por operación de base de datos, porque `IDocumentoRepository` es Scoped y dos de sus consumidores (`HybridTdnClasificarProvider`, `ConfigurableClasificarDataProvider`) son Singleton. Es el mismo patrón que `TipologiaVersionResolver`.

### Entrada

```csharp
public sealed class ContextoMarkdown
{
    public string? Sha256 { get; init; }
    public string? Md5 { get; init; }
    public string? BlobPath { get; init; }
    public string? DocumentoBase64 { get; init; }      // solo flujo legado sin blob
    public string NombreDocumento { get; init; }
    public string? Tipologia { get; init; }
    public int TotalPaginas { get; init; }             // 0 si no se conoce (Office)
    public bool ForceReprocess { get; init; }
    public string? MarkdownCaller { get; init; }        // Instrucciones.Classification.Markdown
    public ResultadoMarkdown? CacheEjecucion { get; init; }
}
```

### Orden de resolución

1. **Caller.** Si `MarkdownCaller` tiene contenido: se devuelve con `Fuente=Caller`, `Completo=true` a efectos de cobertura (gana siempre), `Persistido=false`. No se consulta nada más.
2. **Caché de la ejecución.** Si `CacheEjecucion` cubre la necesidad, se devuelve tal cual con `Fuente=CacheEjecucion`.
3. **Base de datos.** Salvo `ForceReprocess`. Se busca por SHA256 y, en su defecto, por MD5. Sirve si `MarkdownCompleto = 1`, o si la necesidad es de páginas y `MarkdownPaginas >= PaginasMinimas`. Cobertura `NULL` **no** sirve para esta regla.
4. **Layout.** Con `PaginasSolicitadas = necesidad.DocumentoCompleto ? null : PaginasMinimas`. Si devuelve markdown, se persiste (regla de escritura) y se devuelve con `Fuente=Layout`.
5. **Fallback a base de datos.** Si el layout lanza o devuelve vacío y en base de datos había algo (con cualquier cobertura, incluida `NULL`), se devuelve con `Fuente=BaseDatos` y la cobertura que tenga. Es el comportamiento actual de AB#99986, ahora explícito.
6. **Nada.** `Markdown=null`, `Fuente=Ninguna`. Las guardas de contenido de las actividades (AB#100180, AB#100027) deciden qué hacer.

Con `ForceReprocess` se salta el paso 3 pero **no** el 5: si el layout falla, el markdown viejo sigue siendo mejor que nada.

### Inferencia de "completo"

Un layout sin `PaginasSolicitadas` es completo por definición. Un layout con `PaginasSolicitadas = N` es completo si `TotalPaginas > 0 && Paginas >= TotalPaginas` (el documento tenía menos páginas que las pedidas). En cualquier otro caso, `Completo=false, Paginas=<devueltas por DI>`.

### Regla de escritura

Método nuevo en `IDocumentoRepository`, `ActualizarMarkdownSiMejoraAsync`, con `ExecuteUpdateAsync` (EF Core 8), sin cargar la entidad. Devuelve el número de filas afectadas (0 o 1), que el resolutor traduce a `Persistido`:

```sql
UPDATE Documentos
SET NormalizacionMarkdownGzip = @gzip,
    NormalizacionMarkdownCompressed = @base64,   -- escritura dual AB#100169
    MarkdownPaginas = @paginas,
    MarkdownCompleto = @completo,
    FechaActualizacion = SYSUTCDATETIME()
WHERE SHA256 = @sha256
  AND (
        @forzar = 1                                              -- ForceReprocess
     OR NormalizacionMarkdownGzip IS NULL                        -- no había nada
     OR (@completo = 1 AND MarkdownCompleto = 0)                 -- pasamos a completo
     OR (@completo = 0 AND MarkdownCompleto = 0
         AND MarkdownPaginas IS NOT NULL AND MarkdownPaginas < @paginas)   -- más páginas
  )
```

Tres propiedades que la regla garantiza:

- **Nunca se escribe `null`** ni se degrada cobertura. Un markdown con cobertura desconocida (`MarkdownPaginas IS NULL` y contenido presente) solo se sustituye por uno completo o por `ForceReprocess`: podría ser el documento entero y un recorte no debe pisarlo.
- **Atómica frente a concurrencia.** Dos ejecuciones del mismo SHA256 (posibles con `ForceReprocess` o `SkipDuplicateCheck`) no se pisan: la condición va en el propio `UPDATE`.
- **Si la fila no existe todavía** (primera ejecución del documento; `PersistirActivity` la crea al final), el `UPDATE` afecta a cero filas y el resolutor lo registra en `Persistido=false`. La persistencia de esa primera vez la hace `PersistirActivity` **en el alta**, con el markdown y la cobertura de la caché de la ejecución (ver más abajo). Así ningún documento se queda sin markdown persistido por ser la primera vez.

### Consumos

Cada llamada a Layout devuelve sus `ConsumoIA` (AB#100229) y el resolutor los propaga en `ResultadoMarkdown.Consumos`. Las resoluciones desde caller, caché o base de datos devuelven la lista vacía.

## Proveedor de Layout: `pages=1-N`

`AzureDocumentIntelligenceLayoutMarkdownProvider` añade `&pages=1-{N}` a la URL de `analyze` cuando `PaginasSolicitadas` viene informado **y** el documento es PDF o TIFF (por extensión del nombre). Para el resto de formatos (DOCX, XLSX, PPTX, HTML, imágenes) se ignora el rango y se procesa el documento entero: la documentación de Document Intelligence restringe `pages` a PDF y TIFF multipágina, y las "páginas" de Office son unidades sintéticas (3.000 caracteres, hoja, diapositiva).

La fuente sigue siendo la que resuelve `DocumentIntelligenceSourceResolver`: `BlobPath` (urlSource con SAS) con preferencia sobre base64. Se mantiene el reintento inline ante `InvalidContent` (AB#99856).

**Verificación en DEV antes de dar la tarea por cerrada:** el markdown de `pages=1-3` sobre el blob completo debe coincidir en contenido con el del PDF recortado localmente por `PdfRecorteService` sobre el mismo documento. Es el mismo motor; si difiere, hay que entender por qué antes de seguir.

## Esquema y backfill

### Migración

Migración EF en `DocumentIA.Data`, patrón de `20260902103313_MarkdownBinario`:

| Columna | Tipo | Significado |
| --- | --- | --- |
| `Documentos.MarkdownPaginas` | `int NULL` | Páginas que cubre el markdown almacenado. `NULL` = cobertura desconocida |
| `Documentos.MarkdownCompleto` | `bit NOT NULL DEFAULT 0` | El markdown corresponde al documento entero |

`Up` añade las columnas; `Down` las quita. Sin `UpdateData`. El histórico nace con `NULL / 0`: solo válido como fallback.

Sin índice nuevo: la búsqueda sigue siendo por `SHA256`, que ya lo tiene.

### Backfill del caso seguro

Script `scripts/database/backfill-markdown-cobertura.ps1`, patrón de `backfill-idactivo.ps1` (AB#100168): por lotes, fuera de la migración, idempotente y reanudable, parámetros `Server / Database / BatchSize / MaxBatches`.

Marca `MarkdownCompleto = 1` únicamente cuando la **última** ejecución del documento tiene `OrigenMarkdown` en `{LayoutDocumentoCompletoPostClasificacion, FallbackLayout, LayoutBajoDemandaPrompt, Extraccion, MarkdownPrevio}` y `EstadoFinal = 'OK'`. `MarkdownPrevio` entra porque en el flujo completo es el nombre que recibe el markdown regenerado cuando llega a la extracción (se comprobó en la ejecución 8059). El resto queda en `NULL`. No se infieren páginas de recortes.

Solo toca filas con `MarkdownPaginas IS NULL AND MarkdownCompleto = 0 AND NormalizacionMarkdownGzip IS NOT NULL`, por lo que se puede relanzar sin duplicar trabajo y no interfiere con filas ya escritas por el código nuevo.

## Orquestador

### Caché de la ejecución

Una variable local `ResultadoMarkdown? markdownEjecucion` sustituye al uso de `datosNormalizados["Markdown"]` como único portador de estado. `datosNormalizados["Markdown"]` se sigue rellenando (las actividades lo leen), pero la cobertura vive en `markdownEjecucion`. Es replay-safe: se deriva de resultados de actividad.

Una función local:

```csharp
async Task<ResultadoMarkdown> AsegurarMarkdownAsync(NecesidadMarkdown necesidad, string origenTraza)
```

que (a) devuelve la caché si cubre, (b) si no, llama a `ObtenerMarkdownActivity` con el `ContextoMarkdown` de la ejecución, (c) actualiza caché y `datosNormalizados["Markdown"]`, (d) acumula consumos, (e) registra trazabilidad con `RegistrarMarkdown(markdown, origenTraza, resultado)`.

### Qué cambia en cada punto

| Hoy | Después |
| --- | --- |
| Paso 2.8 (L890–L940) + 2.8b (L944–L973) | `AsegurarMarkdownAsync(Paginas(maxPaginasClasificacion), "LayoutPreClasificacion")` bajo la misma guarda (sin `ExpectedType`, provider no DI/CU). El 2.8b desaparece: la base de datos ya se consultó primero |
| Propagación de `ContentExtraido` (L1161) | Igual, pero además alimenta `markdownEjecucion` con `Fuente=Clasificador`, `Paginas=PagesProcessed` (o `PaginasIncluidas`), y **se persiste** con la regla de escritura vía una llamada ligera a la actividad |
| Layout de resumen en `ClassificationOnly` (L1630–L1668) | `AsegurarMarkdownAsync(Completo(), "LayoutResumenClassificationOnly")`. Cambio de comportamiento deliberado (regla 6) |
| Regeneración a documento completo (L1919–L1966) | **Desaparece.** Quien necesite completo lo declara |
| Paso 3.5 (L2004–L2041) | `AsegurarMarkdownAsync(Completo(), "LayoutPrevioExtraccion")` bajo la misma guarda (provider GPT-directo) |
| Fallback tras extraer (L2113–L2164) | `AsegurarMarkdownAsync(Completo(), "FallbackLayout")` cuando la extracción no dejó markdown y el flujo necesita prompt; si no lo necesita, no se pide |
| Obtención bajo demanda del prompt (L365–L414) | `AsegurarMarkdownAsync(Completo(), "LayoutBajoDemandaPrompt")` |
| `layoutDocumentoCompletoIntentado` | **Desaparece.** La caché sabe si ya se intentó completo |
| `RecuperarMarkdownPersistidoActivity` | **Se retira** del orquestador y del proyecto |

Los nombres de `OrigenMarkdown` se conservan para no romper el monitor ni el backfill; ahora indican el **punto del flujo** que pidió el markdown, y la fuente real va en el campo nuevo.

### Optimización de tipología conocida

Justo después del paso 2.75, si `ExpectedType` sigue informado, el orquestador resuelve la tipología (ya lo hace) y calcula si el flujo necesitará completo. Si sí, llama a `AsegurarMarkdownAsync(Completo(), "LayoutDocumentoCompletoAnticipado")` antes de nada. El resto de puntos encontrarán la caché cubierta.

### `PersistirActivity`

Escribe el markdown **solo en el alta** de la fila de `Documentos` (rama `documento == null`), tomando markdown, `MarkdownPaginas` y `MarkdownCompleto` de `DetalleEjecucion` — es decir, de la caché de la ejecución. En la rama de **actualización** no toca ninguna de las cuatro columnas de markdown: esa escritura ya la hizo el resolutor con la regla de cobertura. Esto corrige el borrado con `null` como efecto directo y cubre la primera ejecución de cada documento, cuando el resolutor aún no tenía fila sobre la que escribir.

## Consumidores dentro de actividades

- **`HybridTdnClasificarProvider.EnsureMarkdownContextAsync`** recibe `MarkdownResolver` en lugar de `ILayoutMarkdownProvider` y pide `Paginas(_options.PagesToInspect)`. Deja de exigir base64: pasa `BlobPath` en el contexto. Es un salvavidas: con el orquestador resolviendo antes de `ClasificarActivity`, en la práctica no se ejecuta.
- **`ConfigurableExtraerDataProvider`** recibe `MarkdownResolver` y pide `Completo()` cuando CU no dejó markdown y hace falta contexto para el fallback GPT. Se mantiene la semántica de "no es un descarte" para sus consumos.
- Ambos devuelven el markdown obtenido a través de sus resultados (`ContentExtraido`, `MarkdownExtraido`), como hoy, para que el orquestador alimente la caché.
- **Criterio de cierre:** ningún código llama a `ILayoutMarkdownProvider` salvo el resolutor y `ExtraerMarkdownLayoutActivity` (que se conserva por compatibilidad de tests hasta la última tarea y se retira entonces).

## Trazabilidad

`DetalleEjecucion` gana tres campos junto a `MarkdownGenerado` y `OrigenMarkdown`:

| Campo | Tipo | Contenido |
| --- | --- | --- |
| `MarkdownPaginas` | `int` | Cobertura final del markdown de la ejecución |
| `MarkdownCompleto` | `bool` | Cubre el documento entero |
| `MarkdownFuente` | `string` | `Caller`, `CacheEjecucion`, `BaseDatos`, `Layout`, `Clasificador` |

`RegistrarMarkdown` pasa a recibir el `ResultadoMarkdown` y a rellenar los cinco. Con `MarkdownFuente = BaseDatos` y `Consumos` vacíos el monitor puede contar layouts evitados sin más instrumentación.

## Errores y degradación

| Situación | Comportamiento |
| --- | --- |
| Layout lanza o devuelve vacío | Fallback a base de datos si hay algo (cualquier cobertura); si no, `Fuente=Ninguna` y deciden las guardas actuales |
| Base de datos no disponible al leer | Se trata como "no hay nada persistido" y se va a Layout; se registra warning |
| Base de datos no disponible al escribir | El markdown se usa en la ejecución igualmente; `Persistido=false`; warning. Nunca tumba la ejecución |
| Documento sin `BlobPath` ni base64 | `Fuente=Ninguna` sin llamar a Layout |
| `pages` en formato no soportado | Se pide el documento entero; `Completo=true` |

Las guardas de contenido existentes no cambian: `GptClasificarDataProvider` sigue devolviendo `SinContenido` y `OpenAIPromptDataProvider` sigue abortando sin markdown ni base64.

## Concurrencia

Dos orquestaciones del mismo SHA256 pueden ejecutarse a la vez (`ForceReprocess`, `SkipDuplicateCheck`, dedup no reutilizable). La regla de escritura en el `UPDATE` hace que el resultado final sea el de mayor cobertura, independientemente del orden. No hay bloqueos ni transacciones adicionales.

## Tests

| Nivel | Qué | Dónde |
| --- | --- | --- |
| Unitario | Las seis vías de resolución; `Cubre`; inferencia de completo; `ForceReprocess` salta lectura pero conserva fallback; caller gana y no persiste | `MarkdownResolverTests` (nuevo), con `ILayoutMarkdownProvider` y repositorio falsos |
| Unitario | Regla de escritura: cada rama del `WHERE`, incluido "cobertura desconocida no se degrada" y "fila inexistente devuelve 0" | `DocumentoRepositoryMarkdownTests` sobre **SQLite en memoria**. El resto de tests de repositorio usan el proveedor InMemory, pero ese proveedor no soporta `ExecuteUpdateAsync` (lanza). Se añade `Microsoft.EntityFrameworkCore.Sqlite` al proyecto de tests si no está referenciado; solo lo usan estos tests |
| Unitario | URL con y sin `pages`; PDF/TIFF frente a Office | `AzureDocumentIntelligenceLayoutMarkdownProviderTests` |
| Unitario | Orquestador: los 27 asserts que cuentan `ExtraerMarkdownLayoutActivity` pasan a contar `ObtenerMarkdownActivity`; el `FakeTaskOrchestrationContext` devuelve un `ResultadoMarkdown` por defecto | `DocumentProcessOrchestratorTests` |
| Unitario | Regresión del borrado: ejecución OK seguida de `SIN_CONTENIDO_DOCUMENTO` sobre el mismo SHA256; el markdown sobrevive | `PersistirActivityTests` |
| Unitario | Optimización de tipología conocida: `ExpectedType` con prompt habilitado hace **una** llamada a `ObtenerMarkdownActivity` con `Completo` | `DocumentProcessOrchestratorTests` |
| E2E (bug 100256) | `S-S4` con `expectedStatus: ["OK"]`, `expectOutputPathEquals` sobre `DetalleEjecucion.Clasificacion.Modelo = expectedtype-input` e `Identificacion.Tipologia = resumen.documental`, `expectOutputPathsNotEmpty: ["DatosExtraidos.ResultadoPrompt"]`. Revisión del resto de casos que solo usan `expectNoTechnicalError` | `smoke-cases.json`, `full-cases.json` |
| E2E | Caso nuevo en `full`: mismo documento dos veces con `SkipDuplicateCheck` y sin `ForceReprocess`; la segunda debe tener `MarkdownFuente = BaseDatos` y cero consumos de layout | `full-cases.json` |

## Despliegue

1. Migración en DEV por el pipeline (Generate) y aplicación manual; en PRO manual, como hasta ahora (Apply sigue bloqueado por Deny Public Network Access).
2. Despliegue del código. **Drenar orquestaciones en vuelo** antes: se reordenan y retiran llamadas a actividades (misma precaución que AB#100176 y AB#100217).
3. Backfill por lotes, primero DEV, con recuento de filas marcadas y no marcadas.
4. Verificación: repetir la ejecución 8060 (`resumen.documental`, 2 páginas) y comprobar `MarkdownFuente = Layout` la primera vez y `BaseDatos` la segunda; smoke E2E con el `S-S4` fortalecido.
5. Observar durante una semana el ratio `MarkdownFuente = BaseDatos` frente a `Layout` en DEV antes de PRO.

## Riesgos

| Riesgo | Mitigación |
| --- | --- |
| El markdown de `pages=1-N` difiere del recorte local | Verificación explícita en DEV en la tarea 100249, antes de cambiar el orquestador |
| Prompt y extracción con documento completo suben el coste en flujos que hoy usan el recorte (`ClassificationOnly` con resumen forzado) | Es la regla 6, decidida a propósito; se mide en el monitor con `MarkdownPaginas` |
| Crecimiento de `Documentos` por persistir más completos | Retención indefinida decidida; se gobierna con AB#100165 |
| Dos escritores del markdown (resolutor en la actualización, `PersistirActivity` en el alta) | Responsabilidades disjuntas por rama; test de regresión del borrado cubre ambas |
| 27 asserts de tests que cambian a la vez | Tarea propia (100252); el `FakeTaskOrchestrationContext` devuelve un resultado por defecto para que los tests que no montan la actividad sigan pasando |

## Orden de implementación

Coincide con las tareas 100246 a 100255 y añade el bug 100256 al final. Cada tarea deja la suite en verde; el orquestador (100252) es la única que cambia comportamiento visible y va después de que el resolutor, la actividad y el proveedor estén cubiertos por tests.
