<#
.SYNOPSIS
  Genera un catalogo vivo de App Settings (claves de IConfiguration) usadas por la solucion.

.DESCRIPTION
  Escanea todos los archivos .cs en src/ buscando patrones como:
    - configuration["Foo:Bar"]
    - _configuration["Foo:Bar"]
    - context.Configuration["Foo:Bar"]
    - Configuration["Foo:Bar"]
    - GetSection("Foo:Bar")
    - GetValue<T>("Foo:Bar")
    - Environment.GetEnvironmentVariable("FOO_BAR")

  Para cada clave detectada lista los archivos donde se usa y produce un fichero
  Markdown con tabla. La salida es regenerable (idempotente).

.PARAMETER OutputPath
  Ruta del fichero Markdown a generar. Por defecto: docs/CATALOGO_APP_SETTINGS.md

.EXAMPLE
  pwsh ./scripts/generate-config/generate-appsettings-catalog.ps1
#>
[CmdletBinding()]
param(
  [string]$OutputPath = (Join-Path $PSScriptRoot "..\..\docs\CATALOGO_APP_SETTINGS.md")
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$srcRoot  = Join-Path $repoRoot 'src'

if (-not (Test-Path $srcRoot)) {
  throw "No se encontro la carpeta src/ en $repoRoot"
}

Write-Host "[catalog] Escaneando $srcRoot" -ForegroundColor Cyan

$files = Get-ChildItem -Path $srcRoot -Recurse -Filter *.cs -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

# Patrones que extraen el nombre de la clave en grupo 1
$patterns = @(
  '(?:_?[Cc]onfiguration|context\.Configuration|Configuration)\["([^"]+)"\]',
  'GetSection\("([^"]+)"\)',
  'GetValue<[^>]+>\("([^"]+)"\)',
  'Environment\.GetEnvironmentVariable\("([^"]+)"\)'
)

$results = @{}

foreach ($file in $files) {
  $content = Get-Content -Raw -LiteralPath $file.FullName
  foreach ($pat in $patterns) {
    $matches = [regex]::Matches($content, $pat)
    foreach ($m in $matches) {
      $key = $m.Groups[1].Value
      if ([string]::IsNullOrWhiteSpace($key)) { continue }
      # Filtra rutas DI obvias
      if ($key -match '^(Microsoft\.|System\.|/|\\)') { continue }
      $relPath = $file.FullName.Substring($repoRoot.Path.Length + 1).Replace('\','/')
      if (-not $results.ContainsKey($key)) {
        $results[$key] = New-Object 'System.Collections.Generic.HashSet[string]'
      }
      [void]$results[$key].Add($relPath)
    }
  }
}

Write-Host "[catalog] $($results.Count) claves detectadas" -ForegroundColor Green

# Clasificacion en categorias
function Get-Category {
  param([string]$key)
  switch -Regex ($key) {
    '^(GDC):'                         { return 'GDC (SOAP)' }
    '^(AssetResolver):'               { return 'AssetResolver Plugin' }
    '^(FunctionsAdminApi):'           { return 'Frontend Admin' }
    '^(ConnectionStrings|SqlConnect)' { return 'Base de datos' }
    '^(AzureStorage|AzureWebJobsStorage|BlobStorage)' { return 'Azure Storage' }
    '^(ApplicationInsights|APPINSIGHTS|APPLICATIONINSIGHTS)' { return 'Observabilidad' }
    '^(ASPNETCORE_)'                  { return 'Runtime ASP.NET' }
    '^(AzureWebJobs|AzureFunctions|FUNCTIONS_)' { return 'Runtime Functions' }
    '^(KeyVault|AZURE_KEY)'           { return 'Key Vault' }
    '^(ApiKey)$'                      { return 'AssetResolver Plugin' }
    '^(RunDatabaseMigrations)'        { return 'Bootstrapping' }
    default                           { return 'Otros' }
  }
}

$grouped = $results.GetEnumerator() |
  ForEach-Object {
    [pscustomobject]@{
      Key      = $_.Key
      Category = Get-Category $_.Key
      Files    = ($_.Value | Sort-Object)
    }
  } |
  Sort-Object Category, Key

# Render Markdown
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Catalogo de App Settings (vivo)")
[void]$sb.AppendLine()
[void]$sb.AppendLine("> Generado automaticamente por ``scripts/generate-config/generate-appsettings-catalog.ps1``  ")
[void]$sb.AppendLine("> Fecha: " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + "  ")
[void]$sb.AppendLine('> Fuente: escaneo regex sobre `src/**/*.cs` (patrones `IConfiguration["..."]`, `GetSection`, `GetValue<T>`, `Environment.GetEnvironmentVariable`)')
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Como regenerar")
[void]$sb.AppendLine()
[void]$sb.AppendLine('```powershell')
[void]$sb.AppendLine('pwsh ./scripts/generate-config/generate-appsettings-catalog.ps1')
[void]$sb.AppendLine('```')
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Resumen por categoria")
[void]$sb.AppendLine()
[void]$sb.AppendLine("| Categoria | Numero de claves |")
[void]$sb.AppendLine("|---|---:|")
$grouped | Group-Object Category | Sort-Object Name | ForEach-Object {
  [void]$sb.AppendLine("| $($_.Name) | $($_.Count) |")
}
[void]$sb.AppendLine("| **TOTAL** | **$($grouped.Count)** |")
[void]$sb.AppendLine()

$grouped | Group-Object Category | Sort-Object Name | ForEach-Object {
  [void]$sb.AppendLine("## " + $_.Name)
  [void]$sb.AppendLine()
  [void]$sb.AppendLine("| Clave | Usada en |")
  [void]$sb.AppendLine("|---|---|")
  foreach ($entry in ($_.Group | Sort-Object Key)) {
    $links = ($entry.Files | ForEach-Object { '[' + $_ + '](' + $_ + ')' }) -join '<br>'
    [void]$sb.AppendLine("| ``$($entry.Key)`` | $links |")
  }
  [void]$sb.AppendLine()
}

[void]$sb.AppendLine("---")
[void]$sb.AppendLine()
[void]$sb.AppendLine("## Notas")
[void]$sb.AppendLine()
[void]$sb.AppendLine("- Las claves con prefijo doble `:` indican secciones jerarquicas (ej. `GDC:Endpoint`).")
[void]$sb.AppendLine("- Para ver el valor concreto en cada entorno consultar Azure Portal -> Function App `srbappprodocai` -> Configuration, o el `local.settings.json` local.")
[void]$sb.AppendLine("- Los secretos referenciados via `@Microsoft.KeyVault(...)` se documentan en `docs/INFRAESTRUCTURA_AZURE.md`.")
[void]$sb.AppendLine("- Ver tambien `.env.example` (raiz del repo) y `MANUAL_HEALTHCHECK.md` para settings con probe especifico.")

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
$sb.ToString() | Out-File -LiteralPath $OutputPath -Encoding utf8

Write-Host "[catalog] Generado $OutputPath" -ForegroundColor Green
