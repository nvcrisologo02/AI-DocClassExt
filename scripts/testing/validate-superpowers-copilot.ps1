param(
    [Parameter(Mandatory = $false)]
    [string]$PluginPath = ".superpowers.old",

    [Parameter(Mandatory = $false)]
    [string]$ArtifactsDir = "artifacts/superpowers-validation"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Title)

    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & $Command @Arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output.TrimEnd()
    }
}

function Test-SkillInvocation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Output,

        [Parameter(Mandatory = $true)]
        [string]$SkillName
    )

    $pattern = '"skill":"([^\"]*:)?' + [Regex]::Escape($SkillName) + '"'
    return ($Output -match '"name":"Skill"') -and ($Output -match $pattern)
}

function New-CheckResult {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Summary,
        [string]$Evidence
    )

    return [pscustomobject]@{
        Name = $Name
        Status = $Status
        Summary = $Summary
        Evidence = $Evidence
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedPluginPath = (Resolve-Path (Join-Path $repoRoot $PluginPath)).Path
$resolvedArtifactsDir = Join-Path $repoRoot $ArtifactsDir
$reportPath = Join-Path $resolvedArtifactsDir "report.md"

New-Item -ItemType Directory -Force -Path $resolvedArtifactsDir | Out-Null

$results = New-Object System.Collections.Generic.List[object]
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ssK"

Write-Section "Entorno"

$copilotCommand = Get-Command copilot -ErrorAction SilentlyContinue
if (-not $copilotCommand) {
    $results.Add((New-CheckResult -Name "Copilot CLI" -Status "FAIL" -Summary "El comando 'copilot' no esta disponible en PATH." -Evidence "No se encontro ejecutable."))
}
else {
    $versionResult = Invoke-NativeCommand -Command $copilotCommand.Source -Arguments @("--version")
    $results.Add((New-CheckResult -Name "Copilot CLI" -Status $(if ($versionResult.ExitCode -eq 0) { "PASS" } else { "FAIL" }) -Summary "Version detectada del runtime de Copilot CLI." -Evidence $versionResult.Output))
}

if (-not (Test-Path $resolvedPluginPath)) {
    $results.Add((New-CheckResult -Name "Plugin local" -Status "FAIL" -Summary "No existe la ruta local del plugin Superpowers." -Evidence $resolvedPluginPath))
}
else {
    $packageJsonPath = Join-Path $resolvedPluginPath "package.json"
    $hasPackageJson = Test-Path $packageJsonPath
    $packageSummary = if ($hasPackageJson) { Get-Content -Raw -Path $packageJsonPath } else { "package.json no encontrado" }
    $results.Add((New-CheckResult -Name "Plugin local" -Status $(if ($hasPackageJson) { "PASS" } else { "FAIL" }) -Summary "Estructura base del plugin local cargable por Copilot CLI." -Evidence $packageSummary))
}

if ($copilotCommand) {
    Write-Section "Plugins instalados"
    $pluginListResult = Invoke-NativeCommand -Command $copilotCommand.Source -Arguments @("plugin", "list")
    $pluginListStatus = if ($pluginListResult.ExitCode -eq 0) { "PASS" } else { "FAIL" }
    $pluginListSummary = if ($pluginListResult.Output -match 'No plugins installed') {
        "No hay plugins instalados globalmente en Copilot CLI."
    }
    else {
        "Copilot CLI devuelve el listado de plugins instalados."
    }
    $results.Add((New-CheckResult -Name "Plugins instalados" -Status $pluginListStatus -Summary $pluginListSummary -Evidence $pluginListResult.Output))

    if (Test-Path $resolvedPluginPath) {
        Write-Section "Prueba de descubrimiento"

        $explicitPrompt = "Please use brainstorming. I have an idea for a feature and need a design approach."
        $explicitOutputPath = Join-Path $resolvedArtifactsDir "explicit-brainstorming.jsonl"
        $explicitArgs = @(
            "-p", $explicitPrompt,
            "--plugin-dir", $resolvedPluginPath,
            "--allow-all",
            "--output-format", "json",
            "--no-color"
        )

        $explicitResult = Invoke-NativeCommand -Command $copilotCommand.Source -Arguments $explicitArgs
        $explicitResult.Output | Set-Content -Path $explicitOutputPath -Encoding utf8

        if ($explicitResult.Output -match 'No authentication information found') {
            $results.Add((New-CheckResult -Name "Prompt explicito" -Status "BLOCKED" -Summary "Copilot CLI no puede ejecutar prompts porque falta autenticacion de GitHub." -Evidence $explicitResult.Output))
        }
        elseif ($explicitResult.ExitCode -ne 0) {
            $results.Add((New-CheckResult -Name "Prompt explicito" -Status "FAIL" -Summary "La ejecucion del prompt explicito fallo antes de poder verificar la skill." -Evidence $explicitResult.Output))
        }
        elseif (Test-SkillInvocation -Output $explicitResult.Output -SkillName "brainstorming") {
            $results.Add((New-CheckResult -Name "Prompt explicito" -Status "PASS" -Summary "El prompt explicito dispara la skill de brainstorming." -Evidence $explicitResult.Output))
        }
        else {
            $results.Add((New-CheckResult -Name "Prompt explicito" -Status "FAIL" -Summary "El prompt se ejecuto, pero no hay evidencia de invocacion de la skill brainstorming." -Evidence $explicitResult.Output))
        }

        $naturalPrompt = "I have an idea for something I'd like to build."
        $naturalOutputPath = Join-Path $resolvedArtifactsDir "natural-build-idea.jsonl"
        $naturalArgs = @(
            "-p", $naturalPrompt,
            "--plugin-dir", $resolvedPluginPath,
            "--allow-all",
            "--output-format", "json",
            "--no-color"
        )

        $naturalResult = Invoke-NativeCommand -Command $copilotCommand.Source -Arguments $naturalArgs
        $naturalResult.Output | Set-Content -Path $naturalOutputPath -Encoding utf8

        if ($naturalResult.Output -match 'No authentication information found') {
            $results.Add((New-CheckResult -Name "Prompt natural" -Status "BLOCKED" -Summary "La validacion de activacion automatica esta bloqueada por falta de autenticacion." -Evidence $naturalResult.Output))
        }
        elseif ($naturalResult.ExitCode -ne 0) {
            $results.Add((New-CheckResult -Name "Prompt natural" -Status "FAIL" -Summary "La ejecucion del prompt natural fallo antes de verificar activacion automatica." -Evidence $naturalResult.Output))
        }
        elseif ((Test-SkillInvocation -Output $naturalResult.Output -SkillName "using-superpowers") -or (Test-SkillInvocation -Output $naturalResult.Output -SkillName "brainstorming")) {
            $results.Add((New-CheckResult -Name "Prompt natural" -Status "PASS" -Summary "Hay evidencia de activacion automatica de skills ante un prompt de inicio de proyecto." -Evidence $naturalResult.Output))
        }
        else {
            $results.Add((New-CheckResult -Name "Prompt natural" -Status "FAIL" -Summary "El prompt natural se ejecuto, pero no hay evidencia de activacion automatica de skills." -Evidence $naturalResult.Output))
        }
    }
}

$overallStatus = if ($results.Status -contains "FAIL") {
    "FAIL"
}
elseif ($results.Status -contains "BLOCKED") {
    "BLOCKED"
}
else {
    "PASS"
}

$reportLines = New-Object System.Collections.Generic.List[string]
$reportLines.Add("# Validacion Superpowers en Copilot CLI")
$reportLines.Add("")
$reportLines.Add("- Fecha: $timestamp")
$reportLines.Add("- Repo: $repoRoot")
$reportLines.Add("- Plugin local: $resolvedPluginPath")
$reportLines.Add("- Estado global: $overallStatus")
$reportLines.Add("")
$reportLines.Add("## Resultados")
$reportLines.Add("")

foreach ($result in $results) {
    $reportLines.Add("### $($result.Name)")
    $reportLines.Add("")
    $reportLines.Add("- Estado: $($result.Status)")
    $reportLines.Add("- Resumen: $($result.Summary)")
    $reportLines.Add("- Evidencia:")
    $reportLines.Add('```text')
    $reportLines.Add(($result.Evidence | Out-String).TrimEnd())
    $reportLines.Add('```')
    $reportLines.Add("")
}

$blockedItems = $results | Where-Object { $_.Status -eq "BLOCKED" }
if ($blockedItems) {
    $reportLines.Add("## Bloqueos")
    $reportLines.Add("")
    foreach ($item in $blockedItems) {
        $reportLines.Add("- $($item.Name): $($item.Summary)")
    }
    $reportLines.Add("")
}

$reportLines.Add("## Proximo paso recomendado")
$reportLines.Add("")
if ($overallStatus -eq "BLOCKED") {
    $reportLines.Add("- Ejecutar 'copilot login' o configurar GH_TOKEN/COPILOT_GITHUB_TOKEN y relanzar este script.")
}
elseif ($overallStatus -eq "FAIL") {
    $reportLines.Add("- Corregir los fallos anteriores y volver a ejecutar este script para obtener una prueba end-to-end real.")
}
else {
    $reportLines.Add("- Anadir mas prompts de aceptacion si quieres cobertura sobre otras skills como systematic-debugging o writing-plans.")
}

$reportLines | Set-Content -Path $reportPath -Encoding utf8

Write-Section "Resumen"
Write-Host "Estado global: $overallStatus"
Write-Host "Informe: $reportPath"