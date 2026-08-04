<#
.SYNOPSIS
    Crea (o actualiza) las alertas productivas de Azure Monitor sobre Application Insights (AB#99083, cierre F6.3).

.DESCRIPTION
    Crea 5 scheduled query rules (alertas de logs) sobre el recurso de Application Insights:

      1. Tasa de errores        : % de DocumentProcessed con EstadoFinal de error (>10% en 5 min, Sev 2)
      2. Latencia E2E excesiva  : p95 de DocumentIA.Duracion.Total > 120 s en 15 min (Sev 2)
      3. Fallback GPT elevado   : % de DocumentProcessed con UseFallbackLLM=true (>20% en 30 min, Sev 3)
      4. Excepciones elevadas   : > 10 excepciones en 5 min (Sev 2) - cubre tambien fallos GDC hasta
                                  que exista un evento especifico (GdcUploadFailed no se emite hoy)
      5. Sin actividad          : 0 requests en 60 min dentro de horario laboral L-V 8-18 Europe/Madrid (Sev 2)

    Ajustes respecto al plan original (docs 7.3.4 de 2026-06-09), alineados con la telemetria real del codigo:
      - No existe el evento GdcUploadFailed ni GptFallbackUsed; el fallback se mide con la dimension
        UseFallbackLLM del evento DocumentProcessed (PersistirActivity.EmitirTelemetria).
      - "Error" se evalua contra los estados Error/ERROR/Fallido (mismo criterio que DocumentoEjecucionRepository),
        no como EstadoFinal != "OK", para no contar REVISION como error.
      - Las alertas de ratio exigen un minimo de 5 documentos en la ventana para evitar ruido con volumen bajo.

    Es idempotente: si la regla ya existe se actualiza (update), si no, se crea (create).
    Sin -ActionGroupId las reglas se crean sin acciones (visibles en el portal de Alertas); cuando exista
    el action group de operaciones, re-ejecutar con -ActionGroupId para asociarlo.

.PARAMETER ResourceGroup
    Resource group del recurso de Application Insights. Por defecto SRBRGDOCSAIPROD.

.PARAMETER AppInsightsName
    Nombre del recurso de Application Insights. Por defecto srbappiprodocai.

.PARAMETER ActionGroupId
    (Opcional) Resource ID completo del action group a asociar a todas las reglas.

.EXAMPLE
    ./create-monitor-alerts.ps1
    ./create-monitor-alerts.ps1 -ActionGroupId "/subscriptions/<sub>/resourceGroups/<rg>/providers/microsoft.insights/actionGroups/<nombre>"
#>
[CmdletBinding()]
param(
    [string]$ResourceGroup   = "SRBRGDOCSAIPROD",
    [string]$AppInsightsName = "srbappiprodocai",
    [string]$ActionGroupId   = ""
)

# "Continue" y control por $LASTEXITCODE: az escribe avisos por stderr (p. ej. el del proxy corporativo)
# y con "Stop" PowerShell 5.1 los convertiria en errores terminales.
$ErrorActionPreference = "Continue"

# --- Resolver el recurso de App Insights ---
$appInsights = az resource show -g $ResourceGroup -n $AppInsightsName --resource-type "Microsoft.Insights/components" -o json 2>$null | ConvertFrom-Json
if (-not $appInsights) {
    throw "No se encuentra Application Insights '$AppInsightsName' en el RG '$ResourceGroup'. Verifica 'az login' y la suscripcion activa."
}
$scope    = $appInsights.id
$location = $appInsights.location
Write-Host "Application Insights: $scope ($location)" -ForegroundColor Cyan

# --- Definicion de las 5 reglas ---
# Cada query devuelve filas SOLO cuando se incumple el umbral; la condicion es "count 'Incumplimientos' > 0".
$rules = @(
    @{
        Name        = "srbalerterrprodocai"
        DisplayName = "DocumentIA - Tasa de errores > 10% (5 min)"
        Description = "Porcentaje de eventos DocumentProcessed con EstadoFinal de error (Error/ERROR/Fallido) supera el 10% en ventana de 5 minutos (minimo 5 documentos). AB#99083."
        Severity    = 2
        WindowSize  = "5m"
        Frequency   = "5m"
        Query       = @"
customEvents
| where name == 'DocumentProcessed'
| extend estado = tostring(customDimensions['EstadoFinal'])
| summarize total = count(), errores = countif(estado in ('Error', 'ERROR', 'Fallido'))
| where total >= 5 and 100.0 * errores / total > 10
"@
    },
    @{
        Name        = "srbalertlatprodocai"
        DisplayName = "DocumentIA - Latencia E2E p95 > 120 s (15 min)"
        Description = "El p95 de la metrica DocumentIA.Duracion.Total supera 120 segundos en ventana de 15 minutos. AB#99083."
        Severity    = 2
        WindowSize  = "15m"
        Frequency   = "15m"
        Query       = @"
customMetrics
| where name == 'DocumentIA.Duracion.Total'
| extend duracionMs = valueSum / valueCount
| summarize p95Ms = percentile(duracionMs, 95)
| where p95Ms > 120000
"@
    },
    @{
        Name        = "srbalertfbkprodocai"
        DisplayName = "DocumentIA - Fallback GPT > 20% (30 min)"
        Description = "Porcentaje de eventos DocumentProcessed con UseFallbackLLM=true supera el 20% en ventana de 30 minutos (minimo 5 documentos). Aviso de calidad. AB#99083."
        Severity    = 3
        WindowSize  = "30m"
        Frequency   = "15m"
        Query       = @"
customEvents
| where name == 'DocumentProcessed'
| extend fallback = tostring(customDimensions['UseFallbackLLM'])
| summarize total = count(), conFallback = countif(fallback =~ 'true')
| where total >= 5 and 100.0 * conFallback / total > 20
"@
    },
    @{
        Name        = "srbalertexcprodocai"
        DisplayName = "DocumentIA - Excepciones > 10 (5 min)"
        Description = "Mas de 10 excepciones registradas en Application Insights en ventana de 5 minutos. Cubre tambien fallos de integracion GDC mientras no exista evento especifico. AB#99083."
        Severity    = 2
        WindowSize  = "5m"
        Frequency   = "5m"
        Query       = @"
exceptions
| summarize n = count()
| where n > 10
"@
    },
    @{
        Name        = "srbalertidleprodocai"
        DisplayName = "DocumentIA - Sin actividad en horario laboral (60 min)"
        Description = "Cero requests en 60 minutos dentro de horario laboral (L-V 8:00-18:00 Europe/Madrid). Posible caida de la Function App o del flujo de entrada. AB#99083."
        Severity    = 2
        WindowSize  = "1h"
        Frequency   = "15m"
        Query       = @"
requests
| summarize n = count()
| extend ahora = datetime_utc_to_local(now(), 'Europe/Madrid')
| where dayofweek(ahora) between (1d .. 5d) and datetime_part('hour', ahora) >= 8 and datetime_part('hour', ahora) < 18
| where n == 0
"@
    }
)

# --- Crear / actualizar ---
$existing = az monitor scheduled-query list -g $ResourceGroup --query "[].name" -o json 2>$null | ConvertFrom-Json
if (-not $existing) { $existing = @() }

foreach ($rule in $rules) {
    $verb = if ($existing -contains $rule.Name) { "update" } else { "create" }
    Write-Host "[$verb] $($rule.Name) - $($rule.DisplayName)" -ForegroundColor Yellow

    # La query se aplana a una linea: PowerShell 5.1 corrompe los argumentos posteriores a un
    # argumento nativo multilinea, y KQL admite la expresion completa en una sola linea.
    $queryFlat = ($rule.Query -split "`r?`n" | Where-Object { $_.Trim() } | ForEach-Object { $_.Trim() }) -join " "

    $azArgs = @(
        "monitor", "scheduled-query", $verb,
        "-g", $ResourceGroup,
        "-n", $rule.Name
    )
    # --scopes y --location solo se admiten en create; update conserva los del recurso existente
    if ($verb -eq "create") { $azArgs += @("--scopes", $scope, "--location", $location) }
    $azArgs += @(
        "--description", $rule.Description,
        "--severity", $rule.Severity,
        "--window-size", $rule.WindowSize,
        "--evaluation-frequency", $rule.Frequency,
        "--condition", "count 'Incumplimientos' > 0",
        "--condition-query", "Incumplimientos=$queryFlat",
        "--auto-mitigate", "true"
    )
    if ($ActionGroupId) { $azArgs += @("--action-groups", $ActionGroupId) }

    az @azArgs -o none
    if ($LASTEXITCODE -ne 0) { throw "Fallo al $verb la regla $($rule.Name)" }
}

Write-Host "`nReglas resultantes en ${ResourceGroup}:" -ForegroundColor Cyan
az monitor scheduled-query list -g $ResourceGroup --query "[].{name:name, enabled:enabled, severity:severity, windowSize:windowSize, frequency:evaluationFrequency}" -o table
