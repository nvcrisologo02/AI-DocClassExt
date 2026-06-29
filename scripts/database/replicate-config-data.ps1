<#
.SYNOPSIS
  Replica los datos de CONFIGURACION de DocumentIA entre entornos (dev/pre/prod).

.DESCRIPTION
  Copia unicamente las tablas de configuracion del sistema (modelos/providers,
  tipologias, catalogos TDN1/TDN2, plugins por tipologia y prompts). NO toca datos
  operativos (documentos, ejecuciones, auditoria, resultados, validaciones).

  Tablas replicadas (en orden FK-seguro de insercion):
    1. ModeloConfigs          (modelos + providers; columna Provider)
    2. PromptTemplates        (plantillas de prompt)
    3. Tipologias             (catalogo de tipologias)
    4. CatalogoTdn1           (catalogo nivel 1)
    5. CatalogoTdn2           (catalogo nivel 2; FK Tdn1Id -> CatalogoTdn1.Id)
    6. PluginTipologiaConfigs (config de plugins por tipologia; ref. Tipologias.Codigo)

  La generacion es IDEMPOTENTE: usa MERGE por clave primaria con SET IDENTITY_INSERT,
  de modo que conserva los Id originales (imprescindible para mantener la FK
  CatalogoTdn2 -> CatalogoTdn1) y puede ejecutarse varias veces sin duplicar.

  El esquema (PK, columna identidad, columnas, columnas computadas) se detecta
  dinamicamente desde el catalogo del servidor de ORIGEN, por lo que el script
  resiste cambios de columnas sin necesidad de mantenerlo.

.PARAMETER Mode
  Export : (por defecto) lee de ORIGEN y genera un .sql idempotente en disco.
           No modifica ninguna base de datos. Pensado para revisar antes de prod.
  Apply  : ejecuta un .sql previamente generado contra el DESTINO.
  Copy   : lee de ORIGEN y aplica directamente contra el DESTINO (en una transaccion).

.PARAMETER SourceConnectionString
  Cadena de conexion ADO.NET al entorno ORIGEN. Requerida en Export y Copy.
  Ej Azure SQL: "Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Authentication=Active Directory Default;Encrypt=True;"
  Ej SQL auth : "Server=tcp:...,1433;Database=DocumentIA;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=False;"

.PARAMETER TargetConnectionString
  Cadena de conexion ADO.NET al entorno DESTINO. Requerida en Apply y Copy.

.PARAMETER OutputFile
  (Export) Ruta del .sql a generar. Por defecto: artifacts\db-config\config-seed_<timestamp>.sql

.PARAMETER InputFile
  (Apply) Ruta del .sql a ejecutar contra el destino.

.PARAMETER Mirror
  Si se indica, el DESTINO queda como espejo EXACTO del ORIGEN para esas tablas:
  ademas de insertar/actualizar, ELIMINA del destino las filas cuya PK no exista
  en origen (en orden inverso de FK). Sin este switch solo hace upsert (no borra).

.PARAMETER Schema
  Esquema SQL. Por defecto "dbo".

.PARAMETER BatchSize
  Filas por sentencia MERGE (constructor VALUES). Por defecto 250.

.PARAMETER Force
  Omite la confirmacion interactiva en Apply/Copy (util en pipelines).

.EXAMPLE
  # 1) Generar el .sql desde DEV (no modifica nada)
  pwsh ./scripts/database/replicate-config-data.ps1 -Mode Export `
    -SourceConnectionString $env:DOCIA_SRC_CS -OutputFile .\artifacts\db-config\seed-from-dev.sql

.EXAMPLE
  # 2) Revisar el .sql y aplicarlo a PRE
  pwsh ./scripts/database/replicate-config-data.ps1 -Mode Apply `
    -TargetConnectionString $env:DOCIA_DST_CS -InputFile .\artifacts\db-config\seed-from-dev.sql

.EXAMPLE
  # Copia directa DEV -> PRE, dejando PRE como espejo exacto
  pwsh ./scripts/database/replicate-config-data.ps1 -Mode Copy -Mirror `
    -SourceConnectionString $env:DOCIA_SRC_CS -TargetConnectionString $env:DOCIA_DST_CS

.NOTES
  - Requiere el ensamblado System.Data.SqlClient (incluido en .NET Framework y en
    Windows PowerShell 5.1; en PowerShell 7 se carga via Microsoft.Data.SqlClient si
    estuviera disponible, con fallback a System.Data.SqlClient).
  - Las cadenas de conexion NO se versionan: pasalas por parametro o variable de entorno.
  - Para PROD se recomienda Mode Export -> revisar .sql -> Mode Apply.
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidateSet('Export', 'Apply', 'Copy')]
    [string]$Mode = 'Export',

    [string]$SourceConnectionString,
    [string]$TargetConnectionString,

    [string]$OutputFile,
    [string]$InputFile,

    [switch]$Mirror,
    [string]$Schema = 'dbo',
    [int]$BatchSize = 250,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Tablas de configuracion en orden FK-seguro de insercion --------------------
$ConfigTables = @(
    'ModeloConfigs',
    'PromptTemplates',
    'Tipologias',
    'CatalogoTdn1',
    'CatalogoTdn2',
    'PluginTipologiaConfigs'
)

function Write-Step { param([string]$m) Write-Host "[STEP] $m" -ForegroundColor Cyan }
function Write-Info { param([string]$m) Write-Host "[INFO] $m" -ForegroundColor Gray }
function Write-Ok   { param([string]$m) Write-Host "[ OK ] $m" -ForegroundColor Green }
function Write-Warn2{ param([string]$m) Write-Host "[WARN] $m" -ForegroundColor Yellow }

function Quote-Id {
    param([string]$Name)
    return "[$($Name.Replace(']', ']]'))]"
}

function New-SqlConnection {
    param([Parameter(Mandatory)][string]$ConnectionString)
    # Preferir Microsoft.Data.SqlClient (PS7) y caer en System.Data.SqlClient
    $cn = $null
    try {
        $cn = New-Object Microsoft.Data.SqlClient.SqlConnection $ConnectionString
    } catch {
        $cn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    }
    $cn.Open()
    return $cn
}

function Invoke-Reader {
    param([Parameter(Mandatory)]$Connection, [Parameter(Mandatory)][string]$Query)
    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = $Query
    return $cmd.ExecuteReader()
}

function Get-ScalarRows {
    # Devuelve la primera columna de cada fila como array de strings.
    param($Connection, [string]$Query)
    $rows = @()
    $rd = Invoke-Reader -Connection $Connection -Query $Query
    try { while ($rd.Read()) { $rows += [string]$rd.GetValue(0) } } finally { $rd.Close() }
    return , $rows
}

function Get-TableMeta {
    param($Connection, [string]$Schema, [string]$Table)
    $full = "$Schema.$Table"

    $pk = Get-ScalarRows -Connection $Connection -Query @"
SELECT col.name
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id
WHERE i.is_primary_key = 1 AND i.object_id = OBJECT_ID('$full')
ORDER BY ic.key_ordinal;
"@

    $cols = Get-ScalarRows -Connection $Connection -Query @"
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID('$full') AND c.is_computed = 0
ORDER BY c.column_id;
"@

    $identity = Get-ScalarRows -Connection $Connection -Query @"
SELECT c.name
FROM sys.columns c
WHERE c.object_id = OBJECT_ID('$full') AND c.is_identity = 1;
"@

    if (-not $cols -or $cols.Count -eq 0) { throw "La tabla $full no existe o no tiene columnas en el origen." }
    if (-not $pk -or $pk.Count -eq 0) { throw "La tabla $full no tiene clave primaria; no se puede generar MERGE idempotente." }

    return [pscustomobject]@{
        Schema       = $Schema
        Table        = $Table
        Columns      = $cols
        PrimaryKey   = $pk
        IdentityCol  = ($identity | Select-Object -First 1)
    }
}

function ConvertTo-SqlLiteral {
    param($Value)
    if ($null -eq $Value -or $Value -is [DBNull]) { return 'NULL' }
    if ($Value -is [byte[]]) {
        if ($Value.Length -eq 0) { return '0x' }
        return '0x' + ([BitConverter]::ToString($Value) -replace '-', '')
    }
    if ($Value -is [bool]) { if ($Value) { return '1' } else { return '0' } }
    if ($Value -is [datetime]) { return "'" + $Value.ToString('yyyy-MM-ddTHH:mm:ss.fffffff') + "'" }
    if ($Value -is [System.DateTimeOffset]) { return "'" + $Value.ToString('yyyy-MM-ddTHH:mm:ss.fffffffzzz') + "'" }
    if ($Value -is [guid]) { return "'" + $Value.ToString() + "'" }
    if ($Value -is [decimal] -or $Value -is [double] -or $Value -is [single]) {
        return $Value.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Value -is [byte] -or $Value -is [int16] -or $Value -is [int32] -or $Value -is [int64] -or
        $Value -is [sbyte] -or $Value -is [uint16] -or $Value -is [uint32] -or $Value -is [uint64]) {
        return $Value.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }
    # string y cualquier otro tipo -> nvarchar con escape de comilla simple
    return "N'" + ($Value.ToString().Replace("'", "''")) + "'"
}

function New-MergeSql {
    <#
      Genera el bloque MERGE idempotente para una tabla a partir de sus filas.
      $Rows es un array de hashtables/objetos indexables por nombre de columna.
    #>
    param(
        [Parameter(Mandatory)]$Meta,
        [Parameter(Mandatory)][System.Collections.IEnumerable]$Rows,
        [int]$BatchSize = 250,
        [switch]$Mirror
    )

    $schema = $Meta.Schema; $table = $Meta.Table
    $qTable = "$(Quote-Id $schema).$(Quote-Id $table)"
    $cols   = $Meta.Columns
    $pk     = $Meta.PrimaryKey
    $nonPk  = @($cols | Where-Object { $pk -notcontains $_ })
    $hasIdentity = [string]::IsNullOrEmpty($Meta.IdentityCol) -eq $false

    $rowList = @($Rows)
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("-- ===== $qTable ($($rowList.Count) filas) =====")

    # Mirror: borrar en destino las PKs que no esten en origen (PK simple unicamente)
    if ($Mirror) {
        if ($pk.Count -ne 1) {
            throw "Mirror no soportado para clave primaria compuesta en $qTable."
        }
        $pkCol = $pk[0]
        if ($rowList.Count -eq 0) {
            [void]$sb.AppendLine("DELETE FROM $qTable;")
        } else {
            $keyLiterals = @($rowList | ForEach-Object { ConvertTo-SqlLiteral $_[$pkCol] })
            [void]$sb.AppendLine("DELETE FROM $qTable WHERE $(Quote-Id $pkCol) NOT IN (")
            [void]$sb.AppendLine('    ' + ($keyLiterals -join ', '))
            [void]$sb.AppendLine(');')
        }
    }

    if ($rowList.Count -eq 0) {
        [void]$sb.AppendLine("-- (origen sin filas; nada que insertar)")
        return $sb.ToString()
    }

    if ($hasIdentity) { [void]$sb.AppendLine("SET IDENTITY_INSERT $qTable ON;") }

    $colListQuoted = ($cols | ForEach-Object { Quote-Id $_ }) -join ', '
    $onClause      = ($pk   | ForEach-Object { "tgt.$(Quote-Id $_) = src.$(Quote-Id $_)" }) -join ' AND '
    $insertCols    = $colListQuoted
    $insertVals    = ($cols | ForEach-Object { "src.$(Quote-Id $_)" }) -join ', '
    $updateSet     = ($nonPk | ForEach-Object { "tgt.$(Quote-Id $_) = src.$(Quote-Id $_)" }) -join ', '

    # Batches del constructor VALUES (limite practico de filas por sentencia)
    for ($i = 0; $i -lt $rowList.Count; $i += $BatchSize) {
        $batch = $rowList[$i..([Math]::Min($i + $BatchSize - 1, $rowList.Count - 1))]
        $valuesLines = foreach ($row in $batch) {
            $vals = foreach ($c in $cols) { ConvertTo-SqlLiteral $row[$c] }
            '    (' + ($vals -join ', ') + ')'
        }

        [void]$sb.AppendLine("MERGE INTO $qTable AS tgt")
        [void]$sb.AppendLine("USING (VALUES")
        [void]$sb.AppendLine(($valuesLines -join ",`r`n"))
        [void]$sb.AppendLine(") AS src ($colListQuoted)")
        [void]$sb.AppendLine("ON $onClause")
        if ($nonPk.Count -gt 0) {
            [void]$sb.AppendLine("WHEN MATCHED THEN UPDATE SET $updateSet")
        }
        [void]$sb.AppendLine("WHEN NOT MATCHED BY TARGET THEN INSERT ($insertCols) VALUES ($insertVals);")
    }

    if ($hasIdentity) { [void]$sb.AppendLine("SET IDENTITY_INSERT $qTable OFF;") }
    [void]$sb.AppendLine('')
    return $sb.ToString()
}

function Read-TableRows {
    param($Connection, $Meta)
    $colListQuoted = ($Meta.Columns | ForEach-Object { Quote-Id $_ }) -join ', '
    $qTable = "$(Quote-Id $Meta.Schema).$(Quote-Id $Meta.Table)"
    $rows = New-Object System.Collections.ArrayList
    $rd = Invoke-Reader -Connection $Connection -Query "SELECT $colListQuoted FROM $qTable;"
    try {
        while ($rd.Read()) {
            $row = @{}
            foreach ($c in $Meta.Columns) { $row[$c] = $rd[$c] }
            [void]$rows.Add($row)
        }
    } finally { $rd.Close() }
    return $rows
}

function Build-FullScript {
    param($Connection, [string]$Schema, [string[]]$Tables, [int]$BatchSize, [switch]$Mirror, [string]$SourceLabel)

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("-- ============================================================================")
    [void]$sb.AppendLine("-- DocumentIA - Replicacion de datos de configuracion")
    [void]$sb.AppendLine("-- Generado: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  Origen: $SourceLabel")
    [void]$sb.AppendLine("-- Modo espejo (borra filas ausentes en origen): $([bool]$Mirror)")
    [void]$sb.AppendLine("-- Idempotente: MERGE por PK con IDENTITY_INSERT. Reejecutable.")
    [void]$sb.AppendLine("-- ============================================================================")
    [void]$sb.AppendLine("SET XACT_ABORT ON;")
    [void]$sb.AppendLine("SET NOCOUNT ON;")
    [void]$sb.AppendLine("BEGIN TRANSACTION;")
    [void]$sb.AppendLine('')

    # Mirror: las eliminaciones deben ir en orden INVERSO de FK (hijos antes que padres)
    if ($Mirror) {
        [void]$sb.AppendLine("-- ---- Fase 1: eliminar en destino lo que no exista en origen (orden inverso FK) ----")
        $reverse = @($Tables)
        [array]::Reverse($reverse)
        foreach ($t in $reverse) {
            $meta = Get-TableMeta -Connection $Connection -Schema $Schema -Table $t
            $rows = Read-TableRows -Connection $Connection -Meta $meta
            $qTable = "$(Quote-Id $meta.Schema).$(Quote-Id $meta.Table)"
            if ($meta.PrimaryKey.Count -ne 1) { throw "Mirror no soportado para PK compuesta en $qTable." }
            $pkCol = $meta.PrimaryKey[0]
            if ($rows.Count -eq 0) {
                [void]$sb.AppendLine("DELETE FROM $qTable;")
            } else {
                $keyLiterals = @($rows | ForEach-Object { ConvertTo-SqlLiteral $_[$pkCol] })
                [void]$sb.AppendLine("DELETE FROM $qTable WHERE $(Quote-Id $pkCol) NOT IN (")
                [void]$sb.AppendLine('    ' + ($keyLiterals -join ', '))
                [void]$sb.AppendLine(');')
            }
        }
        [void]$sb.AppendLine('')
    }

    [void]$sb.AppendLine("-- ---- Fase 2: upsert (insert/update) en orden FK de insercion ----")
    foreach ($t in $Tables) {
        Write-Info "Leyendo $Schema.$t ..."
        $meta = Get-TableMeta -Connection $Connection -Schema $Schema -Table $t
        $rows = Read-TableRows -Connection $Connection -Meta $meta
        # En el script completo el borrado de Mirror ya se hizo en Fase 1: aqui solo upsert
        $block = New-MergeSql -Meta $meta -Rows $rows -BatchSize $BatchSize
        [void]$sb.Append($block)
        Write-Info "  $t -> $($rows.Count) filas"
    }

    [void]$sb.AppendLine("COMMIT TRANSACTION;")
    [void]$sb.AppendLine("PRINT 'Replicacion de configuracion completada.';")
    return $sb.ToString()
}

function Invoke-SqlScript {
    param($Connection, [string]$Sql)
    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = $Sql
    [void]$cmd.ExecuteNonQuery()
}

function Test-LooksLikeProd {
    param([string]$ConnectionString)
    return ($ConnectionString -match '(?i)prod')
}

# ============================ Validacion de parametros ============================
switch ($Mode) {
    'Export' { if (-not $SourceConnectionString) { throw "Mode Export requiere -SourceConnectionString." } }
    'Apply'  { if (-not $TargetConnectionString) { throw "Mode Apply requiere -TargetConnectionString." }
               if (-not $InputFile) { throw "Mode Apply requiere -InputFile." }
               if (-not (Test-Path -LiteralPath $InputFile)) { throw "No existe el fichero: $InputFile" } }
    'Copy'   { if (-not $SourceConnectionString) { throw "Mode Copy requiere -SourceConnectionString." }
               if (-not $TargetConnectionString) { throw "Mode Copy requiere -TargetConnectionString." } }
}

Write-Step "Modo: $Mode | Tablas: $($ConfigTables -join ', ')"
if ($Mirror) { Write-Warn2 "Mirror ACTIVADO: el destino quedara como espejo exacto del origen (se borraran filas ausentes en origen)." }

# ============================ Ejecucion por modo =================================
if ($Mode -eq 'Export') {
    if (-not $OutputFile) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $OutputFile = Join-Path (Join-Path '.' 'artifacts\db-config') "config-seed_$stamp.sql"
    }
    $dir = Split-Path -Parent $OutputFile
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $src = New-SqlConnection -ConnectionString $SourceConnectionString
    try {
        $label = $src.DataSource + '/' + $src.Database
        Write-Step "Generando script desde ORIGEN ($label) ..."
        $sql = Build-FullScript -Connection $src -Schema $Schema -Tables $ConfigTables -BatchSize $BatchSize -Mirror:$Mirror -SourceLabel $label
        [System.IO.File]::WriteAllText($OutputFile, $sql, [System.Text.UTF8Encoding]::new($false))
        Write-Ok "Script generado: $OutputFile"
        Write-Info "Revisalo y aplicalo con: -Mode Apply -InputFile '$OutputFile' -TargetConnectionString <destino>"
    } finally { $src.Close() }
}
elseif ($Mode -eq 'Apply') {
    $isProd = Test-LooksLikeProd -ConnectionString $TargetConnectionString
    if ($isProd -and -not $Force -and -not $PSCmdlet.ShouldProcess($TargetConnectionString, "APLICAR configuracion (parece PROD)")) {
        Write-Warn2 "Cancelado por el usuario."
        return
    }
    $sql = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $InputFile))
    $dst = New-SqlConnection -ConnectionString $TargetConnectionString
    try {
        Write-Step "Aplicando $InputFile contra DESTINO ($($dst.DataSource)/$($dst.Database)) ..."
        Invoke-SqlScript -Connection $dst -Sql $sql
        Write-Ok "Aplicado correctamente."
    } finally { $dst.Close() }
}
elseif ($Mode -eq 'Copy') {
    $src = New-SqlConnection -ConnectionString $SourceConnectionString
    $sql = $null
    try {
        $label = $src.DataSource + '/' + $src.Database
        Write-Step "Generando script desde ORIGEN ($label) ..."
        $sql = Build-FullScript -Connection $src -Schema $Schema -Tables $ConfigTables -BatchSize $BatchSize -Mirror:$Mirror -SourceLabel $label
    } finally { $src.Close() }

    $isProd = Test-LooksLikeProd -ConnectionString $TargetConnectionString
    if ($isProd -and -not $Force -and -not $PSCmdlet.ShouldProcess($TargetConnectionString, "COPIAR configuracion a DESTINO (parece PROD)")) {
        Write-Warn2 "Cancelado por el usuario."
        return
    }
    $dst = New-SqlConnection -ConnectionString $TargetConnectionString
    try {
        Write-Step "Aplicando contra DESTINO ($($dst.DataSource)/$($dst.Database)) ..."
        Invoke-SqlScript -Connection $dst -Sql $sql
        Write-Ok "Copia directa completada."
    } finally { $dst.Close() }
}
