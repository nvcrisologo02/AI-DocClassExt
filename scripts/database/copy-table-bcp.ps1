param(
    [string]$SourceServer    = "SRBWBDP71",
    [string]$SourceDatabase  = "ODS_DWH",
    [string]$DestinationConnectionString = "Server=127.0.0.1,1433;Database=ODS;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;",
    [string]$SchemaName      = "dbo",
    [Parameter(Mandatory)][string]$TableName,
    [int]   $RowCount        = 10000,
    [string]$WorkDirectory   = ".\artifacts\bcp",
    [switch]$ExecuteCopy,
    [switch]$ForceRecreate
)

$ErrorActionPreference = "Stop"

function Write-Step { param([string]$m) Write-Host "[STEP] $m" -ForegroundColor Cyan   }
function Write-Info { param([string]$m) Write-Host "[INFO] $m" -ForegroundColor Gray   }

function Quote-Name {
    param([string]$Name)
    return "[$($Name.Replace(']',']]'))]"
}

function New-Connection {
    param([string]$Server, [string]$Database, [string]$ConnectionString)
    $cs = if ($ConnectionString) {
        $ConnectionString
    } else {
        "Server=$Server;Database=$Database;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;Application Name=BCP-Copy"
    }
    $cn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cn.Open()
    return $cn
}

function Invoke-SqlScalar {
    param([string]$Server, [string]$Database, [string]$Query, [string]$ConnectionString)
    $cn = $null
    try {
        $cn = New-Connection -Server $Server -Database $Database -ConnectionString $ConnectionString
        $cmd = $cn.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $Query
        return $cmd.ExecuteScalar()
    } finally { if ($cn) { $cn.Dispose() } }
}

function Invoke-SqlNonQuery {
    param([string]$Server, [string]$Database, [string]$Query, [string]$ConnectionString)
    $cn = $null
    try {
        $cn = New-Connection -Server $Server -Database $Database -ConnectionString $ConnectionString
        $cmd = $cn.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $Query
        [void]$cmd.ExecuteNonQuery()
    } finally { if ($cn) { $cn.Dispose() } }
}

function Invoke-SqlTable {
    param([string]$Server, [string]$Database, [string]$Query, [string]$ConnectionString)
    $cn = $null
    try {
        $cn = New-Connection -Server $Server -Database $Database -ConnectionString $ConnectionString
        $cmd = $cn.CreateCommand(); $cmd.CommandTimeout = 0; $cmd.CommandText = $Query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)
        return ,$table
    } finally { if ($cn) { $cn.Dispose() } }
}

function Get-ColumnTypeSql {
    param($Row)
    $t  = [string]$Row.TypeName
    $ml = [int]$Row.MaxLength
    $p  = [int]$Row.PrecisionValue
    $s  = [int]$Row.ScaleValue
    switch ($t.ToLowerInvariant()) {
        "varchar"       { if ($ml -eq -1) { return "varchar(max)" }; return "varchar($ml)" }
        "char"          { return "char($ml)" }
        "varbinary"     { if ($ml -eq -1) { return "varbinary(max)" }; return "varbinary($ml)" }
        "binary"        { return "binary($ml)" }
        "nvarchar"      { if ($ml -eq -1) { return "nvarchar(max)" }; return "nvarchar($([int]($ml/2)))" }
        "nchar"         { return "nchar($([int]($ml/2)))" }
        "decimal"       { return "decimal($p,$s)" }
        "numeric"       { return "numeric($p,$s)" }
        "datetime2"     { return "datetime2($s)" }
        "datetimeoffset"{ return "datetimeoffset($s)" }
        "time"          { return "time($s)" }
        default         { return $t }
    }
}

function New-CreateTableScript {
    param([System.Data.DataTable]$Columns, [System.Data.DataTable]$PkColumns, [string]$SchemaName, [string]$TableName)
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($row in $Columns.Rows) {
        $cn  = Quote-Name ([string]$row.ColumnNameSql)
        $ct  = Get-ColumnTypeSql -Row $row
        $nul = if ([bool]$row.IsNullable) { "NULL" } else { "NOT NULL" }
        $id  = if ([bool]$row.IsIdentity) { " IDENTITY($($row.IdentitySeed),$($row.IdentityIncrement))" } else { "" }
        $df  = if ($row.DefaultDefinition -and [string]$row.DefaultDefinition -ne "") { " DEFAULT $($row.DefaultDefinition)" } else { "" }
        $lines.Add("    $cn $ct$id $nul$df")
    }
    $qt  = "$(Quote-Name $SchemaName).$(Quote-Name $TableName)"
    $ddl = "CREATE TABLE $qt (`n" + ($lines -join ",`n") + "`n);"
    if ($PkColumns.Rows.Count -gt 0) {
        $pkCols = ($PkColumns.Rows | Sort-Object KeyOrdinal | ForEach-Object {
            $dir = if ([bool]$_.IsDescending) { "DESC" } else { "ASC" }
            "$(Quote-Name ([string]$_.ColumnNameSql)) $dir"
        }) -join ", "
        $ddl += "`nALTER TABLE $qt ADD CONSTRAINT $(Quote-Name "PK_$TableName") PRIMARY KEY ($pkCols);"
    }
    return $ddl
}

# ── prerequisitos ──────────────────────────────────────────────────────────────
Write-Step "Validando prerequisitos locales"
if (-not (Get-Command bcp -ErrorAction SilentlyContinue)) {
    throw "BCP no encontrado en PATH. Instala SQL Server Command Line Utilities."
}
if (-not (Test-Path $WorkDirectory)) { New-Item $WorkDirectory -ItemType Directory | Out-Null }

$dataFile = Join-Path $WorkDirectory "$($TableName)_$($RowCount)_random.bcp"

$destBuilder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $DestinationConnectionString
$DestinationServer   = $destBuilder.DataSource
$DestinationDatabase = $destBuilder.InitialCatalog
$destUser            = $destBuilder.UserID
$destPassword        = $destBuilder.Password
if (-not $DestinationServer -or -not $DestinationDatabase) {
    throw "DestinationConnectionString debe incluir Server y Database."
}

# ── metadata origen ────────────────────────────────────────────────────────────
Write-Step "Leyendo metadata de origen y validando conectividad"

$existsQ = "SELECT COUNT(1) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='$SchemaName' AND t.name='$TableName'"
$srcExists = [int](Invoke-SqlScalar -Server $SourceServer -Database $SourceDatabase -Query $existsQ -ConnectionString "")
if ($srcExists -eq 0) { throw "Tabla origen $SourceServer.$SourceDatabase.$SchemaName.$TableName no existe." }

$colsQ = @"
SELECT c.column_id AS ColumnId, c.name AS ColumnNameSql, ty.name AS TypeName,
       c.max_length AS MaxLength, c.precision AS PrecisionValue, c.scale AS ScaleValue,
       c.is_nullable AS IsNullable, c.is_identity AS IsIdentity, c.is_computed AS IsComputed,
       ic.seed_value AS IdentitySeed, ic.increment_value AS IdentityIncrement,
       dc.definition AS DefaultDefinition
FROM sys.columns c
JOIN sys.types ty ON c.user_type_id=ty.user_type_id
JOIN sys.tables t  ON c.object_id=t.object_id
JOIN sys.schemas s ON t.schema_id=s.schema_id
LEFT JOIN sys.identity_columns ic ON c.object_id=ic.object_id AND c.column_id=ic.column_id
LEFT JOIN sys.default_constraints dc ON c.default_object_id=dc.object_id
WHERE s.name='$SchemaName' AND t.name='$TableName'
ORDER BY c.column_id
"@
$columns = Invoke-SqlTable -Server $SourceServer -Database $SourceDatabase -Query $colsQ -ConnectionString ""
if ($columns.Rows.Count -eq 0) { throw "No se pudo leer metadata de columnas." }

$computed = ($columns.Rows | Where-Object { [bool]$_.IsComputed }).Count
if ($computed -gt 0) { throw "La tabla tiene $computed columnas computadas. Ajusta el proceso con format file." }

$pkQ = @"
SELECT ic.key_ordinal AS KeyOrdinal, c.name AS ColumnNameSql, ic.is_descending_key AS IsDescending
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
JOIN sys.columns c        ON ic.object_id=c.object_id  AND ic.column_id=c.column_id
JOIN sys.tables t         ON i.object_id=t.object_id
JOIN sys.schemas s        ON t.schema_id=s.schema_id
WHERE i.is_primary_key=1 AND s.name='$SchemaName' AND t.name='$TableName'
ORDER BY ic.key_ordinal
"@
$pkColumns = Invoke-SqlTable -Server $SourceServer -Database $SourceDatabase -Query $pkQ -ConnectionString ""

Write-Info "Columnas en origen : $($columns.Rows.Count)"
Write-Info "Columnas PK        : $($pkColumns.Rows.Count)"

# ── estado destino ─────────────────────────────────────────────────────────────
Write-Step "Validando estado de tabla en destino"
$destExistsQ = "SELECT COUNT(1) FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE s.name='$SchemaName' AND t.name='$TableName'"
$destExists  = [int](Invoke-SqlScalar -Server $DestinationServer -Database $DestinationDatabase -Query $destExistsQ -ConnectionString $DestinationConnectionString)

if ($destExists -gt 0 -and -not $ForceRecreate) {
    throw "La tabla destino ya existe. Usa -ForceRecreate para recrearla."
}

$qt = "$(Quote-Name $SchemaName).$(Quote-Name $TableName)"

if (-not $ExecuteCopy) {
    Write-Step "Modo planificacion — sin ejecutar copia"
    Write-Info "Origen  : $SourceServer.$SourceDatabase.$SchemaName.$TableName"
    Write-Info "Destino : $DestinationServer.$DestinationDatabase.$SchemaName.$TableName"
    Write-Info "Filas   : $RowCount  |  Archivo BCP: $dataFile"
    exit 0
}

# ── ejecutar ───────────────────────────────────────────────────────────────────
if ($destExists -gt 0 -and $ForceRecreate) {
    Write-Step "Eliminando tabla destino (-ForceRecreate)"
    Invoke-SqlNonQuery -Server $DestinationServer -Database $DestinationDatabase -Query "DROP TABLE $qt" -ConnectionString $DestinationConnectionString
}

Write-Step "Creando estructura de tabla destino"
$ddl = New-CreateTableScript -Columns $columns -PkColumns $pkColumns -SchemaName $SchemaName -TableName $TableName
Invoke-SqlNonQuery -Server $DestinationServer -Database $DestinationDatabase -Query $ddl -ConnectionString $DestinationConnectionString

$colList   = ($columns.Rows | Sort-Object ColumnId | ForEach-Object { Quote-Name ([string]$_.ColumnNameSql) }) -join ", "
$exportSql = "SELECT TOP ($RowCount) $colList FROM $(Quote-Name $SchemaName).$(Quote-Name $TableName) ORDER BY NEWID()"

Write-Step "Exportando $RowCount filas aleatorias a $dataFile"
if (Test-Path $dataFile) { Remove-Item $dataFile -Force }

$exportArgs = "`"$exportSql`" queryout `"$dataFile`" -S `"$SourceServer`" -d `"$SourceDatabase`" -T -n"
Write-Info "bcp $exportArgs"
Invoke-Expression "bcp $exportArgs"
if (-not (Test-Path $dataFile)) { throw "No se generó el archivo BCP en $dataFile" }

Write-Step "Importando $dataFile a destino"
$importArgs = "`"$SchemaName.$TableName`" in `"$dataFile`" -S `"$DestinationServer`" -d `"$DestinationDatabase`" -U `"$destUser`" -P `"$destPassword`" -n -E -b 5000 -h `"TABLOCK`""
Write-Info "bcp $importArgs"
Invoke-Expression "bcp $importArgs"

Write-Step "Validando integridad en destino"
$count = [int](Invoke-SqlScalar -Server $DestinationServer -Database $DestinationDatabase -Query "SELECT COUNT(1) FROM $qt" -ConnectionString $DestinationConnectionString)
Write-Info "Filas en destino : $count"

if ($pkColumns.Rows.Count -gt 0) {
    $pkNames = ($pkColumns.Rows | Sort-Object KeyOrdinal | ForEach-Object { Quote-Name ([string]$_.ColumnNameSql) }) -join ", "
    $dups = [int](Invoke-SqlScalar -Server $DestinationServer -Database $DestinationDatabase `
        -Query "SELECT COUNT(1) FROM (SELECT $pkNames, COUNT(1) c FROM $qt GROUP BY $pkNames HAVING COUNT(1)>1) d" `
        -ConnectionString $DestinationConnectionString)
    Write-Info "Duplicados por PK: $dups"
}

Write-Host "[DONE] Copia de $TableName completada: $count filas." -ForegroundColor Green
