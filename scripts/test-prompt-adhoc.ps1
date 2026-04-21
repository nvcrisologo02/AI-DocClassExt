# Test E2E de prompt ad-hoc por peticion HTTP (Epic AB#99122)
#
# Escenarios:
#   1. Prompt completo sin tipologia configurada (override completo)
#   2. Prompt parcial sobre tipologia con PromptConfig (override parcial — solo temperatura y maxTokens)
#   3. Sin prompt en peticion (comportamiento base sin cambios)
#
# Uso:
#   .\test-prompt-adhoc.ps1 [-Endpoint <url>] [-Escenario <1|2|3|todos>] [-SkipPolling]
#
# Ejemplo:
#   .\test-prompt-adhoc.ps1 -Escenario todos
#   .\test-prompt-adhoc.ps1 -Escenario 1 -Endpoint "https://mifunc.azurewebsites.net/api"

param(
    [string]$Endpoint = "http://localhost:7071/api/IngestDocument",
    [ValidateSet("1","2","3","todos")]
    [string]$Escenario = "todos",
    [switch]$SkipPolling
)

$ErrorActionPreference = "Stop"

# PDF minimo valido en Base64 (1 pagina, sin contenido)
$PdfBase64Minimo = "JVBERi0xLjQKJeLjz9MKMSAwIG9iajw8L1R5cGUvQ2F0YWxvZy9QYWdlcyAyIDAgUj4+ZW5kb2JqCjIgMCBvYmo8PC9UeXBlL1BhZ2VzL0NvdW50IDEvS2lkc1szIDAgUl0+PmVuZG9iagozIDAgb2JqPDwvVHlwZS9QYWdlL01lZGlhQm94WzAgMCA2MTIgNzkyXS9QYXJlbnQgMiAwIFIvUmVzb3VyY2VzPDw+Pj4+ZW5kb2JqCnhyZWYKMCA0CjAwMDAwMDAwMDAgNjU1MzUgZiAKMDAwMDAwMDAxNSAwMDAwMCBuIAowMDAwMDAwMDY0IDAwMDAwIG4gCjAwMDAwMDAxMjEgMDAwMDAgbiAKdHJhaWxlcjw8L1NpemUgNC9Sb290IDEgMCBSPj4Kc3RhcnR4cmVmCjE5NAolJUVPRg=="

function Write-Header([string]$titulo) {
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host "  $titulo" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host ""
}

function Write-Ok([string]$msg)   { Write-Host "[OK]  $msg" -ForegroundColor Green }
function Write-Err([string]$msg)  { Write-Host "[ERR] $msg" -ForegroundColor Red }
function Write-Info([string]$msg) { Write-Host "      $msg" -ForegroundColor Gray }

function Invoke-Ingest([hashtable]$body, [string]$nombreTest) {
    Write-Host "Enviando: $nombreTest..." -ForegroundColor Yellow
    $json = $body | ConvertTo-Json -Depth 15

    try {
        $resp = Invoke-RestMethod -Uri $Endpoint -Method Post -Body $json -ContentType "application/json"
        Write-Ok "202 Accepted — instanceId: $($resp.instanceId)"
        return $resp
    }
    catch {
        $statusCode = $_.Exception.Response?.StatusCode.value__
        $detail = $_.ErrorDetails?.Message
        if ($statusCode -eq 400) {
            Write-Host "[400] Bad Request (esperado en validacion): $detail" -ForegroundColor Yellow
        } else {
            Write-Err "Error HTTP $statusCode`: $detail"
        }
        return $null
    }
}

function Wait-Resultado([string]$statusQueryUri, [string]$instanceId) {
    if ($SkipPolling -or -not $statusQueryUri) { return }

    Write-Info "Polling instancia $instanceId ..."
    $max = 20
    $n   = 0
    do {
        Start-Sleep -Seconds 4
        $n++
        try {
            $st = Invoke-RestMethod -Uri $statusQueryUri
        } catch { Write-Err "Error en polling: $_"; return }

        $actividad = $st.customStatus?.actividad ?? "-"
        Write-Info "[$n/$max] $($st.runtimeStatus) — actividad: $actividad"
    } while ($st.runtimeStatus -in @("Pending","Running") -and $n -lt $max)

    if ($st.runtimeStatus -eq "Completed") {
        Write-Ok "Completado"
        $out = $st.output
        if ($out) {
            Write-Info "Estado resultado : $($out.resultado?.estado)"
            Write-Info "Tipologia        : $($out.identificacion?.tipologia)"
            $promptResult = $out.datosExtraidos?.PromptResult
            if ($promptResult) {
                Write-Ok "PromptResult obtenido:"
                Write-Host $promptResult -ForegroundColor White
            } else {
                Write-Info "PromptResult     : (no present en datosExtraidos)"
            }
        }
    } elseif ($st.runtimeStatus -eq "Failed") {
        Write-Err "Orquestacion fallida: $($st.output)"
    } else {
        Write-Info "Estado final: $($st.runtimeStatus)"
    }
}

# ---------------------------------------------------------------------------
# ESCENARIO 1 — Prompt completo ad-hoc sin PromptConfig en tipologia
# ---------------------------------------------------------------------------
function Run-Escenario1 {
    Write-Header "ESCENARIO 1: Prompt completo ad-hoc (sin PromptConfig en tipologia)"
    Write-Info "Tipologia: resumen.documental (o cualquiera sin PromptConfig)"
    Write-Info "Espera: PromptResult en datosExtraidos con resumen del documento"
    Write-Host ""

    $body = @{
        instrucciones = @{
            expectedType       = "resumen.documental"
            skipDuplicateCheck = $true
            forceReprocess     = $true
            prompt = @{
                systemPrompt       = "Eres un asistente experto en documentos inmobiliarios."
                userPromptTemplate = "Extrae en 3 puntos clave la informacion mas relevante del siguiente documento:`n`n{{CONTENT}}"
                modelKey           = "gpt-4o-mini"
                temperature        = 0.2
                maxTokens          = 600
                contentMode        = "markdown"
            }
        }
        documento = @{
            name    = "test-prompt-adhoc-escenario1.pdf"
            content = @{ base64 = $PdfBase64Minimo }
        }
        trazabilidad = @{
            correlationId = [Guid]::NewGuid().ToString()
            submittedBy   = "test-prompt-adhoc"
        }
    }

    $resp = Invoke-Ingest $body "Escenario 1 — override completo"
    if ($resp) { Wait-Resultado $resp.statusQueryUri $resp.instanceId }
}

# ---------------------------------------------------------------------------
# ESCENARIO 2 — Prompt parcial (solo temperatura y maxTokens) sobre tipologia con PromptConfig
# ---------------------------------------------------------------------------
function Run-Escenario2 {
    Write-Header "ESCENARIO 2: Override parcial sobre tipologia con PromptConfig"
    Write-Info "Tipologia: nota.simple.1_4 (tiene PromptConfig configurada)"
    Write-Info "Override: solo temperature=0.7 y maxTokens=1200 — resto viene de tipologia"
    Write-Host ""

    $body = @{
        instrucciones = @{
            expectedType       = "nota.simple.1_4"
            skipDuplicateCheck = $true
            forceReprocess     = $true
            prompt = @{
                temperature = 0.7
                maxTokens   = 1200
            }
        }
        documento = @{
            name    = "test-prompt-adhoc-escenario2.pdf"
            content = @{ base64 = $PdfBase64Minimo }
        }
        trazabilidad = @{
            correlationId = [Guid]::NewGuid().ToString()
            submittedBy   = "test-prompt-adhoc"
        }
    }

    $resp = Invoke-Ingest $body "Escenario 2 — override parcial"
    if ($resp) { Wait-Resultado $resp.statusQueryUri $resp.instanceId }
}

# ---------------------------------------------------------------------------
# ESCENARIO 3 — Sin prompt en peticion (comportamiento base)
# ---------------------------------------------------------------------------
function Run-Escenario3 {
    Write-Header "ESCENARIO 3: Sin instrucciones.prompt (comportamiento base)"
    Write-Info "Tipologia: nota.simple.1_4"
    Write-Info "Espera: sin PromptResult si tipologia no tiene PromptEnabled"
    Write-Host ""

    $body = @{
        instrucciones = @{
            expectedType       = "nota.simple.1_4"
            skipDuplicateCheck = $true
            forceReprocess     = $true
        }
        documento = @{
            name    = "test-prompt-adhoc-escenario3.pdf"
            content = @{ base64 = $PdfBase64Minimo }
        }
        trazabilidad = @{
            correlationId = [Guid]::NewGuid().ToString()
            submittedBy   = "test-prompt-adhoc"
        }
    }

    $resp = Invoke-Ingest $body "Escenario 3 — sin prompt"
    if ($resp) { Wait-Resultado $resp.statusQueryUri $resp.instanceId }
}

# ---------------------------------------------------------------------------
# ESCENARIO VALIDACION — Prompt con valores fuera de rango (debe devolver 400)
# ---------------------------------------------------------------------------
function Run-EscenarioValidacion {
    Write-Header "VALIDACION: Prompt con valores invalidos (espera 400)"
    Write-Info "temperature=5.0 (fuera de [0.0-2.0]) -> debe devolver 400 Bad Request"
    Write-Host ""

    $body = @{
        instrucciones = @{
            expectedType = "nota.simple.1_4"
            prompt = @{
                userPromptTemplate = "Resume este documento."
                temperature        = 5.0
                maxTokens          = 800
            }
        }
        documento = @{
            name    = "test-validacion-prompt.pdf"
            content = @{ base64 = $PdfBase64Minimo }
        }
        trazabilidad = @{
            correlationId = [Guid]::NewGuid().ToString()
            submittedBy   = "test-prompt-adhoc"
        }
    }

    Invoke-Ingest $body "Validacion — temperature invalida" | Out-Null
    Write-Info "(Si aparecio [400] arriba, la validacion funciona correctamente)"
}

# ---------------------------------------------------------------------------
# EJECUCION
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Test E2E: Prompt Ad-Hoc por Peticion HTTP" -ForegroundColor Magenta
Write-Host "Endpoint: $Endpoint" -ForegroundColor Gray
Write-Host "Escenario: $Escenario" -ForegroundColor Gray
if ($SkipPolling) { Write-Host "Polling: desactivado" -ForegroundColor Gray }
Write-Host ""

switch ($Escenario) {
    "1"      { Run-Escenario1 }
    "2"      { Run-Escenario2 }
    "3"      { Run-Escenario3 }
    "todos"  {
        Run-Escenario1
        Run-Escenario2
        Run-Escenario3
        Run-EscenarioValidacion
    }
}

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  FIN DEL TEST" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ""
