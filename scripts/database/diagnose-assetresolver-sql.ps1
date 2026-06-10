[CmdletBinding()]
param(
    [string]$AppSettingsPath = "src/plugins/DocumentIA.AssetResolver/appsettings.json",
    [string]$ConnectionStringName = "AssetResolverDb",
    [string]$ConnectionString,
    [string]$FallbackKeyVaultReference = "@Microsoft.KeyVault(VaultName=srbkvprodocai;SecretName=user-ods-dwh)",
    [string]$OutputDir,
    [string]$Idufir = "30035000403354",
    [string]$RefCatastral = "8337302XG7683N0410HD",
    [string]$CodigoPostal = "30202",
    [int]$TopCachedPlans = 20,
    [int]$MaxRowsPerExact = 500,
    [int]$CommandTimeoutSec = 120,
    [switch]$SkipStats,
    [switch]$SkipEstimatedPlans,
    [switch]$SkipCachedPlans,
    [switch]$SkipMissingIndexes
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Message)
    Write-Host "`n== $Message ==" -ForegroundColor Cyan
}

function Resolve-PathSafe {
    param([string]$Path)
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    $fromCwd = Join-Path (Get-Location) $Path
    if (Test-Path -LiteralPath $fromCwd) {
        return $fromCwd
    }

    $repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
    $fromRepoRoot = Join-Path $repoRoot $Path
    if (Test-Path -LiteralPath $fromRepoRoot) {
        return $fromRepoRoot
    }

    return $fromRepoRoot
}

function Resolve-ConnectionStringFromAppSettings {
    param(
        [string]$Path,
        [string]$Name
    )

    $fullPath = Resolve-PathSafe -Path $Path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        throw "No se encontro appsettings: $fullPath"
    }

    $json = Get-Content -Raw -LiteralPath $fullPath | ConvertFrom-Json
    $value = $json.ConnectionStrings.$Name
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "No existe ConnectionStrings.$Name en $fullPath"
    }

    return [string]$value
}

function Resolve-RawConnectionString {
    if (-not [string]::IsNullOrWhiteSpace($ConnectionString)) {
        return $ConnectionString.Trim()
    }

    try {
        return (Resolve-ConnectionStringFromAppSettings -Path $AppSettingsPath -Name $ConnectionStringName)
    }
    catch {
        Write-Warning "No se pudo leer appsettings. Se usa FallbackKeyVaultReference. Detalle: $($_.Exception.Message)"
        if ([string]::IsNullOrWhiteSpace($FallbackKeyVaultReference)) {
            throw
        }
        return $FallbackKeyVaultReference.Trim()
    }
}

function Parse-KeyVaultReference {
    param([string]$InputValue)

    $pattern = '^@Microsoft\.KeyVault\(VaultName=(?<vault>[^;\)]+);SecretName=(?<secret>[^\)]+)\)$'
    $m = [System.Text.RegularExpressions.Regex]::Match($InputValue.Trim(), $pattern)
    if (-not $m.Success) {
        return $null
    }

    return [pscustomobject]@{
        VaultName  = $m.Groups['vault'].Value
        SecretName = $m.Groups['secret'].Value
    }
}

function Resolve-KeyVaultSecretValue {
    param(
        [string]$VaultName,
        [string]$SecretName
    )

    $az = Get-Command az -ErrorAction SilentlyContinue
    if (-not $az) {
        throw "No se encontro Azure CLI (az). Instala az o pasa -ConnectionString con valor directo."
    }

    $value = az keyvault secret show --vault-name $VaultName --name $SecretName --query value -o tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($value)) {
        throw "No se pudo leer el secreto '$SecretName' del vault '$VaultName'. Verifica login/permiso/red."
    }

    return $value.Trim()
}

function Ensure-OutputDir {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $Path = Join-Path (Get-Location) ("artifacts/sql-diagnostics/assetresolver-pro-" + $stamp)
    }

    $full = Resolve-PathSafe -Path $Path
    New-Item -ItemType Directory -Force -Path $full | Out-Null
    return $full
}

function New-SqlConnection {
    param(
        [string]$ConnectionStringValue,
        [int]$TimeoutSec
    )

    $cn = New-Object System.Data.SqlClient.SqlConnection $ConnectionStringValue
    $cn.FireInfoMessageEventOnUserErrors = $true
    $cn.Open()

    $cmd = $cn.CreateCommand()
    $cmd.CommandTimeout = $TimeoutSec
    $cmd.CommandText = "SET NOCOUNT ON;"
    $cmd.ExecuteNonQuery() | Out-Null

    return $cn
}

function Invoke-SqlDataTable {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Query,
        [hashtable]$Parameters,
        [int]$TimeoutSec
    )

    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = $TimeoutSec
    $cmd.CommandText = $Query

    if ($Parameters) {
        foreach ($k in $Parameters.Keys) {
            $p = $cmd.Parameters.Add("@$k", [System.Data.SqlDbType]::NVarChar, 4000)
            $p.Value = [string]$Parameters[$k]
        }
    }

    $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $dt = New-Object System.Data.DataTable
    [void]$da.Fill($dt)
    # Important: return DataTable as a single object (avoid pipeline enumeration into DataRow)
    return ,$dt
}

function Invoke-SqlWithStatistics {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Query,
        [hashtable]$Parameters,
        [int]$TimeoutSec
    )

    $script:__diagStatsMessages = New-Object System.Collections.Generic.List[string]
    $handler = [System.Data.SqlClient.SqlInfoMessageEventHandler]{
        param($sender, $event)
        foreach ($msg in ($event.Message -split "`r?`n")) {
            if (-not [string]::IsNullOrWhiteSpace($msg)) {
                $script:__diagStatsMessages.Add($msg)
            }
        }
    }

    $Connection.add_InfoMessage($handler)
    try {
        $cmd = $Connection.CreateCommand()
        $cmd.CommandTimeout = $TimeoutSec
        $cmd.CommandText = "SET STATISTICS IO ON; SET STATISTICS TIME ON; " + $Query + " SET STATISTICS TIME OFF; SET STATISTICS IO OFF;"

        if ($Parameters) {
            foreach ($k in $Parameters.Keys) {
                $p = $cmd.Parameters.Add("@$k", [System.Data.SqlDbType]::NVarChar, 4000)
                $p.Value = [string]$Parameters[$k]
            }
        }

        $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
        $ds = New-Object System.Data.DataSet
        [void]$da.Fill($ds)

        return [pscustomobject]@{
            DataSet = $ds
            Messages = @($script:__diagStatsMessages)
        }
    }
    finally {
        $Connection.remove_InfoMessage($handler)
    }
}

function Save-DataTableCsv {
    param(
        [System.Data.DataTable]$Table,
        [string]$Path
    )

    if ($null -eq $Table -or $Table.Rows.Count -eq 0) {
        "" | Set-Content -LiteralPath $Path -Encoding UTF8
        return
    }

    $rows = foreach ($r in $Table.Rows) {
        $obj = [ordered]@{}
        foreach ($c in $Table.Columns) {
            $obj[$c.ColumnName] = $r[$c.ColumnName]
        }
        [pscustomobject]$obj
    }

    $rows | Export-Csv -LiteralPath $Path -NoTypeInformation -Encoding UTF8
}

# 1) Resolver cadena de conexion
Write-Section "Resolviendo cadena de conexion"
$rawConnectionString = Resolve-RawConnectionString

$keyVaultRef = Parse-KeyVaultReference -InputValue $rawConnectionString
$resolvedConnectionString = if ($null -ne $keyVaultRef) {
    Write-Host "Se detecto referencia Key Vault: vault=$($keyVaultRef.VaultName), secret=$($keyVaultRef.SecretName)"
    Resolve-KeyVaultSecretValue -VaultName $keyVaultRef.VaultName -SecretName $keyVaultRef.SecretName
} else {
    $rawConnectionString
}

# 2) Preparar salida
$outputRoot = Ensure-OutputDir -Path $OutputDir
Write-Host "Salida: $outputRoot"

# 3) Ejecutar diagnostico
Write-Section "Conectando a SQL"
$cn = $null
try {
    $cn = New-SqlConnection -ConnectionStringValue $resolvedConnectionString -TimeoutSec $CommandTimeoutSec

    # Metadata basica
    Write-Section "Metadata de base"
    $meta = Invoke-SqlDataTable -Connection $cn -TimeoutSec $CommandTimeoutSec -Parameters @{} -Query @"
SELECT
    DB_NAME() AS [DatabaseName],
    @@SERVERNAME AS [ServerName],
    SYSDATETIMEOFFSET() AS [CapturedAt],
    @@VERSION AS [SqlVersion];
"@
    Save-DataTableCsv -Table $meta -Path (Join-Path $outputRoot "metadata.csv")

    # Stats IO/TIME con consultas representativas del plugin
    if (-not $SkipStats) {
        Write-Section "STATISTICS IO/TIME"
        $statsResult = Invoke-SqlWithStatistics -Connection $cn -TimeoutSec $CommandTimeoutSec -Parameters @{
            Idufir = $Idufir
            RefCat = $RefCatastral
            CodigoPostal = $CodigoPostal
            MaxRows = [string]$MaxRowsPerExact
        } -Query @"
DECLARE @MaxRowsInt INT = TRY_CAST(@MaxRows AS INT);
IF @MaxRowsInt IS NULL OR @MaxRowsInt <= 0 SET @MaxRowsInt = 500;

SELECT TOP (@MaxRowsInt)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT, ID_IDUFIR, ID_REF_CATAST, NUM_COD_POSTAL
FROM dbo.DM_POSICION_AAII_TB
WHERE ID_IDUFIR = @Idufir
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (@MaxRowsInt)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT, ID_IDUFIR, ID_REF_CATAST, NUM_COD_POSTAL
FROM dbo.DM_POSICION_AAII_TB
WHERE ID_REF_CATAST = @RefCat
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (2000)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT, NUM_COD_POSTAL, DES_NOMBRE_VIA, NUM_VIA, DES_MUNICP
FROM dbo.DM_POSICION_AAII_TB
WHERE NUM_COD_POSTAL = @CodigoPostal;

SELECT TOP (@MaxRowsInt)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT, ID_IDUFIR, ID_REF_CATAST, NUM_COD_POSTAL
FROM dbo.DM_POSICION_AACC_TB
WHERE ID_IDUFIR = @Idufir
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (@MaxRowsInt)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT, ID_IDUFIR, ID_REF_CATAST, NUM_COD_POSTAL
FROM dbo.DM_POSICION_AACC_TB
WHERE ID_REF_CATAST = @RefCat
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (2000)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT, NUM_COD_POSTAL, DES_NOMBRE_VIA, NUM_VIA, DES_MUNICP
FROM dbo.DM_POSICION_AACC_TB
WHERE NUM_COD_POSTAL = @CodigoPostal;
"@

        $statsPath = Join-Path $outputRoot "statistics-io-time.txt"
        $statsResult.Messages | Set-Content -LiteralPath $statsPath -Encoding UTF8

        $tableIdx = 1
        foreach ($dt in $statsResult.DataSet.Tables) {
            Save-DataTableCsv -Table $dt -Path (Join-Path $outputRoot ("stats-resultset-" + $tableIdx.ToString("00") + ".csv"))
            $tableIdx++
        }
    }

    # Estimated plans
    if (-not $SkipEstimatedPlans) {
        Write-Section "Planes estimados"
        $estimatedPlans = Invoke-SqlDataTable -Connection $cn -TimeoutSec $CommandTimeoutSec -Parameters @{
            Idufir = $Idufir
            RefCat = $RefCatastral
        } -Query @"
SET SHOWPLAN_XML ON;

SELECT TOP (500)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT
FROM dbo.DM_POSICION_AAII_TB
WHERE ID_IDUFIR = @Idufir
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (500)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT
FROM dbo.DM_POSICION_AAII_TB
WHERE ID_REF_CATAST = @RefCat
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (500)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT
FROM dbo.DM_POSICION_AACC_TB
WHERE ID_IDUFIR = @Idufir
ORDER BY FCH_CIERRE_DT DESC;

SELECT TOP (500)
    ID_ACTIVO_SAREB, FCH_CIERRE_DT
FROM dbo.DM_POSICION_AACC_TB
WHERE ID_REF_CATAST = @RefCat
ORDER BY FCH_CIERRE_DT DESC;

SET SHOWPLAN_XML OFF;
"@

        $i = 1
        foreach ($r in $estimatedPlans.Rows) {
            $plan = [string]$r[0]
            if (-not [string]::IsNullOrWhiteSpace($plan)) {
                $planPath = Join-Path $outputRoot ("estimated-plan-" + $i.ToString("00") + ".sqlplan")
                $plan | Set-Content -LiteralPath $planPath -Encoding UTF8
                $i++
            }
        }
    }

    # Cached plans
    if (-not $SkipCachedPlans) {
        Write-Section "Planes en cache"
        $cached = Invoke-SqlDataTable -Connection $cn -TimeoutSec $CommandTimeoutSec -Parameters @{} -Query @"
SELECT TOP ($TopCachedPlans)
    DB_NAME(st.dbid) AS database_name,
    qs.execution_count,
    qs.total_elapsed_time / 1000 AS total_elapsed_ms,
    qs.total_worker_time / 1000 AS total_cpu_ms,
    qs.total_logical_reads,
    qs.total_logical_writes,
    qs.last_elapsed_time / 1000 AS last_elapsed_ms,
    qs.last_execution_time,
    st.text AS query_text,
    qp.query_plan
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
WHERE st.text LIKE '%DM_POSICION_AAII_TB%'
   OR st.text LIKE '%DM_POSICION_AACC_TB%'
ORDER BY qs.total_elapsed_time DESC;
"@

        Save-DataTableCsv -Table $cached -Path (Join-Path $outputRoot "cached-plans-summary.csv")

        $p = 1
        foreach ($row in $cached.Rows) {
            $xml = [string]$row[9]
            if (-not [string]::IsNullOrWhiteSpace($xml)) {
                $planPath = Join-Path $outputRoot ("cached-plan-" + $p.ToString("00") + ".sqlplan")
                $xml | Set-Content -LiteralPath $planPath -Encoding UTF8
                $p++
            }
        }
    }

    # Missing indexes
    if (-not $SkipMissingIndexes) {
        Write-Section "Missing indexes"
        $missing = Invoke-SqlDataTable -Connection $cn -TimeoutSec $CommandTimeoutSec -Parameters @{} -Query @"
SELECT
    DB_NAME(mid.database_id) AS database_name,
    mid.statement AS table_name,
    migs.user_seeks,
    migs.user_scans,
    migs.last_user_seek,
    migs.avg_total_user_cost,
    migs.avg_user_impact,
    migs.unique_compiles,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns,
    CAST((migs.avg_total_user_cost * migs.avg_user_impact * (migs.user_seeks + migs.user_scans)) AS DECIMAL(18,2)) AS improvement_score,
    'CREATE INDEX IX_AUTO_' + REPLACE(REPLACE(PARSENAME(REPLACE(mid.statement,'[',''),1),']',''),' ','_') +
    ' ON ' + mid.statement +
    ' (' + ISNULL(mid.equality_columns,'') +
    CASE WHEN mid.equality_columns IS NOT NULL AND mid.inequality_columns IS NOT NULL THEN ',' ELSE '' END +
    ISNULL(mid.inequality_columns,'') + ')' +
    ISNULL(' INCLUDE (' + mid.included_columns + ')','') AS create_index_statement
FROM sys.dm_db_missing_index_groups mig
JOIN sys.dm_db_missing_index_group_stats migs ON mig.index_group_handle = migs.group_handle
JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
WHERE mid.database_id = DB_ID()
  AND (mid.statement LIKE '%DM_POSICION_AAII_TB%' OR mid.statement LIKE '%DM_POSICION_AACC_TB%')
ORDER BY improvement_score DESC;
"@

        Save-DataTableCsv -Table $missing -Path (Join-Path $outputRoot "missing-indexes.csv")
    }

    # Estadisticas de tablas e indices actuales
    Write-Section "Estado de tablas e indices"
    $inventory = Invoke-SqlDataTable -Connection $cn -TimeoutSec $CommandTimeoutSec -Parameters @{} -Query @"
SELECT
    t.name AS table_name,
    i.name AS index_name,
    i.index_id,
    i.type_desc,
    i.is_unique,
    i.is_primary_key,
    SUM(ps.row_count) AS row_count,
    SUM(ps.used_page_count) * 8 / 1024.0 AS used_mb
FROM sys.tables t
JOIN sys.indexes i ON t.object_id = i.object_id
JOIN sys.dm_db_partition_stats ps ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE t.name IN ('DM_POSICION_AAII_TB', 'DM_POSICION_AACC_TB')
GROUP BY t.name, i.name, i.index_id, i.type_desc, i.is_unique, i.is_primary_key
ORDER BY t.name, i.index_id;
"@
    Save-DataTableCsv -Table $inventory -Path (Join-Path $outputRoot "table-index-inventory.csv")

    Write-Section "Diagnostico completado"
    Write-Host "Artefactos generados en: $outputRoot" -ForegroundColor Green
}
finally {
    if ($cn) {
        $cn.Dispose()
    }
}
