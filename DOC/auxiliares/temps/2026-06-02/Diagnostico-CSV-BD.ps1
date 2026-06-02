# Script de diagnóstico para comparar IDs del CSV con IDs de BD

$csvPath = ".\docs\auxiliares\tiposgpt.csv"
$localSettingsPath = ".\src\backend\DocumentIA.Functions\local.settings.json"

Write-Host "DIAGNOSTICO: CSV vs BD IDs" -ForegroundColor Cyan
Write-Host ""

# Leer CSV
Write-Host "1. Leyendo CSV..." -ForegroundColor Green
$csvData = Import-Csv -Path $csvPath -Encoding UTF8 -Delimiter ";"
Write-Host "   Registros CSV: $($csvData.Count)" -ForegroundColor Green
Write-Host "   Primeros IDs del CSV:" -ForegroundColor Gray
$csvData | Select-Object -First 5 -Property Id | ForEach-Object { Write-Host "     - $($_.Id) (tipo: $($_.Id.GetType().Name))" }

# Conectar a BD
Write-Host ""
Write-Host "2. Conectando a BD..." -ForegroundColor Green
$localSettings = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
$connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = $connectionString
$conn.Open()

# Leer IDs de BD
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT TOP 10 Id, Codigo, Nombre FROM dbo.Tipologias ORDER BY Id"
$reader = $cmd.ExecuteReader()

Write-Host "   Primeros IDs de BD:" -ForegroundColor Gray
while ($reader.Read()) {
    Write-Host "     - $($reader['Id']) [$($reader['Codigo'])] $($reader['Nombre'])"
}
$reader.Close()

# Comparar
Write-Host ""
Write-Host "3. Intentando match..." -ForegroundColor Green

$csvIdsList = $csvData | ForEach-Object { $_.Id.ToString().Trim() } | Select-Object -Unique

$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id FROM dbo.Tipologias"
$reader = $cmd.ExecuteReader()
$bdIds = @()
while ($reader.Read()) {
    $bdIds += $reader['Id']
}
$reader.Close()

Write-Host "   IDs en CSV: $($csvIdsList.Count)" -ForegroundColor Green
Write-Host "   IDs en BD: $($bdIds.Count)" -ForegroundColor Green

$matches = $csvIdsList | Where-Object { $_ -in $bdIds }
Write-Host "   Matches encontrados: $($matches.Count)" -ForegroundColor Green

if ($matches.Count -eq 0) {
    Write-Host ""
    Write-Host "   PROBLEMA: No hay coincidencias entre CSV y BD" -ForegroundColor Red
    Write-Host "   Ejemplo IDs CSV: $($csvIdsList[0..4] -join ', ')" -ForegroundColor Yellow
    Write-Host "   Ejemplo IDs BD: $($bdIds[0..4] -join ', ')" -ForegroundColor Yellow
}

$conn.Close()
Write-Host ""
