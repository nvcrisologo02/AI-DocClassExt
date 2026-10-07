# OutOfMemory con PDF grandes: el documento deja de viajar en base64 (AB#100814)

- **Work item**: Bug AB#100814 `OutOfMemory en el orquestador de PRO con PDF grandes en lotes de Batch (base64 en memoria y en mensajes de Durable)`. Relacionados: AB#100815 (alertas de memoria y OOM, ya aplicadas), AB#100868 (cron de limpieza de blobs, no se toca), AB#100880 (SIN_CONTENIDO con causa real, integrado en el candidato)
- **Fecha**: 2026-10-07
- **Rama**: `bugfix/100814-oom-pdf-grandes` desde `develop` (7aca20b), pendiente de crear
- **Estado**: diseño aprobado en una pasada por el usuario, pendiente de plan de implementación
- **Release**: entra en v1.0.0 (decisión del 2026-10-07); al integrarse reabre el paso 0.1 del registro `docs/releases/v1.0.0/release.md`
- **Medición previa**: `docs/auxiliares/temps/2026-10-07/ab100814-medicion/informe-medicion.md` (gitignored; se resume abajo)

## Problema

El 30/09/2026 PRO (EP1, 3,5 GB por instancia) lanzó dos `System.OutOfMemoryException` durante
un lote de DocumentIA.Batch ClassificationLite con unos 20 PDF de 15-60 MB. La pila apunta a
la serialización de una llamada a activity desde el orquestador. El 2026-10-07 se reprodujo en
DEV con **un solo PDF de 57 MB** (20 páginas escaneadas) y la instancia en reposo:

- El trigger HTTP lee el cuerpo entero como string (76 MB de JSON, 152 MB en UTF-16), lo
  deserializa a otro string base64, lo decodifica a bytes y sube al blob: 8,4 s y varios
  cientos de MB antes de arrancar la orquestación.
- `PrepararDocumentoClasificacionActivity` descarga los bytes del blob, los codifica a base64
  para `PdfRecorteService`, que los decodifica de nuevo y abre el PDF dos veces con PdfPig.
- El recorte (10 páginas, ~25 MB de imágenes, ~34 MB en base64) sale como salida de la activity,
  el orquestador lo mete en la entrada de `ClasificarActivity`, y Durable lo guarda en
  `documentiahub-largemessages`. Entre Preparar y Clasificar el orquestador se ejecutó 8 veces
  (replays), cargando ese payload en el host y entregándolo al worker en cada vuelta.
- El worker murió en `ActivityInputConverter.ConvertAsync` al deserializar `ClasificacionInput`.
  Working set de la instancia: 547 MiB → 2.546 MiB en dos minutos; Private Bytes de host y
  worker sumaban ~4,7 GiB. La instancia dejó de reportar métricas (reciclada).
- Layout va por SAS en PRO; en DEV y PRE va inline y materializa el documento en base64 (ver §3).

Microsoft documenta esta situación y su remedio para el proveedor Azure Storage: guardar los
payloads grandes fuera y pasar referencias ligeras entre operaciones, materializando los datos
solo dentro de las activities (patrón claim check). Las sesiones extendidas no existen en el
worker aislado.

## Objetivo

1. Ningún mensaje de Durable de la ruta de clasificación transporta el documento: las
   activities intercambian rutas de blob.
2. La preparación trabaja con bytes y abre el PDF una vez.
3. El trigger JSON no construye el string del cuerpo ni el string base64.
4. Las orquestaciones en vuelo durante el despliegue siguen funcionando.
5. Criterio de aceptación: el escenario de la medición (PDF de 57 MB, `maxPages=10`, JSON
   base64) termina en DEV con `Resultado.Estado` distinto de ERROR, sin OutOfMemory y con el
   working set máximo de la instancia por debajo de 1,5 GiB.

## Fuera de alcance

- Puerta de concurrencia por tamaño de documento (decisión del usuario del 2026-10-07).
- Memoria del proceso host de Functions por el cuerpo HTTP completo: no depende del código.
- Modo `vision` de `OpenAIPromptDataProvider`: lee `PromptInput.DocumentoBase64`, que ya llega
  a null con blob-first. Se anota como hallazgo; no se cambia.
- `ApplyPdfPageLimitActivity` y `PdfPageLimiterService`: base64 → base64, pero nadie los llama
  desde el orquestador. Código muerto, se anota y no se toca.
- Retirada definitiva de los campos `[Obsolete]`: release siguiente a v1.0.0.
- Ruta multipart del trigger: ya trabaja con bytes.

## Estado verificado el 2026-10-07

- `develop` 7aca20b limpio y sincronizado con origin. Candidato provisional de v1.0.0: 5414335.
- DEV: `srbappdevdocai`, plan EP1 `srbspdevdocai` (1 instancia), código igual al candidato.
- `host.json`: `maxConcurrentActivityFunctions=4`, `maxConcurrentOrchestratorFunctions=4`,
  `extendedSessionsEnabled=false`, `traceInputsAndOutputs=false`.
- `IBlobStorageService.UploadDocumentAsync(byte[], nombre, contenedor)` crea el contenedor si
  no existe y devuelve `<contenedor>/yyyy/MM/<sha256>.<ext>`; `DownloadDocumentAsync` y
  `GenerateSasUrlAsync` resuelven el contenedor desde el primer segmento de la ruta.
- `DocumentIntelligenceSourceResolver.ResolveAsync(blobPath, base64Override, base64Entrada)`:
  con `blobPath` genera SAS `urlSource` (inline si `UseInlineContent` o loopback) y el proveedor
  reintenta inline tras un 400 InvalidContent.
- `MarkdownResolver` acepta `ContextoMarkdown.BlobPath` y lo pasa al proveedor de layout.
- Consumidores de `ClasificacionInput.DocumentoBase64Override`:
  `AzureDocumentIntelligenceClasificarProvider` (resolutor), `HybridTdnClasificarProvider`
  (salvavidas, `ContextoMarkdown.DocumentoBase64`), `ConfigurableClasificarDataProvider`
  (propaga). Tests que lo tocan: `DocumentProcessOrchestratorTests`, `ClasificarActivityTests`.
- `PdfRecorteService` tiene tests propios (`PdfRecorteServiceTests`) y la activity también
  (`PrepararDocumentoClasificacionActivityTests`).

## Diseño

### 1. Contrato entre activities

`PrepararDocumentoClasificacionResultado`:

- Nuevo `string? BlobPathClasificacion`: ruta del recorte si hubo recorte; si no, la ruta del
  blob original.
- `DocumentoBase64Clasif` pasa a `[Obsolete]`; el código nuevo lo deja a null.

`ClasificacionInput`:

- Nuevo `string? BlobPathClasificacion`.
- `DocumentoBase64Override` pasa a `[Obsolete]` y solo se lee como fallback cuando
  `BlobPathClasificacion` viene vacío (instancias serializadas por el código anterior).

`PrepararDocumentoClasificacionInput` pierde `DocumentoBase64` (siempre null desde blob-first).
Si no trae `BlobPath`, la activity lanza `InvalidOperationException` y el orquestador aplica el
`catch` que ya tiene (sigue con el documento completo, es decir, con la ruta original).

Orquestador (`DocumentProcessOrchestrator`, pasos 2.5-2.7 y llamada a Clasificar):

- El `docClasif` por defecto lleva `BlobPathClasificacion = blobPath ?? entrada.Documento.BlobPath`.
- `ClasificacionInput.BlobPathClasificacion = docClasif.BlobPathClasificacion`;
  `DocumentoBase64Override` deja de rellenarse.
- Las líneas `DEBUG` que vuelcan `Base64Length` y el prefijo del base64 se eliminan.

Efecto: los mensajes de esta ruta dejan de transportar el documento (el markdown en
`DatosNormalizados` puede seguir superando los 45 KB en documentos grandes); desaparecen los
blobs de `largemessages` con el documento y su descompresión por replay.

### 2. Preparación con bytes

`PdfRecorteService.RecortarParaClasificacion(byte[] pdf, int maxPaginas)` devuelve
`PdfRecorteResultado` con `byte[]? PdfRecortado` (null si no hay recorte) y los mismos
contadores de hoy. Una sola apertura con PdfPig: `NumberOfPages`, texto de las primeras
`maxPaginas + 2` páginas y, si procede, el `PdfDocumentBuilder` sobre el mismo documento abierto.
`EsPdf` se mantiene; para no PDF devuelve `PdfRecortado = null`, `RecorteAplicado = false`.

`PrepararDocumentoClasificacionActivity`:

1. `bytes = DownloadDocumentAsync(input.BlobPath)`.
2. `recorte = RecortarParaClasificacion(bytes, maxPaginas)`.
3. Si `RecorteAplicado`: `BlobPathClasificacion = UploadDocumentAsync(recorte.PdfRecortado,
   nombre, "documents-clasif")`. Si no: `BlobPathClasificacion = input.BlobPath`.
4. Devuelve el resultado sin base64.

Pico de memoria por activity: el PDF una vez más el recorte.

### 3. Clasificación por ruta

- `AzureDocumentIntelligenceClasificarProvider`: `blobPath = input.BlobPathClasificacion ??
  input.Entrada.Documento.BlobPath`; `base64Override` solo con el campo legado.
- `HybridTdnClasificarProvider` (salvavidas): el `ContextoMarkdown` sigue apuntando al documento
  original (`documento.BlobPath`): el layout de N páginas va por SAS con rango, no ahorra memoria
  usar el recorte, y el markdown se persiste por el SHA256 del original (regla de cobertura de
  AB#100245/AB#100253). `DocumentoBase64` solo con el campo legado.
- `ConfigurableClasificarDataProvider`: propaga `BlobPathClasificacion` igual que hoy propaga
  el override.
- La ruta GPT (`GptClasificarDataProvider`, `DocumentWindowExtractor`) no cambia: trabaja con
  markdown y texto normalizado.
- Camino inline de Document Intelligence (DEV y PRE con `UseInlineContent=true`; PRO en el
  reintento tras InvalidContent): el resolutor devuelve los bytes y el cuerpo se escribe con
  `Utf8JsonWriter.WriteBase64String` a un `MemoryStream` dimensionado, enviado como
  `ByteArrayContent`; no se construye ningún string base64 ni JSON. Medido en DEV el
  2026-10-07: sin este cambio el worker llegaba al límite duro del GC (2,6 GiB) en la
  clasificación del recorte.

### 4. Trigger en streaming

En `IngestAPITrigger`, ruta JSON:

- `JsonSerializer.DeserializeAsync<ContratoEntrada>(req.Body, JsonOptionsIngesta)`.
- `JsonOptionsIngesta` añade un `JsonConverter<ContenidoDocumento>` que, al leer la propiedad
  `base64`, intenta `reader.TryGetBytesFromBase64(out var bytes)` y lo deja en una propiedad
  nueva `[JsonIgnore] public byte[]? Bytes` de `ContenidoDocumento`; si devuelve false, cae a
  `reader.GetString()` + `Convert.FromBase64String` (tolera saltos de línea y espacios) y, si
  tampoco vale, el trigger responde el mismo 400 de hoy (`documento.content.base64 no es un
  base64 válido.`). El converter no escribe nunca: `Bytes` no se serializa.
- `UploadToBlobAndSetHashesAsync` recibe `entrada.Documento.Content.Bytes`, calcula hashes y
  sube como hoy; después pone `Bytes = null` y `Base64 = null`.
- La validación de extensión (AB#100180) se mantiene antes de la subida.
- La ruta multipart no cambia.

Pico en el worker: cuerpo recibido más bytes (unos 130 MB para un PDF de 57 MB), frente a unos
440 MB hoy. `Base64` sigue existiendo en el modelo para la serialización de la orquestación y
los fallbacks legados (GDC).

### 5. Infraestructura

- Contenedor `documents-clasif` en la cuenta de documentos de cada entorno (`srbstgdevdocai`,
  `srbstgpredocai`, `srbstgprodocai`): lo crea el código al primer uso.
- Política de ciclo de vida de Storage: borrar blobs del contenedor `documents-clasif` con más
  de 7 días desde su modificación. Script idempotente nuevo
  `scripts/storage/set-lifecycle-documents-clasif.ps1` (plano de gestión, `az rest`, parámetro
  `-Environment`), ejecutado por el usuario por entorno con sí explícito. Añade la regla sin
  pisar otras reglas existentes de la cuenta.
- `scripts/setup/initialize-azurite.ps1` añade el contenedor para el entorno local.
- El cron de AB#100868 no se toca: solo conoce `documents`.

### 6. Compatibilidad y despliegue

- Instancias en vuelo con `DocumentoBase64Clasif` o `DocumentoBase64Override` serializados:
  los campos legados siguen leyéndose, así que terminan con el comportamiento anterior.
- Si la salida de Preparar llega sin ruta pero con `DocumentoBase64Clasif` (historia anterior al
  fix), el orquestador reenvía ese base64 como `DocumentoBase64Override` una sola vez, así la
  instancia termina como antes.
- Los campos `[Obsolete]` se retiran en la release siguiente a v1.0.0 (task aparte).
- No hay migraciones ni cambios de configuración de la Function App.

### 7. Pruebas y criterio de aceptación

Unitarios (TDD, `DocumentIA.Tests.Unit`):

- `PdfRecorteServiceTests`: entrada bytes, salida bytes; no PDF devuelve null sin recorte;
  documento con menos páginas que el máximo devuelve null sin recorte; contadores iguales a hoy.
- `PrepararDocumentoClasificacionActivityTests`: con recorte sube a `documents-clasif` y
  devuelve esa ruta; sin recorte devuelve la ruta original; sin `BlobPath` lanza.
- `DocumentProcessOrchestratorTests`: la entrada de Clasificar lleva `BlobPathClasificacion`
  y `DocumentoBase64Override` nulo; si Preparar falla, la ruta es la original.
- `DocumentIntelligenceSourceResolverTests` y proveedores: la ruta de clasificación manda
  sobre la del documento; el campo legado sigue funcionando.
- `DocumentIntelligenceSourceResolverTests`: los casos inline devuelven `InlineBytes` y
  `CrearContenido()` produce un JSON `base64Source` que decodifica a los bytes del blob
  (`application/json`); `CrearContenido_ConBytes_NoProduceStringIntermedio_YElJsonEsValido`
  fija el JSON exacto `{"base64Source":"AQIDBA=="}` como `ByteArrayContent`; override legado,
  entrada sin blob y urlSource conservan `Body`.
- `AzureDocumentIntelligenceClasificarProviderTests` (reintento tras InvalidContent): el
  segundo cuerpo enviado decodifica a los bytes del blob.
- Trigger: converter con base64 válido (bytes y sin string), base64 con saltos de línea
  (fallback), base64 inválido (400 con el mismo mensaje), petición sin `base64` (blobPath u
  ObjectIdGDC).

Verificación: `dotnet build` sin warnings, `dotnet format --verify-no-changes`, tests Unit y
Admin en verde.

DEV, tras desplegar la rama: `medir-ab100814.ps1` con el PDF de 57 MB y `maxPages=10` →
`Resultado.Estado` distinto de ERROR, sin OutOfMemory, working set máximo < 1,5 GiB; repetir
con `maxPages=25` (sin recorte, viaja la ruta original); smoke 6/6.

## Riesgos

- Un cliente que envíe base64 con saltos de línea pasa por el fallback con string: funciona,
  pero sin ahorro. ClassificationLite, Desktop y el runner E2E usan `Convert.ToBase64String`
  sin saltos.
- Durante el despliegue, una instancia en vuelo puede ejecutar la activity nueva con la salida
  antigua: cubierto por los campos legados.
- Si la política de ciclo de vida no se aplica en un entorno, `documents-clasif` crece sin
  límite (unos 25 MB por PDF grande recortado). Se verifica en el checklist de release.
- El host de Functions sigue cargando el cuerpo HTTP completo; con muchas ingestas grandes
  simultáneas el límite lo marca la puerta de concurrencia, fuera de alcance.

## Pendientes que resuelve el plan

- Nombre exacto de la rama y creación de tasks hijas en ADO bajo AB#100814 (con sí explícito).
- Orden de los commits (contrato y preparación, clasificación por ruta, trigger, scripts).
- Texto del comentario de cierre en AB#100814 con la evidencia de DEV.

## Referencias

- Data persistence and serialization in Durable Functions: "Keep Durable Functions inputs and
  outputs small", patrón claim check para el proveedor Azure Storage.
- Azure Storage provider for Durable Functions: mensajes > 45 KB en `<taskhub>-largemessages`
  y coste de memoria por replay; sesiones extendidas no disponibles en el worker aislado.
- `Utf8JsonReader.TryGetBytesFromBase64`: devuelve false si el token no es base64 válido.
