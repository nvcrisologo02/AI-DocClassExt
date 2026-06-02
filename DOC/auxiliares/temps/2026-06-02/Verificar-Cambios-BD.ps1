# Verificar que los cambios se aplicaron a la BD

$localSettingsPath = ".\src\backend\DocumentIA.Functions\local.settings.json"
$localSettings = Get-Content $localSettingsPath -Raw | ConvertFrom-Json
$connectionString = $localSettings.Values.'ConnectionStrings:DocumentIA'

$conn = New-Object System.Data.SqlClient.SqlConnection
$conn.ConnectionString = $connectionString
$conn.Open()

Write-Host "VERIFICACION: BD actualizada" -ForegroundColor Cyan
Write-Host ""

# Verificar que ConfiguracionJson tiene tipologiaNombre
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT TOP 10 
    Id, 
    Codigo,
    Nombre,
    ConfiguracionJson
FROM dbo.Tipologias
WHERE Id >= 2019 AND Id <= 2025
ORDER BY Id
"@

$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    $id = $reader['Id']
    $codigo = $reader['Codigo']
    $nombre = $reader['Nombre']
    $json = $reader['ConfiguracionJson'].ToString() | ConvertFrom-Json
    
    $tipoNombre = $json.tipologiaNombre
    $gptDesc = if ($json.gptdescripcion.Length -gt 80) { $json.gptdescripcion.Substring(0, 80) + "..." } else { $json.gptdescripcion }
    
    Write-Host "ID=$id [$codigo]" -ForegroundColor Green
    Write-Host "  tipologiaNombre: $tipoNombre" -ForegroundColor Gray
    Write-Host "  gptdescripcion: $gptDesc" -ForegroundColor Gray
    Write-Host ""
}
$reader.Close()

# Contar cuantas tipologías tienen tipologiaNombre
$cmd = $conn.CreateCommand()
$cmd.CommandText = @"
SELECT COUNT(*) as total
FROM dbo.Tipologias t
CROSS APPLY OPENJSON(t.ConfiguracionJson) 
WITH (tipologiaNombre NVARCHAR(MAX))
"@

$reader = $cmd.ExecuteReader()
if ($reader.Read()) {
    $count = $reader['total']
    Write-Host "RESULTADO: $count tipologías tienen tipologiaNombre en ConfiguracionJson" -ForegroundColor Green
}
$reader.Close()

$conn.Close()
