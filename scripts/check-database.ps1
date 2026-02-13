# Script para verificar qué se guardó en la BD después de ejecutar test

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  DIAGNOSTICO: DATOS EN BASE DE DATOS" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$server = "localhost,1433"
$database = "DocumentIA"
$user = "sa"
$password = "COMPLETAR_SQL_PASSWORD"

# 1. Verificar conectividad
Write-Host "[1] Verificando conectividad a SQL Server..." -ForegroundColor Yellow
try {
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = "Server=$server;Database=$database;User Id=$user;Password=$password;TrustServerCertificate=True;Connection Timeout=5;"
    $connection.Open()
    $connection.Close()
    Write-Host "  [OK] Conectado a SQL Server" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] No se puede conectar: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "`n  Soluciones:" -ForegroundColor Yellow
    Write-Host "    1. Verifica que SQL Server esté corriendo" -ForegroundColor Yellow
    Write-Host "    2. Ejecuta: Test-NetConnection localhost -Port 1433" -ForegroundColor Yellow
    Write-Host "    3. Verifica credenciales en appsettings.json" -ForegroundColor Yellow
    exit 1
}

# 2. Contar documentos
Write-Host "`n[2] Contando documentos en BD..." -ForegroundColor Yellow
try {
    $result = sqlcmd -S $server -U $user -P $password -d $database -Q "SELECT COUNT(*) as Total FROM Documentos" -h -1 | Select-Object -First 1
    $count = [int]$result.Trim()
    
    if ($count -eq 0) {
        Write-Host "  [ADVERTENCIA] No hay documentos en la BD" -ForegroundColor Yellow
        Write-Host "  Esto significa que:" -ForegroundColor Gray
        Write-Host "    - No se ejecutó PersistirActivity" -ForegroundColor Gray
        Write-Host "    - O falló durante el guardado" -ForegroundColor Gray
    } else {
        Write-Host "  [OK] $count documento(s) en Base de Datos" -ForegroundColor Green
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 3. Mostrar últimos documentos
Write-Host "`n[3] Últimos documentos procesados:" -ForegroundColor Yellow
try {
    $query = @"
    SELECT TOP 5
        d.Id,
        d.NombreArchivo,
        d.Tipologia,
        d.Estado,
        CONVERT(VARCHAR(5), d.ConfianzaGlobal * 100) + '%' as ConfianzaGlobal,
        d.FechaProceso as 'Fecha Proceso',
        d.FechaCreacion as 'Fecha Creacion'
    FROM Documentos d
    ORDER BY d.FechaCreacion DESC;
"@
    
    sqlcmd -S $server -U $user -P $password -d $database -Q $query -h -1 | ForEach-Object {
        if ($_.Trim()) { Write-Host "  $_" -ForegroundColor Cyan }
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 4. Mostrar resultados de procesamiento
Write-Host "`n[4] Datos de procesamiento por documento:" -ForegroundColor Yellow
try {
    $query = @"
    SELECT TOP 3
        d.Id,
        d.NombreArchivo,
        r.ModeloClasificacion,
        CONVERT(VARCHAR(5), r.ConfianzaClasificacion * 100) + '%' as 'Confianza Clasificación',
        r.ModeloExtraccion,
        r.ModuloIntegracion,
        r.ResultadoIntegracion
    FROM Documentos d
    LEFT JOIN ResultadosProcesamiento r ON d.Id = r.DocumentoId
    ORDER BY d.FechaCreacion DESC;
"@
    
    sqlcmd -S $server -U $user -P $password -d $database -Q $query -h -1 | ForEach-Object {
        if ($_.Trim()) { Write-Host "  $_" -ForegroundColor Cyan }
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 5. Mostrar validaciones del último documento
Write-Host "`n[5] Validaciones del último documento:" -ForegroundColor Yellow
try {
    $query = @"
    SELECT TOP 1
        d.NombreArchivo,
        r.ValidacionesJson
    FROM Documentos d
    LEFT JOIN ResultadosProcesamiento r ON d.Id = r.DocumentoId
    ORDER BY d.FechaCreacion DESC;
"@
    
    $result = sqlcmd -S $server -U $user -P $password -d $database -Q $query -h -1 -w 200
    $result | ForEach-Object {
        if ($_.Trim()) { Write-Host "  $_" }
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 6. Mostrar auditoría
Write-Host "`n[6] Auditoría (últimas acciones):" -ForegroundColor Yellow
try {
    $query = @"
    SELECT TOP 5
        DocumentoId,
        Accion,
        Nivel,
        Mensaje,
        CONVERT(VARCHAR(19), FechaHora, 120) as 'Fecha/Hora'
    FROM Auditorias
    ORDER BY FechaHora DESC;
"@
    
    sqlcmd -S $server -U $user -P $password -d $database -Q $query -h -1 | ForEach-Object {
        if ($_.Trim()) { Write-Host "  $_" -ForegroundColor Cyan }
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 7. Verificar tabla de datos extraídos
Write-Host "`n[7] Datos extraídos (primeros 500 caracteres):" -ForegroundColor Yellow
try {
    $query = @"
    SELECT TOP 1
        d.NombreArchivo,
        SUBSTRING(r.DatosExtraidosJson, 1, 500) + '...' as 'Datos Extraídos (primeros 500 chars)'
    FROM Documentos d
    LEFT JOIN ResultadosProcesamiento r ON d.Id = r.DocumentoId
    WHERE r.DatosExtraidosJson IS NOT NULL
    ORDER BY d.FechaCreacion DESC;
"@
    
    $result = sqlcmd -S $server -U $user -P $password -d $database -Q $query -h -1 -w 200
    $result | ForEach-Object {
        if ($_.Trim()) { Write-Host "  $_" }
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 8. Resumen de estado de documentos
Write-Host "`n[8] Resumen de estados:" -ForegroundColor Yellow
try {
    $query = @"
    SELECT 
        Estado,
        COUNT(*) as Cantidad
    FROM Documentos
    GROUP BY Estado;
"@
    
    sqlcmd -S $server -U $user -P $password -d $database -Q $query -h -1 | ForEach-Object {
        if ($_.Trim()) { Write-Host "  $_" -ForegroundColor Cyan }
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

# 9. Verificar si no hay datos guardados
Write-Host "`n[9] Diagnóstico:" -ForegroundColor Yellow
try {
    $documentoCount = sqlcmd -S $server -U $user -P $password -d $database -Q "SELECT COUNT(*) FROM Documentos" -h -1 | Select-Object -First 1 | ForEach-Object { [int]$_.Trim() }
    
    if ($documentoCount -eq 0) {
        Write-Host "  [!] NO HAY DATOS EN LA BASE DE DATOS" -ForegroundColor Red
        Write-Host "`n  Causas posibles:" -ForegroundColor Yellow
        Write-Host "    1. PersistirActivity no se executó (verifica logs de func host start)" -ForegroundColor Yellow
        Write-Host "    2. Error en validación/clasificación/extracción (antes de persistencia)" -ForegroundColor Yellow
        Write-Host "    3. Error de conexión a BD durante guardado" -ForegroundColor Yellow
        Write-Host "    4. La orquestación no llegó al paso 7 (Persistencia)" -ForegroundColor Yellow
        
        Write-Host "`n  Próximos pasos:" -ForegroundColor Cyan
        Write-Host "    1. Ejecuta: func host start" -ForegroundColor Gray
        Write-Host "    2. En otra terminal: .\test-multi-plugin.ps1" -ForegroundColor Gray
        Write-Host "    3. Mira los logs de func host start" -ForegroundColor Gray
        Write-Host "    4. Busca errores antes de 'Persistiendo resultado'" -ForegroundColor Gray
    } else {
        Write-Host "  [OK] Se están guardando datos correctamente" -ForegroundColor Green
        Write-Host "`n  Total de documentos: $documentoCount" -ForegroundColor Green
    }
} catch {
    Write-Host "  [ERROR] $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  FIN DEL DIAGNOSTICO" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
