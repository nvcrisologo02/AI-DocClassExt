# =============================================================================
# Deploy manual de Azure Functions a srbappprodocai
#
# USO BASICO (solo build + zip):
#   .\scripts\deploy-manual.ps1
#
# USO CON DEPLOY DIRECTO via Kudu (sin Cloud Shell):
#   1. En Cloud Shell, obtener credenciales de publicacion:
#      az functionapp deployment list-publishing-credentials `
#        --resource-group SRBRGDOCSAIPROD --name srbappprodocai `
#        --query "{user:publishingUserName,pass:publishingPassword}" -o tsv
#   2. Ejecutar con esas credenciales:
#      .\scripts\deploy-manual.ps1 -KuduUser '$srbappprodocai' -KuduPassword 'xxx'
#
# USO CON DEPLOY VIA CLOUD SHELL (si no tienes credenciales Kudu):
#   1. Ejecutar este script sin parametros para generar el zip
#   2. Subir publish\functions.zip al Cloud Shell (boton Upload)
#   3. En Cloud Shell ejecutar:
#      az functionapp deploy --resource-group SRBRGDOCSAIPROD --name srbappprodocai `
#        --src-path ~/functions.zip --type zip
# =============================================================================

param(
    [string]$KuduUser     = "",
    [string]$KuduPassword = ""
)

$ErrorActionPreference = "Stop"

$root        = Split-Path $PSScriptRoot -Parent
$publishDir  = "$root\publish\functions"
$zipPath     = "$root\publish\functions.zip"
$functionApp = "srbappprodocai"
$kuduUrl     = "https://$functionApp.scm.azurewebsites.net/api/zipdeploy"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  DEPLOY MANUAL - $functionApp" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# ─── Limpiar output anterior ─────────────────────────────────────────────────
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $zipPath)    { Remove-Item $zipPath -Force }
New-Item -ItemType Directory -Force -Path (Split-Path $zipPath -Parent) | Out-Null

# ─── PASO 1: Compilar SarebEnrichments ───────────────────────────────────────
Write-Host "[1/5] Compilando SarebEnrichments..." -ForegroundColor Yellow
Push-Location "$root\src\enrichments\SarebEnrichments"
try {
    dotnet build --configuration Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Fallo compilacion de SarebEnrichments" }
    Write-Host "  [OK]" -ForegroundColor Green
} finally { Pop-Location }

# ─── PASO 2: Copiar DLL a plugins/ ───────────────────────────────────────────
Write-Host "[2/5] Copiando SarebEnrichments.dll a plugins/..." -ForegroundColor Yellow
$srcDll = "$root\src\enrichments\SarebEnrichments\bin\Release\net8.0\SarebEnrichments.dll"
if (-not (Test-Path $srcDll)) {
    throw "DLL no encontrada en: $srcDll"
}
Copy-Item $srcDll "$root\plugins\" -Force
Write-Host "  [OK] $root\plugins\SarebEnrichments.dll" -ForegroundColor Green

# ─── PASO 3: Publicar DocumentIA.Functions ───────────────────────────────────
Write-Host "[3/5] Publicando DocumentIA.Functions..." -ForegroundColor Yellow
Push-Location "$root\src\backend\DocumentIA.Functions"
try {
    dotnet publish --configuration Release --output "$publishDir" --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Fallo dotnet publish" }
    Write-Host "  [OK] Output: $publishDir" -ForegroundColor Green
} finally { Pop-Location }

# ─── PASO 4: Copiar plugins/ y ajustar rutas para Azure ──────────────────────
Write-Host "[4/5] Preparando plugins para Azure..." -ForegroundColor Yellow

# Copiar la carpeta plugins/ al output de publish
Copy-Item "$root\plugins" "$publishDir\plugins" -Recurse -Force
Write-Host "  Copiado: plugins/ -> publish/functions/plugins/" -ForegroundColor Gray

# Reemplazar rutas absolutas de assemblyPath por rutas relativas en los configs copiados
# Las rutas absolutas (C:\...\plugins\SarebEnrichments.dll) no funcionan en Azure.
# CustomPlugin.cs resuelve rutas relativas contra AppContext.BaseDirectory (wwwroot en Azure).
$pluginConfigs = Get-ChildItem "$publishDir\config\tipologias\*.plugins.json" -ErrorAction SilentlyContinue
foreach ($cfg in $pluginConfigs) {
    $content = Get-Content $cfg.FullName -Raw
    $updated = $content -replace '"assemblyPath"\s*:\s*"[^"]*SarebEnrichments\.dll"',
                                  '"assemblyPath": "plugins/SarebEnrichments.dll"'
    if ($content -ne $updated) {
        Set-Content $cfg.FullName $updated -NoNewline
        Write-Host "  Ruta actualizada: $($cfg.Name)" -ForegroundColor Gray
    }
}
Write-Host "  [OK]" -ForegroundColor Green

# ─── PASO 5: Crear ZIP con forward slashes (requerido por Kudu/Linux) ─────────
Write-Host "[5/5] Creando zip con rutas Unix..." -ForegroundColor Yellow
Add-Type -Assembly System.IO.Compression.FileSystem
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$zipStream = [System.IO.File]::Create($zipPath)
$archive   = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
Get-ChildItem $publishDir -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($publishDir.Length + 1).Replace('\', '/')
    $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Fastest)
    $entryStream = $entry.Open()
    $fileStream  = [System.IO.File]::OpenRead($_.FullName)
    $fileStream.CopyTo($entryStream)
    $fileStream.Dispose()
    $entryStream.Dispose()
}
$archive.Dispose()
$zipStream.Dispose()
$zipSizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "  [OK] $zipPath ($zipSizeMb MB)" -ForegroundColor Green

# ─── DEPLOY ────────────────────────────────────────────────────────────────── 
if ($KuduUser -and $KuduPassword) {
    Write-Host "`n[DEPLOY] Desplegando via Kudu REST API..." -ForegroundColor Cyan
    Write-Host "  Destino: $kuduUrl" -ForegroundColor Gray
    Write-Host "  Tamano:  $zipSizeMb MB (puede tardar varios minutos)" -ForegroundColor Gray

    $base64Auth = [Convert]::ToBase64String(
        [System.Text.Encoding]::ASCII.GetBytes("${KuduUser}:${KuduPassword}"))
    $headers = @{ Authorization = "Basic $base64Auth" }
    $zipBytes = [System.IO.File]::ReadAllBytes($zipPath)

    try {
        $response = Invoke-RestMethod `
            -Method POST `
            -Uri $kuduUrl `
            -Headers $headers `
            -ContentType "application/zip" `
            -Body $zipBytes `
            -TimeoutSec 600

        Write-Host "`n========================================" -ForegroundColor Green
        Write-Host "  DEPLOY COMPLETADO" -ForegroundColor Green
        Write-Host "  $functionApp desplegado correctamente" -ForegroundColor Green
        Write-Host "========================================`n" -ForegroundColor Green
        Write-Host "Smoke test:" -ForegroundColor Yellow
        Write-Host "  Invoke-RestMethod -Method GET -Uri 'https://$functionApp.azurewebsites.net/api/health'" -ForegroundColor White
    } catch {
        Write-Host "`n[ERROR] Deploy fallido: $_" -ForegroundColor Red
        Write-Host "  Comprueba las credenciales Kudu o usa la opcion Cloud Shell." -ForegroundColor Yellow
        exit 1
    }

} else {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  ZIP LISTO: $zipPath ($zipSizeMb MB)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    Write-Host "`n  OPCION A - Deploy via Kudu (recomendado):" -ForegroundColor Yellow
    Write-Host "    1. En Cloud Shell, obtener credenciales:" -ForegroundColor Gray
    Write-Host "       az functionapp deployment list-publishing-credentials \" -ForegroundColor White
    Write-Host "         --resource-group SRBRGDOCSAIPROD --name $functionApp \" -ForegroundColor White
    Write-Host "         --query `"{user:publishingUserName,pass:publishingPassword}`" -o tsv" -ForegroundColor White
    Write-Host "    2. Volver a ejecutar con las credenciales:" -ForegroundColor Gray
    Write-Host "       .\scripts\deploy-manual.ps1 -KuduUser '<user>' -KuduPassword '<pass>'" -ForegroundColor White

    Write-Host "`n  OPCION B - Deploy via Cloud Shell:" -ForegroundColor Yellow
    Write-Host "    1. Sube '$zipPath' en Cloud Shell (boton de Upload)" -ForegroundColor Gray
    Write-Host "    2. En Cloud Shell ejecutar:" -ForegroundColor Gray
    Write-Host "       az functionapp deploy --resource-group SRBRGDOCSAIPROD --name $functionApp --src-path ~/functions.zip --type zip" -ForegroundColor White

    Write-Host ""
}
