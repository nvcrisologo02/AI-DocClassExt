# Script simple para verificar BD y obtener tipologias

Add-Type -AssemblyName "System.Data"

# Leer configuracion
$localSettingsPath = "c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions\local.settings.json"
$localSettings = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
$connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'

Write-Host "Conectando a BD..." -ForegroundColor Green
Write-Host "Servidor: $(($connectionString -split ';')[0])"
Write-Host ""

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()
    Write-Host "OK: Conexion exitosa" -ForegroundColor Green
    
    # Query simple: obtener tipologias ESCR-01, SERE-24
    $query = @"
SELECT TOP 10 Codigo, Nombre, ConfiguracionJson 
FROM Tipologias 
WHERE Codigo IN ('ESCR-01', 'SERE-24', 'escr.01', 'sere.24')
"@
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $conn)
    $cmd.CommandTimeout = 10
    $reader = $cmd.ExecuteReader()
    
    $count = 0
    while ($reader.Read()) {
        $count++
        Write-Host ""
        Write-Host "=== Tipologia $count ===" -ForegroundColor Yellow
        Write-Host "Codigo: $($reader['Codigo'])"
        Write-Host "Nombre: $($reader['Nombre'])"
        Write-Host "ConfigJSON: $($reader['ConfiguracionJson'])"
    }
    
    if ($count -eq 0) {
        Write-Host "No se encontraron tipologias con esos codigos" -ForegroundColor Yellow
    }
    
    $reader.Close()
    $conn.Close()
    Write-Host "`nOK: Consulta completada" -ForegroundColor Green
    
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
