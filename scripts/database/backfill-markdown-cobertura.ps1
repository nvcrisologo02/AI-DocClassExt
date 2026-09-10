<#
.SYNOPSIS
    Marca como completo el markdown historico de Documentos en el caso seguro. AB#100247.

.DESCRIPTION
    La migracion MarkdownCobertura (AB#100246) anade MarkdownPaginas y MarkdownCompleto y deja el
    historico en NULL / 0: cobertura desconocida, solo valida como fallback. Este script marca
    MarkdownCompleto = 1 unicamente cuando la ULTIMA ejecucion del documento termino en OK, su
    OrigenMarkdown es uno de los que solo se producen con el documento entero
    (LayoutDocumentoCompletoPostClasificacion, FallbackLayout, LayoutBajoDemandaPrompt) y
    Documentos.Paginas es mayor que cero. No se infieren paginas de recortes: un
    error ahi haria reutilizar 3 paginas como si fueran el documento entero.

    MarkdownPrevio queda deliberadamente FUERA de la lista de origenes seguros, aunque
    tambien puede darse con el documento entero. En codigo, "MarkdownPrevio" significa
    "se reutilizo lo que ya hubiera en datosNormalizados['Markdown']", sin registrar de donde
    vino ese contenido. Ese valor puede ser el recorte de paginas para clasificacion del Paso
    2.8 (a partir de DocumentoBase64Clasif), reetiquetado como MarkdownPrevio cuando la
    extraccion posterior no aporto markdown propio. El origen "LayoutPreClasificacion", que si
    identifica ese recorte de forma explicita, esta correctamente excluido. Pero una vez
    reetiquetado a MarkdownPrevio se pierde la distincion, y las filas historicas no tienen
    forma fiable de recuperarla. Marcar esas filas como completas escribiria cobertura falsa y,
    ademas, las dejaria fuera de alcance para siempre de ActualizarMarkdownSiMejoraAsync (que ya
    no las veria como pendientes de mejorar). Dejarlas en NULL es el resultado correcto.

    Va por lotes y NO forma parte de la migracion EF: recorrer JSON_VALUE sobre el contrato de la
    ultima ejecucion de cada documento dentro de la transaccion de una migracion mantendria
    bloqueos sobre tablas de mas de 1 GB en PRO.

    Idempotente y reanudable: solo toca filas con MarkdownPaginas NULL, MarkdownCompleto 0 y
    markdown presente. Las filas escritas por el codigo nuevo ya tienen cobertura y no se tocan.
    El recorrido va por marca de agua sobre Id dentro de una misma ejecucion: cada lote avanza
    siempre, tambien cuando ninguna de sus filas cumplia el caso seguro. Entre ejecuciones no hay
    marca de agua persistida: volver a lanzar el script recorre otra vez todo el rango de Id, pero
    el WHERE excluye las filas ya marcadas, asi que repetir el recorrido no cambia nada (0 filas
    en esos lotes) y es seguro cortar el script e invocarlo de nuevo.

.PARAMETER Server
    FQDN del servidor SQL. Por defecto el de DEV.

.PARAMETER Database
    Base de datos. Por defecto DocumentIA.

.PARAMETER BatchSize
    Documentos por lote (por rango de Id). Por defecto 2000.

.PARAMETER MaxBatches
    Numero maximo de lotes por ejecucion (0 = sin limite).

.PARAMETER WhatIf
    Solo cuenta cuantas filas cumplen el caso seguro y se marcarian, sin escribir nada.

.EXAMPLE
    ./backfill-markdown-cobertura.ps1 -WhatIf
    ./backfill-markdown-cobertura.ps1
    ./backfill-markdown-cobertura.ps1 -Server srbsqlprodocai.database.windows.net -BatchSize 1000
#>
[CmdletBinding()]
param(
    [string]$Server   = "srbsqldevdocai.database.windows.net",
    [string]$Database = "DocumentIA",
    [int]$BatchSize   = 2000,
    [int]$MaxBatches  = 0,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if (-not $token) { throw "No se pudo obtener token de Entra. Ejecuta 'az login' primero." }

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = "Server=tcp:$Server,1433;Database=$Database;Encrypt=True;"
$conn.AccessToken = $token
$conn.Open()

function Invoke-Scalar([string]$sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $cmd.CommandTimeout = 600
    return $cmd.ExecuteScalar()
}

if (-not (Invoke-Scalar "SELECT COL_LENGTH('dbo.Documentos','MarkdownCompleto');")) {
    throw "La columna MarkdownCompleto no existe. Aplica antes la migracion MarkdownCobertura."
}

# Caso seguro: la fila esta sin cobertura (MarkdownPaginas NULL, MarkdownCompleto 0), tiene
# markdown persistido, Documentos.Paginas es mayor que cero (para no dejar la incoherencia
# "completo pero de longitud desconocida"), y la ULTIMA ejecucion del documento termino OK con
# un OrigenMarkdown que solo se produce procesando el documento entero.
#
# MarkdownPrevio se deja fuera a proposito: en codigo significa "se reutilizo lo que hubiera en
# datosNormalizados['Markdown']" sin registrar su procedencia, y ese contenido puede ser el
# recorte de paginas para clasificacion del Paso 2.8 reetiquetado tras perder el origen
# LayoutPreClasificacion; no hay forma fiable de distinguir ambos casos en las filas historicas,
# asi que se prefiere dejarlas en NULL antes que escribir cobertura falsa.
#
# Extraccion queda fuera por el mismo motivo, y no por uno menor: GptDirectExtraerDataProvider
# asigna MarkdownExtraido = el mismo markdown que se le paso, de modo que "Extraccion" no aporta
# ninguna evidencia sobre la cobertura, solo dice por donde paso el texto. En el codigo historico,
# si la regeneracion a documento completo posterior a la clasificacion fallaba -Layout caido, un
# modo de fallo documentado en este proyecto-, lo que llegaba a la extraccion era el recorte de
# clasificacion y el origen se reescribia igualmente a "Extraccion". Marcar esas filas como
# completas escribiria cobertura falsa y ademas las bloquearia: ActualizarMarkdownSiMejoraAsync
# no sustituye una fila ya marcada como completa.
#
# Dejar una fila en NULL porque su cobertura no se puede afirmar es el resultado correcto: la
# siguiente ejecucion del documento la rellenara con cobertura real.
#
# Este WHERE se reutiliza tanto para contar (WhatIf) como para escribir, asi que el recuento y la
# escritura nunca divergen.
$casoSeguroWhere = @"
d.MarkdownCompleto = 0
  AND d.MarkdownPaginas IS NULL
  AND d.Paginas > 0
  AND (d.NormalizacionMarkdownGzip IS NOT NULL OR d.NormalizacionMarkdownCompressed IS NOT NULL)
  AND u.EstadoFinal = 'OK'
  AND JSON_VALUE(u.ContratoSalidaCompletoJson, '`$.DetalleEjecucion.OrigenMarkdown')
      IN ('LayoutDocumentoCompletoPostClasificacion', 'FallbackLayout', 'LayoutBajoDemandaPrompt')
"@

$pendientes = [int](Invoke-Scalar @"
SELECT COUNT(*)
FROM dbo.Documentos d
CROSS APPLY (
    SELECT TOP 1 e.EstadoFinal, e.ContratoSalidaCompletoJson
    FROM dbo.DocumentoEjecuciones e
    WHERE e.DocumentoId = d.Id
    ORDER BY e.FechaEjecucion DESC
) u
WHERE $casoSeguroWhere;
"@)

$maxId = [int](Invoke-Scalar "SELECT ISNULL(MAX(Id), 0) FROM dbo.Documentos;")
Write-Host "Documentos a recorrer: hasta Id $maxId"
Write-Host "Documentos que cumplen el caso seguro y serian marcados como completo: $pendientes"

if ($WhatIf) {
    Write-Host "WhatIf: no se escribe nada."
    $conn.Close()
    return
}

# Marca de agua sobre Id: cada lote avanza siempre. Un filtro por 'MarkdownPaginas IS NULL' no
# convergeria, porque las filas que no cumplen el caso seguro se quedan en NULL de forma legitima.
$lote = 0
$desde = 0
$totalEscritas = 0
while ($desde -lt $maxId) {
    if ($MaxBatches -gt 0 -and $lote -ge $MaxBatches) {
        Write-Host "Limite de lotes alcanzado ($MaxBatches). Vuelve a ejecutar para continuar."
        break
    }

    $hasta = $desde + $BatchSize

    $escritas = Invoke-Scalar @"
UPDATE d
SET d.MarkdownCompleto = 1,
    d.MarkdownPaginas = NULLIF(d.Paginas, 0),
    d.FechaActualizacion = SYSUTCDATETIME()
FROM dbo.Documentos d
CROSS APPLY (
    SELECT TOP 1 e.EstadoFinal, e.ContratoSalidaCompletoJson
    FROM dbo.DocumentoEjecuciones e
    WHERE e.DocumentoId = d.Id
    ORDER BY e.FechaEjecucion DESC
) u
WHERE d.Id > $desde AND d.Id <= $hasta
  AND $casoSeguroWhere;
SELECT @@ROWCOUNT;
"@

    $lote++
    $desde = $hasta
    $totalEscritas += [int]$escritas
    Write-Host ("Lote {0} (Id <= {1}): {2} documentos marcados como completo (acumulado {3})" -f $lote, $hasta, $escritas, $totalEscritas)
    Start-Sleep -Milliseconds 200
}

$completos    = Invoke-Scalar "SELECT COUNT(*) FROM dbo.Documentos WHERE MarkdownCompleto = 1;"
$desconocidos = Invoke-Scalar "SELECT COUNT(*) FROM dbo.Documentos WHERE MarkdownPaginas IS NULL AND MarkdownCompleto = 0 AND (NormalizacionMarkdownGzip IS NOT NULL OR NormalizacionMarkdownCompressed IS NOT NULL);"
Write-Host "Documentos con markdown completo: $completos"
Write-Host "Documentos con markdown de cobertura desconocida (solo fallback): $desconocidos"

if ($desde -ge $maxId) {
    Write-Host "Backfill completado: recorrida toda la tabla."
} else {
    Write-Host "Backfill parcial: reanudar desde Id $desde en la proxima ejecucion."
}

$conn.Close()
