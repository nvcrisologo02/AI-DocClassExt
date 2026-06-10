<#
.SYNOPSIS
  Convert Markdown to PDF using the best available tool on the system.

.DESCRIPTION
  Prefer `md-to-pdf` (Puppeteer) via `npx` for fidelity and minimal install.
  Falls back to `pandoc` + `wkhtmltopdf` or `pandoc` + `pdflatex` if available.

.USAGE
  pwsh ./scripts/convert-md-to-pdf.ps1 -InputPath input.md [-OutputPath out.pdf]

#>

param(
  [Parameter(Mandatory=$true, Position=0)]
  [string]$InputPath,
  [string]$OutputPath
)

function CommandExists($cmd) {
  return $null -ne (Get-Command $cmd -ErrorAction SilentlyContinue)
}

try {
  $inputResolved = Resolve-Path -LiteralPath $InputPath -ErrorAction Stop
  $inputPath = $inputResolved.Path
} catch {
  Write-Error "No se encontró el fichero de entrada: $InputPath"
  exit 2
}

if (-not $OutputPath) {
  $OutputPath = [System.IO.Path]::ChangeExtension($inputPath, '.pdf')
} else {
  # If OutputPath is not an absolute path, make it relative to current location
  if (-not ([System.IO.Path]::IsPathRooted($OutputPath))) {
    $OutputPath = Join-Path (Get-Location) $OutputPath
  }
}

Write-Host "Convirtiendo:`n  Entrada: $inputPath`n  Salida:  $OutputPath" -ForegroundColor Cyan

# Try preferred: md-to-pdf via npx (Puppeteer/Chrome)
if (CommandExists('npx')) {
  Write-Host "Intentando npx md-to-pdf (Puppeteer)..." -ForegroundColor Cyan
  $npArgs = @('md-to-pdf', $inputPath, '--output', $OutputPath)
  & npx --yes @npArgs
  if ($LASTEXITCODE -eq 0) { Write-Host "PDF generado: $OutputPath" -ForegroundColor Green; exit 0 }
  Write-Warning "npx md-to-pdf falló (exit $LASTEXITCODE). Probando alternativas..."
}

# Fallback: pandoc + wkhtmltopdf / pdflatex
if (CommandExists('pandoc')) {
  if (CommandExists('wkhtmltopdf')) {
    Write-Host "Usando pandoc + wkhtmltopdf..." -ForegroundColor Cyan
    & pandoc $inputPath -o $OutputPath --pdf-engine=wkhtmltopdf
    if ($LASTEXITCODE -eq 0) { Write-Host "PDF generado: $OutputPath" -ForegroundColor Green; exit 0 }
  } elseif (CommandExists('pdflatex')) {
    Write-Host "Usando pandoc + pdflatex (LaTeX)..." -ForegroundColor Cyan
    & pandoc $inputPath -o $OutputPath --pdf-engine=pdflatex
    if ($LASTEXITCODE -eq 0) { Write-Host "PDF generado: $OutputPath" -ForegroundColor Green; exit 0 }
  } else {
    Write-Host "Pandoc disponible, pero no se encontró motor PDF (wkhtmltopdf/pdflatex)." -ForegroundColor Yellow
    Write-Host "Intentando pandoc sin engine específico..." -ForegroundColor Yellow
    & pandoc $inputPath -o $OutputPath
    if ($LASTEXITCODE -eq 0) { Write-Host "PDF generado: $OutputPath" -ForegroundColor Green; exit 0 }
  }
}

Write-Error "No se encontró una herramienta válida para convertir MD→PDF (npx/pandoc)."
Write-Host "Sugerencias de instalación:" -ForegroundColor Yellow
Write-Host "  - Instalar Node.js y ejecutar: npm i -g md-to-pdf  (o usar npx)" -ForegroundColor Yellow
Write-Host "  - Instalar Pandoc: https://pandoc.org/installing.html" -ForegroundColor Yellow
Write-Host "  - Instalar wkhtmltopdf o una distribución TeX (MikTeX/TeX Live) para Pandoc" -ForegroundColor Yellow

exit 3
