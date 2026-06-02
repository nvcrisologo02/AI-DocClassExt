# Script para consultar tipologias con descripciones de TDN1 y TDN2

Add-Type -AssemblyName "System.Data"

# Leer configuracion
$localSettingsPath = "c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions\local.settings.json"
$localSettings = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
$connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'

Write-Host "Conectando a BD..." -ForegroundColor Green
Write-Host ""

try {
    $conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $conn.Open()
    Write-Host "OK: Conexion exitosa" -ForegroundColor Green
    Write-Host ""

    # Query: Obtener tipologias con descripciones TDN1 y TDN2
    $query = @"
SELECT 
    t.[Codigo],
    t.[Nombre],
    t.[Estado],
    JSON_VALUE(t.[ConfiguracionJson], '$.gptDescripcion') AS gptDescripcion,
    JSON_VALUE(t.[ConfiguracionJson], '$.classification.tdn1') AS Tdn1Codigo,
    tdn1.[Nombre] AS Tdn1Nombre,
    tdn1.[Descripcion] AS Tdn1Descripcion,
    JSON_VALUE(t.[ConfiguracionJson], '$.classification.tdn2') AS Tdn2Codigo,
    tdn2.[Nombre] AS Tdn2Nombre,
    tdn2.[Descripcion] AS Tdn2Descripcion
FROM [Tipologias] t
LEFT JOIN [CatalogoTdn1] tdn1 ON tdn1.[Codigo] = JSON_VALUE(t.[ConfiguracionJson], '$.classification.tdn1')
LEFT JOIN [CatalogoTdn2] tdn2 ON tdn2.[Codigo] = JSON_VALUE(t.[ConfiguracionJson], '$.classification.tdn2')
ORDER BY t.[Codigo]
"@
    
    Write-Host "Ejecutando consulta..." -ForegroundColor Green
    Write-Host ""
    
    $cmd = New-Object System.Data.SqlClient.SqlCommand($query, $conn)
    $cmd.CommandTimeout = 30
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dataset = New-Object System.Data.DataSet
    [void]$adapter.Fill($dataset)
    
    $results = $dataset.Tables[0]
    
    if ($results -and $results.Rows.Count -gt 0) {
        Write-Host "Encontradas $($results.Rows.Count) tipologias:`n"
        
        foreach ($row in $results.Rows) {
            Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
            Write-Host "Codigo          : $($row['Codigo'])"
            Write-Host "Nombre          : $($row['Nombre'])"
            Write-Host "Estado          : $($row['Estado'])"
            Write-Host "GPT Descripción : $($row['gptDescripcion'])"
            Write-Host ""
            Write-Host "TDN1 Codigo     : $($row['Tdn1Codigo'])"
            Write-Host "TDN1 Nombre     : $($row['Tdn1Nombre'])"
            Write-Host "TDN1 Descripción: $($row['Tdn1Descripcion'])"
            Write-Host ""
            Write-Host "TDN2 Codigo     : $($row['Tdn2Codigo'])"
            Write-Host "TDN2 Nombre     : $($row['Tdn2Nombre'])"
            Write-Host "TDN2 Descripción: $($row['Tdn2Descripcion'])"
            Write-Host ""
        }
    } else {
        Write-Host "No se encontraron tipologias" -ForegroundColor Yellow
    }
    
    $conn.Close()
    Write-Host "OK: Consulta completada" -ForegroundColor Green
    
} catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
}
