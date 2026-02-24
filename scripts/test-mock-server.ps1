# Test del mock server de enriquecimiento

param(
    [int]$Port = 8080
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TEST MOCK ENRICHMENT SERVER" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$endpoint = "http://localhost:$Port"

# 1. Health check
Write-Host "[1/3] Health Check..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "$endpoint/health" -Method Get
    Write-Host "  [OK] Servidor activo" -ForegroundColor Green
    Write-Host "  Version: $($health.version)" -ForegroundColor Gray
} catch {
    Write-Host "  [ERROR] Servidor no responde" -ForegroundColor Red
    Write-Host "  Asegurate de haber ejecutado la tarea: start (mock servers)" -ForegroundColor Yellow
    exit 1
}

# 2. Test con datos de Nota Simple
Write-Host "`n[2/3] Test enriquecimiento Nota Simple..." -ForegroundColor Yellow

$testData = @{
    tipologia = "notasimple1.3"
    documentoId = "TEST-NS-001"
    datosExtraidos = @{
        FincaRegistral = "12345"
        RegistroPropiedad = "Madrid Numero 1"
        Titular = "Juan Perez Gomez"
        NIF = "12345678A"
        ReferenciaCatastral = "1234567AB1234S0001ZX"
        superficie = 85.5
        Direccion = "Calle Mayor 123, Madrid"
    }
    metadata = @{
        correlationId = "test-123"
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $testData -ContentType "application/json"
    
    Write-Host "  [OK] Enriquecimiento exitoso" -ForegroundColor Green
    Write-Host "`n  Datos devueltos:" -ForegroundColor White
    $response | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor Gray
    
    # Verificar campos enriquecidos
    $camposEsperados = @("IdActivo", "IdProyecto", "EstadoActivo", "FechaEnriquecimiento")
    $faltantes = @()
    
    foreach ($campo in $camposEsperados) {
        if (-not $response.PSObject.Properties.Name.Contains($campo)) {
            $faltantes += $campo
        }
    }
    
    if ($faltantes.Count -eq 0) {
        Write-Host "`n  [OK] Todos los campos esperados presentes" -ForegroundColor Green
    } else {
        Write-Host "`n  [ADVERTENCIA] Campos faltantes: $($faltantes -join ', ')" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "  [ERROR] Fallo el enriquecimiento" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Test con datos de Tasacion
Write-Host "`n[3/3] Test enriquecimiento Tasacion..." -ForegroundColor Yellow

$testTasacion = @{
    tipologia = "tasacion"
    documentoId = "TEST-TAS-001"
    datosExtraidos = @{
        Titular = "Maria Lopez"
        NIF = "87654321B"
        ValorTasado = 350000
        FechaTasacion = "2024-02-12"
    }
} | ConvertTo-Json -Depth 10

try {
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body $testTasacion -ContentType "application/json"
    
    Write-Host "  [OK] Enriquecimiento exitoso" -ForegroundColor Green
    Write-Host "  IdActivo generado: $($response.IdActivo)" -ForegroundColor Gray
    Write-Host "  Rango valor: $($response.RangoValor)" -ForegroundColor Gray
    
} catch {
    Write-Host "  [ERROR] Fallo el enriquecimiento" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  TEST COMPLETADO" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan
