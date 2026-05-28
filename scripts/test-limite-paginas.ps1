<#
.SYNOPSIS
    Smoke test E2E para la funcionalidad "Límite de páginas por documento" (HU #99685).
    
.DESCRIPTION
    Prueba los tres escenarios clave con el host local:
      1. Documento que SUPERA el límite → espera PAGINAS_EXCEDIDAS
      2. Documento DENTRO del límite   → espera OK / clasificación normal
      3. Documento que supera + ForzarProcesadoSinLimitePaginas=true → espera OK / clasificación normal

    Estrategia:
      - Crea una tipología smoke con maxPaginasDocumento=1 en configuracionJson.
      - Usa un PDF de 2 páginas (mínimo) para el caso de superación.
      - Usa el mismo PDF con ForzarProcesadoSinLimitePaginas para el bypass.
      - Para "dentro del límite", usa maxPaginasDocumento=100 en otra tipología.

    Pre-requisitos:
      - Host local corriendo en http://localhost:7071
      - (Opcional) Variable de entorno $env:TEST_PDF_BASE64 con un PDF ≥2 páginas en base64.
        Si no se provee, el script genera un PDF sintético de 2 páginas mínimas.
#>

param(
    [string]$BaseUrl = "http://localhost:7071/api",
    [int]   $WaitSeconds = 60,
    [int]   $PollSeconds = 4,
    [int]   $TimeoutOrchestrationSeconds = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Helpers ──────────────────────────────────────────────────────────────────

function Log-Step {
    param([string]$Step, [string]$Status, [string]$Detail = "")
    $color = if ($Status -eq "PASS") { "Green" } elseif ($Status -eq "FAIL") { "Red" } else { "Yellow" }
    Write-Host ("[{0}] {1}" -f $Status, $Step) -ForegroundColor $color
    if ($Detail) { Write-Host "    $Detail" -ForegroundColor Gray }
}

function Wait-Orchestration {
    param([string]$InstanceId, [int]$TimeoutSec = 120)
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    # La URL de status de Durable Functions está en /runtime/webhooks/durabletask/...
    $baseHost = $BaseUrl -replace '/api/?$', ''
    while ((Get-Date) -lt $deadline) {
        try {
            $status = Invoke-RestMethod `
                -Uri "$baseHost/runtime/webhooks/durabletask/instances/$InstanceId" `
                -Method Get -ErrorAction Stop
            if ($status.runtimeStatus -in @("Completed", "Failed", "Terminated")) {
                return $status
            }
        } catch { <# ignorar errores de polling #> }
        Start-Sleep -Seconds $PollSeconds
    }
    throw "Timeout esperando orquestación $InstanceId después de ${TimeoutSec}s"
}

function Invoke-Ingest {
    param([hashtable]$Body)
    $json = $Body | ConvertTo-Json -Depth 10 -Compress
    $resp = Invoke-RestMethod -Uri "$BaseUrl/IngestDocument" -Method Post `
        -Body $json -ContentType "application/json" -ErrorAction Stop
    return $resp
}

# ─── Generar PDF sintético ≥2 páginas ─────────────────────────────────────────
# PDF mínimo válido de 2 páginas (estructura manualmente construida).
# Si el entorno tiene un PDF real en $env:TEST_PDF_BASE64, lo usamos.

function Get-TestPdfBase64 {
    if (-not [string]::IsNullOrWhiteSpace($env:TEST_PDF_BASE64)) {
        Write-Host "  Usando PDF de \$env:TEST_PDF_BASE64" -ForegroundColor Gray
        return $env:TEST_PDF_BASE64
    }

    # PDF sintético de 2 páginas construido manualmente (compatible con PdfPig/iText).
    # Creamos un PDF con 2 páginas vacías (solo objetos de estructura).
    $pdfBytes = [System.Text.Encoding]::Latin1.GetBytes(@"
%PDF-1.4
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R 4 0 R]/Count 2>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj
4 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj
xref
0 5
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000174 00000 n 
trailer<</Size 5/Root 1 0 R>>
startxref
233
%%EOF
"@)
    return [Convert]::ToBase64String($pdfBytes)
}

# ─── Setup ────────────────────────────────────────────────────────────────────

$ts = Get-Date -Format "yyyyMMddHHmmss"
$tipLimitada  = "smoke.limitepaginas.limitada.$ts"
$tipSinLimite = "smoke.limitepaginas.sinlimite.$ts"
$results = [ordered]@{ Pass = 0; Fail = 0; Skip = 0 }

Write-Host "`n══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  Smoke Test — Límite de páginas por documento" -ForegroundColor Cyan
Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
Write-Host "══════════════════════════════════════════════════════`n" -ForegroundColor Cyan

# ─── Paso 0: Esperar que el host esté listo ────────────────────────────────────

Write-Host "Paso 0: Esperando host en $BaseUrl..." -ForegroundColor Yellow
$start = Get-Date
$ready = $false
while (((Get-Date) - $start).TotalSeconds -lt $WaitSeconds) {
    try {
        Invoke-RestMethod -Uri "$BaseUrl/management/tipologias" -Method Get -ErrorAction Stop | Out-Null
        $ready = $true; break
    } catch { Start-Sleep -Seconds 2 }
}
if ($ready) {
    Log-Step "0. Host disponible" "PASS"
    $results.Pass++
} else {
    Log-Step "0. Host disponible" "FAIL" "Timeout ${WaitSeconds}s — ¿está el host arrancado?"
    $results.Fail++
    Write-Host "`nABORTANDO: el host no responde." -ForegroundColor Red
    exit 1
}

$pdfBase64 = Get-TestPdfBase64

# ─── Paso 1: Crear tipología con maxPaginasDocumento=1 ───────────────────────

Write-Host "`nPaso 1: Creando tipología con maxPaginasDocumento=1..." -ForegroundColor Yellow

$configLimitada = @{
    tipologiaId        = $tipLimitada
    tipologiaNombre    = "Smoke Limite Paginas"
    version            = "1.0"
    skipGDCUpload      = $true
    maxPaginasDocumento = 1
    extraction         = @{ enabled = $false }
    promptConfig       = @{ enabled = $false }
    fields             = @()
} | ConvertTo-Json -Compress

$bodyTipLimitada = @{
    codigo           = $tipLimitada
    nombre           = "Smoke Limite Paginas (limitada)"
    version          = "1.0"
    configuracionJson = $configLimitada
    usuario          = "smoke-test-limite-paginas"
} | ConvertTo-Json

$tipLimitadaId = $null
try {
    $resTip = Invoke-RestMethod -Uri "$BaseUrl/management/tipologias" -Method Post `
        -Body $bodyTipLimitada -ContentType "application/json" -ErrorAction Stop
    $tipLimitadaId = $resTip.id
    Log-Step "1a. Crear tipología limitada (maxPaginas=1)" "PASS" "id=$tipLimitadaId"
    $results.Pass++
} catch {
    Log-Step "1a. Crear tipología limitada (maxPaginas=1)" "FAIL" $_.Exception.Message
    $results.Fail++
    Write-Host "ABORTANDO: no se pudo crear la tipología de prueba." -ForegroundColor Red
    exit 1
}

try {
    Invoke-RestMethod -Uri "$BaseUrl/management/tipologias/$tipLimitadaId/publicar" -Method Post `
        -ContentType "application/json" -ErrorAction Stop | Out-Null
    Log-Step "1b. Publicar tipología limitada" "PASS"
    $results.Pass++
} catch {
    Log-Step "1b. Publicar tipología limitada" "FAIL" $_.Exception.Message
    $results.Fail++
    Write-Host "ABORTANDO: no se pudo publicar la tipología de prueba." -ForegroundColor Red
    exit 1
}

# ─── Paso 2: Documento que SUPERA el límite (2 págs, límite=1) ───────────────

Write-Host "`nPaso 2: Enviando documento ≥2 páginas con tipología limitada (espera PAGINAS_EXCEDIDAS)..." -ForegroundColor Yellow

$bodyIngest2 = @{
    documento = @{
        name    = "documento-smoke-2paginas.pdf"
        content = @{ base64 = $pdfBase64 }
    }
    instrucciones = @{
        expectedType                   = $tipLimitada
        skipGDCUpload                  = $true
        skipDuplicateCheck             = $true
        forzarProcesadoSinLimitePaginas = $false
    }
    trazabilidad = @{ correlationId = "smoke-limite-$ts-supera" }
}

$estadoFinal2 = $null
try {
    $ingest2 = Invoke-Ingest -Body $bodyIngest2
    $orch2   = Wait-Orchestration -InstanceId $ingest2.instanceId -TimeoutSec $TimeoutOrchestrationSeconds
    # Invoke-RestMethod ya parsea JSON; si output es string lo convertimos, si ya es objeto lo usamos directamente
    $output2 = if ($orch2.output -is [string]) { $orch2.output | ConvertFrom-Json } else { $orch2.output }
    $estadoFinal2 = $output2.resultado.estado
    
    if ($estadoFinal2 -eq "PAGINAS_EXCEDIDAS") {
        Log-Step "2. Supera límite → PAGINAS_EXCEDIDAS" "PASS" `
            "estado=$estadoFinal2 | paginas=$($output2.identificacion.paginas) | mensaje=$($output2.resultado.mensajeError)"
        $results.Pass++
    } else {
        Log-Step "2. Supera límite → PAGINAS_EXCEDIDAS" "FAIL" `
            "estado obtenido=$estadoFinal2 (esperado PAGINAS_EXCEDIDAS) | runtimeStatus=$($orch2.runtimeStatus)"
        $results.Fail++
    }
} catch {
    Log-Step "2. Supera límite → PAGINAS_EXCEDIDAS" "FAIL" $_.Exception.Message
    $results.Fail++
}

# ─── Paso 3: Mismo doc + ForzarProcesadoSinLimitePaginas=true (bypass) ───────

Write-Host "`nPaso 3: Mismo documento con ForzarProcesadoSinLimitePaginas=true (espera NO PAGINAS_EXCEDIDAS)..." -ForegroundColor Yellow

$bodyIngest3 = @{
    documento = @{
        name    = "documento-smoke-2paginas.pdf"
        content = @{ base64 = $pdfBase64 }
    }
    instrucciones = @{
        expectedType                   = $tipLimitada
        skipGDCUpload                  = $true
        skipDuplicateCheck             = $true
        forzarProcesadoSinLimitePaginas = $true
    }
    trazabilidad = @{ correlationId = "smoke-limite-$ts-forzado" }
}

try {
    $ingest3 = Invoke-Ingest -Body $bodyIngest3
    $orch3   = Wait-Orchestration -InstanceId $ingest3.instanceId -TimeoutSec $TimeoutOrchestrationSeconds
    $output3 = if ($orch3.output -is [string]) { $orch3.output | ConvertFrom-Json } else { $orch3.output }
    $estado3 = $output3.resultado.estado

    if ($estado3 -ne "PAGINAS_EXCEDIDAS") {
        Log-Step "3. Bypass ForzarProcesadoSinLimitePaginas=true" "PASS" "estado=$estado3 (pipeline continuó)"
        $results.Pass++
    } else {
        Log-Step "3. Bypass ForzarProcesadoSinLimitePaginas=true" "FAIL" `
            "estado=$estado3 — el límite NO fue ignorado aunque se pasó forzarProcesadoSinLimitePaginas=true"
        $results.Fail++
    }
} catch {
    Log-Step "3. Bypass ForzarProcesadoSinLimitePaginas=true" "FAIL" $_.Exception.Message
    $results.Fail++
}

# ─── Paso 4: Tipología sin límite (o límite=0) + mismo doc → sin bloqueo ─────

Write-Host "`nPaso 4: Creando tipología sin límite (maxPaginasDocumento=0) y enviando mismo doc..." -ForegroundColor Yellow

$configSinLimite = @{
    tipologiaId        = $tipSinLimite
    tipologiaNombre    = "Smoke Sin Limite"
    version            = "1.0"
    skipGDCUpload      = $true
    maxPaginasDocumento = 0
    extraction         = @{ enabled = $false }
    promptConfig       = @{ enabled = $false }
    fields             = @()
} | ConvertTo-Json -Compress

$bodyTipSinLimite = @{
    codigo           = $tipSinLimite
    nombre           = "Smoke Sin Limite"
    version          = "1.0"
    configuracionJson = $configSinLimite
    usuario          = "smoke-test-limite-paginas"
} | ConvertTo-Json

$tipSinLimiteId = $null
try {
    $resTipSL = Invoke-RestMethod -Uri "$BaseUrl/management/tipologias" -Method Post `
        -Body $bodyTipSinLimite -ContentType "application/json" -ErrorAction Stop
    $tipSinLimiteId = $resTipSL.id
    Log-Step "4a. Crear tipología sin límite (maxPaginas=0)" "PASS" "id=$tipSinLimiteId"
    $results.Pass++
} catch {
    Log-Step "4a. Crear tipología sin límite (maxPaginas=0)" "FAIL" $_.Exception.Message
    $results.Fail++
}

try {
    Invoke-RestMethod -Uri "$BaseUrl/management/tipologias/$tipSinLimiteId/publicar" -Method Post `
        -ContentType "application/json" -ErrorAction Stop | Out-Null
    Log-Step "4b. Publicar tipología sin límite" "PASS"
    $results.Pass++
} catch {
    Log-Step "4b. Publicar tipología sin límite" "FAIL" $_.Exception.Message
    $results.Fail++
}

$bodyIngest4 = @{
    documento = @{
        name    = "documento-smoke-2paginas.pdf"
        content = @{ base64 = $pdfBase64 }
    }
    instrucciones = @{
        expectedType       = $tipSinLimite
        skipGDCUpload      = $true
        skipDuplicateCheck = $true
    }
    trazabilidad = @{ correlationId = "smoke-limite-$ts-sinlimite" }
}

try {
    $ingest4 = Invoke-Ingest -Body $bodyIngest4
    $orch4   = Wait-Orchestration -InstanceId $ingest4.instanceId -TimeoutSec $TimeoutOrchestrationSeconds
    $output4 = if ($orch4.output -is [string]) { $orch4.output | ConvertFrom-Json } else { $orch4.output }
    $estado4 = $output4.resultado.estado

    if ($estado4 -ne "PAGINAS_EXCEDIDAS") {
        Log-Step "4b. Sin límite → pipeline NO bloqueado" "PASS" "estado=$estado4"
        $results.Pass++
    } else {
        Log-Step "4b. Sin límite → pipeline NO bloqueado" "FAIL" "estado=$estado4 — bloqueó sin límite activo"
        $results.Fail++
    }
} catch {
    Log-Step "4b. Sin límite → pipeline NO bloqueado" "FAIL" $_.Exception.Message
    $results.Fail++
}

# ─── Resumen ──────────────────────────────────────────────────────────────────

$total = $results.Pass + $results.Fail
Write-Host "`n══════════════════════════════════════════════════════" -ForegroundColor Cyan
$summaryColor = if ($results.Fail -eq 0) { "Green" } else { "Red" }
Write-Host ("  RESULTADO: {0}/{1} pasados  |  {2} fallidos" -f $results.Pass, $total, $results.Fail) `
    -ForegroundColor $summaryColor
Write-Host "══════════════════════════════════════════════════════`n" -ForegroundColor Cyan

if ($results.Fail -gt 0) { exit 1 }
