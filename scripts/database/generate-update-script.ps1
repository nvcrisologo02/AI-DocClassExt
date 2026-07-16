<#
.SYNOPSIS
  Genera un script .sql de solo-UPDATEs para llevar los datos de una tabla de un
  entorno a otro, casando por las columnas clave que se indiquen.

.DESCRIPTION
  No hay linked servers entre los servidores SQL de los entornos, de modo que la
  copia necesita una fase intermedia: este script lee del ORIGEN y emite un .sql
  que se revisa y se ejecuta a mano contra el DESTINO.

  SEMANTICA: solo UPDATE. Solo se actualizan las filas cuya clave existe en ambos
  entornos. No inserta filas nuevas ni borra las ausentes. El script NUNCA se
  conecta al destino.

  Como no se conecta al destino, no puede saber en el momento de generar que claves
  existiran alli. Por eso cada UPDATE lleva detras un aviso IF @@ROWCOUNT = 0: al
  ejecutar en el destino, esos avisos delatan las claves huerfanas (existen en el
  origen pero no en el destino), que si no se descartarian en silencio.

  La clave la eliges tu (-KeyColumns) y debe ser la clave de NEGOCIO, no el Id: las
  columnas identidad divergen entre entornos (el Id 7 de dev no es la misma fila que
  el Id 7 de pro). Para replicar el conjunto de configuracion PRESERVANDO los Id
  (necesario para sostener la FK CatalogoTdn2.Tdn1Id -> CatalogoTdn1.Id), esta no es
  la herramienta: usa replicate-config-data.ps1.

  El esquema (columnas, identidad, computadas, nulabilidad) se detecta en tiempo de
  ejecucion desde sys.columns, por lo que el script resiste cambios de columnas sin
  necesidad de mantenerlo.

.PARAMETER SourceConnectionString
  Cadena de conexion ADO.NET al entorno ORIGEN.
  Con -EntraAuth basta con Server/Database/Encrypt:
  "Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Encrypt=True;"

.PARAMETER Table
  Nombre de la tabla a exportar. Ej: CatalogoTdn1

.PARAMETER KeyColumns
  Columnas que actuan como clave de union con el destino. Admite varias:
  -KeyColumns Codigo
  -KeyColumns PromptKey,Version

.PARAMETER Columns
  Subconjunto de columnas a actualizar. Por defecto se actualizan todas las columnas
  excepto las de clave, la identidad y las computadas.

.PARAMETER Where
  Predicado que filtra las filas del ORIGEN, sin la palabra WHERE. Ej: "IsActive = 1"
  SEGURIDAD: se concatena crudo (es un filtro arbitrario). Pensado para que lo escriba a
  mano el operador con acceso de lectura a dev. No alimentar desde fuentes no confiables
  (CI, entrada de usuario): seria inyeccion SQL contra el origen. El destino no se toca.

.PARAMETER Schema
  Esquema SQL. Por defecto "dbo".

.PARAMETER OutputFile
  Ruta del .sql a generar. Por defecto: artifacts/db-config/update_<Tabla>_<timestamp>.sql

.PARAMETER NoBackup
  Por defecto, el .sql generado hace un backup de la tabla DESTINO antes de actualizar:
  un 'SELECT * INTO <Tabla>__bak_<timestamp>' que se crea FUERA de la transaccion, de modo
  que persiste aunque el UPDATE se revierta. Con -NoBackup se omite ese bloque.

.PARAMETER EntraAuth
  Obtiene un token de Entra ID via 'az account get-access-token' y lo aplica a la
  conexion. Requiere 'az login' previo.

.PARAMETER SourceAccessToken
  Token explicito, alternativa a -EntraAuth.

.EXAMPLE
  # Catalogo TDN1 completo, casando por Codigo
  az login
  pwsh ./scripts/database/generate-update-script.ps1 -EntraAuth `
    -SourceConnectionString "Server=tcp:srbsqldevdocai.database.windows.net,1433;Database=DocumentIA;Encrypt=True;" `
    -Table CatalogoTdn1 -KeyColumns Codigo

.EXAMPLE
  # Solo el prompt de Phase 2
  pwsh ./scripts/database/generate-update-script.ps1 -EntraAuth `
    -SourceConnectionString $env:DOCIA_SRC_CS `
    -Table CatalogoTdn1 -KeyColumns Codigo -Columns TDN2_Prompt

.EXAMPLE
  # Prompts activos, clave compuesta
  pwsh ./scripts/database/generate-update-script.ps1 -EntraAuth `
    -SourceConnectionString $env:DOCIA_SRC_CS `
    -Table PromptTemplates -KeyColumns PromptKey,Version -Where "IsActive = 1"

.NOTES
  - Los parametros NO son [Parameter(Mandatory)] a proposito: se validan a mano en
    Invoke-Main. Un parametro mandatory abriria un prompt interactivo al dot-sourcear
    el script desde los tests y los colgaria.
  - El fichero se emite en UTF-8 CON BOM. Sin BOM, sqlcmd aplica la code page actual
    y corrompe los acentos de los prompts. Con sqlcmd usa ademas -f 65001.
  - Las cadenas de conexion NO se versionan: pasalas por parametro o variable de entorno.

  Tests: scripts/database/tests/generate-update-script.Tests.ps1
  Manual: scripts/database/README-generate-update-script.md
#>
param(
    [string]$SourceConnectionString,
    [string]$Table,
    [string[]]$KeyColumns,
    [string[]]$Columns,
    [string]$Where,
    [string]$Schema = 'dbo',
    [string]$OutputFile,
    [switch]$NoBackup,
    [switch]$EntraAuth,
    [string]$SourceAccessToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Step { param([string]$m) Write-Host "[STEP] $m" -ForegroundColor Cyan }
function Write-Info { param([string]$m) Write-Host "[INFO] $m" -ForegroundColor Gray }
function Write-Ok   { param([string]$m) Write-Host "[ OK ] $m" -ForegroundColor Green }
function Write-Warn2{ param([string]$m) Write-Host "[WARN] $m" -ForegroundColor Yellow }

# ============================ Helpers puros ======================================

function Quote-Id {
    param([string]$Name)
    return "[$($Name.Replace(']', ']]'))]"
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
    # InvariantCulture: sin esto, un locale espanol emitiria comas decimales y romperia el SQL.
    if ($Value -is [decimal] -or $Value -is [double] -or $Value -is [single]) {
        return $Value.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Value -is [byte] -or $Value -is [int16] -or $Value -is [int32] -or $Value -is [int64] -or
        $Value -is [sbyte] -or $Value -is [uint16] -or $Value -is [uint32] -or $Value -is [uint64]) {
        return $Value.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    }
    # string y cualquier otro tipo -> nvarchar con escape de comilla simple.
    # Un literal N'...' de mas de 4000 caracteres se promociona a nvarchar(max) y no
    # se trunca, de modo que TDN2_Prompt y Content son seguros como literal unico.
    return "N'" + ($Value.ToString().Replace("'", "''")) + "'"
}

function New-TableMetaObject {
    param(
        [string]$Schema,
        [string]$Table,
        [string[]]$Columns,
        [string]$IdentityCol,
        [string[]]$ComputedCols,
        [string[]]$NullableCols,
        # Columnas rowversion/timestamp: SQL Server las genera solo y rechaza que un
        # UPDATE les asigne un valor, asi que nunca deben entrar en el SET.
        [string[]]$RowversionCols = @()
    )
    return [pscustomobject]@{
        Schema         = $Schema
        Table          = $Table
        Columns        = @($Columns)
        IdentityCol    = $IdentityCol
        ComputedCols   = @($ComputedCols)
        NullableCols   = @($NullableCols)
        RowversionCols = @($RowversionCols)
    }
}

function Assert-KeyColumns {
    param($Meta, [string[]]$KeyColumns)
    # El Where-Object filtra los nulos y NO es opcional: @($null) tiene Count 1, no 0.
    # Sin el, un -KeyColumns ausente pareceria una clave de una columna vacia.
    $keys = @($KeyColumns | Where-Object { $_ })
    if ($keys.Count -eq 0) {
        throw "Se requiere -KeyColumns con al menos una columna."
    }
    foreach ($k in $keys) {
        if ($Meta.Columns -notcontains $k) {
            throw "La columna clave '$k' no existe en $($Meta.Schema).$($Meta.Table). Columnas disponibles: $($Meta.Columns -join ', ')."
        }
    }
}

function Resolve-UpdateColumns {
    param($Meta, [string[]]$KeyColumns, [string[]]$Columns)

    # El Where-Object filtra los nulos y NO es opcional: @($null) tiene Count 1, no 0.
    # Sin el, omitir -Columns pareceria pedir una columna vacia, el codigo entraria
    # siempre en la rama de "subconjunto indicado" y el camino por defecto (todas las
    # columnas elegibles) nunca se alcanzaria.
    $keys = @($KeyColumns | Where-Object { $_ })
    $requested = @($Columns | Where-Object { $_ })

    if ($requested.Count -gt 0) {
        $canonical = @()
        foreach ($c in $requested) {
            # -contains es case-insensitive; localizamos el nombre canonico de la
            # columna para devolverlo con el casing real del esquema, no el del usuario.
            $match = $Meta.Columns | Where-Object { $_ -eq $c } | Select-Object -First 1
            if (-not $match) {
                throw "La columna '$c' indicada en -Columns no existe en $($Meta.Schema).$($Meta.Table)."
            }
            if ($keys -contains $match) {
                throw "La columna '$c' es una columna clave y no puede actualizarse. Quitala de -Columns."
            }
            if ($match -eq $Meta.IdentityCol) {
                throw "La columna '$c' es la columna identidad y no puede actualizarse."
            }
            if ($Meta.ComputedCols -contains $match) {
                throw "La columna '$c' es una columna computada y no puede actualizarse."
            }
            if ($Meta.RowversionCols -contains $match) {
                throw "La columna '$c' es rowversion/timestamp: SQL Server la genera sola y no admite UPDATE."
            }
            $canonical += $match
        }
        return $canonical
    }

    $resolved = @($Meta.Columns | Where-Object {
        $keys -notcontains $_ -and
        $_ -ne $Meta.IdentityCol -and
        $Meta.ComputedCols -notcontains $_ -and
        $Meta.RowversionCols -notcontains $_
    })

    if ($resolved.Count -eq 0) {
        throw "No queda ninguna columna que actualizar en $($Meta.Schema).$($Meta.Table) tras excluir clave, identidad y computadas."
    }
    return $resolved
}

# ============================ Emision del SQL ====================================

function New-UpdateStatement {
    param($Meta, $Row, [string[]]$KeyColumns, [string[]]$SetColumns)

    $qTable = "$(Quote-Id $Meta.Schema).$(Quote-Id $Meta.Table)"

    $setLines = @($SetColumns | ForEach-Object {
        "$(Quote-Id $_) = $(ConvertTo-SqlLiteral $Row[$_])"
    })
    $whereParts = @($KeyColumns | ForEach-Object {
        "$(Quote-Id $_) = $(ConvertTo-SqlLiteral $Row[$_])"
    })

    # Descripcion legible de la clave para el aviso. Se emite via ConvertTo-SqlLiteral
    # para que una comilla en el valor de la clave no rompa el literal del PRINT.
    $keyDesc = ($KeyColumns | ForEach-Object { "$_ = $($Row[$_])" }) -join ', '
    $msgLiteral = ConvertTo-SqlLiteral "AVISO: sin match en destino -> $keyDesc"

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine("UPDATE $qTable")
    [void]$sb.AppendLine("SET $($setLines -join ",`r`n    ")")
    [void]$sb.AppendLine("WHERE $($whereParts -join ' AND ');")
    [void]$sb.AppendLine("IF @@ROWCOUNT = 0 PRINT $msgLiteral;")
    return $sb.ToString()
}

function New-UpdateScript {
    param(
        $Meta,
        $Rows,
        [string[]]$KeyColumns,
        [string[]]$SetColumns,
        [string]$SourceLabel,
        [string]$WhereClause,
        [string]$Timestamp,
        [bool]$IncludeBackup = $false
    )

    $qTable   = "$(Quote-Id $Meta.Schema).$(Quote-Id $Meta.Table)"
    $qSchema  = Quote-Id $Meta.Schema
    $rowList  = @($Rows)
    $keyList  = ($KeyColumns | ForEach-Object { Quote-Id $_ }) -join ', '
    $setList  = ($SetColumns | ForEach-Object { Quote-Id $_ }) -join ', '
    $filtro   = if ($WhereClause) { $WhereClause } else { '(ninguno)' }
    # El backup solo tiene sentido si hay algo que actualizar.
    $doBackup = $IncludeBackup -and $rowList.Count -gt 0

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('-- ============================================================================')
    [void]$sb.AppendLine('-- DocumentIA - UPDATE de configuracion entre entornos')
    [void]$sb.AppendLine("-- Origen : $SourceLabel")
    [void]$sb.AppendLine("-- Tabla  : $qTable")
    [void]$sb.AppendLine("-- Clave  : $keyList")
    [void]$sb.AppendLine("-- Set    : $setList")
    [void]$sb.AppendLine("-- Filtro : $filtro")
    [void]$sb.AppendLine("-- Filas  : $($rowList.Count)        Generado: $Timestamp")
    if ($doBackup) {
        [void]$sb.AppendLine("-- Backup : SI - crea $qSchema.[<Tabla>__bak_<timestamp>] antes de actualizar")
    } else {
        [void]$sb.AppendLine('-- Backup : NO')
    }
    [void]$sb.AppendLine('--')
    [void]$sb.AppendLine('-- SOLO UPDATE: no inserta ni borra filas de la tabla destino.')
    [void]$sb.AppendLine('-- Para ensayar sin persistir, sustituye COMMIT TRANSACTION por ROLLBACK TRANSACTION.')
    [void]$sb.AppendLine('-- ============================================================================')
    [void]$sb.AppendLine('SET XACT_ABORT ON;')
    [void]$sb.AppendLine('SET NOCOUNT ON;')
    [void]$sb.AppendLine('')

    if ($doBackup) {
        # Nombre base del backup con las comillas simples escapadas para el literal N'...'.
        $tableEsc = $Meta.Table.Replace("'", "''")
        [void]$sb.AppendLine('-- ---- Backup de la tabla destino ANTES de actualizar (snapshot completo) ----')
        [void]$sb.AppendLine('-- Se crea FUERA de la transaccion, por lo que PERSISTE aunque el UPDATE se')
        [void]$sb.AppendLine('-- revierta o falle. El nombre lleva un timestamp de ejecucion: cada corrida')
        [void]$sb.AppendLine('-- genera su propio backup y no pisa los anteriores. Con XACT_ABORT ON, si el')
        [void]$sb.AppendLine('-- backup falla (p. ej. sin permiso de CREATE TABLE) el lote se aborta y el')
        [void]$sb.AppendLine('-- UPDATE no llega a ejecutarse: nunca hay UPDATE sin backup.')
        [void]$sb.AppendLine("DECLARE @bak sysname = N'${tableEsc}__bak_' + FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss');")
        [void]$sb.AppendLine("DECLARE @baksql nvarchar(max) = N'SELECT * INTO $qSchema.' + QUOTENAME(@bak) + N' FROM $qTable;';")
        [void]$sb.AppendLine('EXEC sp_executesql @baksql;')
        [void]$sb.AppendLine("PRINT 'Backup de la tabla destino creado: $qSchema.' + QUOTENAME(@bak);")
        [void]$sb.AppendLine('')
    }

    [void]$sb.AppendLine('BEGIN TRANSACTION;')
    [void]$sb.AppendLine('')

    if ($rowList.Count -eq 0) {
        [void]$sb.AppendLine('-- (el origen no devolvio filas; nada que actualizar)')
        [void]$sb.AppendLine('')
    } else {
        foreach ($row in $rowList) {
            [void]$sb.Append((New-UpdateStatement -Meta $Meta -Row $row -KeyColumns $KeyColumns -SetColumns $SetColumns))
            [void]$sb.AppendLine('')
        }
    }

    [void]$sb.AppendLine('COMMIT TRANSACTION;')
    [void]$sb.AppendLine("PRINT 'Generado desde $SourceLabel. Filas emitidas: $($rowList.Count).';")
    return $sb.ToString()
}

# ============================ Acceso a BBDD ======================================

function Get-SqlAccessToken {
    # Token de Entra ID para Azure SQL (valido para cualquier servidor del tenant).
    $token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "No se pudo obtener el token de Azure SQL. Ejecuta 'az login' (y selecciona el tenant correcto) e intentalo de nuevo."
    }
    return $token
}

function New-SqlConnection {
    param(
        [Parameter(Mandatory)][string]$ConnectionString,
        [string]$AccessToken
    )
    # Preferir Microsoft.Data.SqlClient (PS7) y caer en System.Data.SqlClient.
    $cn = $null
    try {
        $cn = New-Object Microsoft.Data.SqlClient.SqlConnection $ConnectionString
    } catch {
        $cn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
    }
    # No combinar AccessToken con User Id/Password en la cadena de conexion.
    if ($AccessToken) { $cn.AccessToken = $AccessToken }
    $cn.Open()
    return $cn
}

function Get-TableMeta {
    param($Connection, [string]$Schema, [string]$Table)

    $full = "$Schema.$Table"
    $cols = @(); $identity = $null; $computed = @(); $nullable = @(); $rowversion = @()

    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = @"
SELECT c.name, c.is_identity, c.is_computed, c.is_nullable, t.name AS type_name
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID(@full)
ORDER BY c.column_id;
"@
    [void]$cmd.Parameters.AddWithValue('@full', $full)

    # ExecuteReader SIEMPRE en el sitio de uso: un reader es IEnumerable y retornarlo
    # desde una funcion PowerShell lo desenrolla y lo rompe.
    $rd = $cmd.ExecuteReader()
    try {
        while ($rd.Read()) {
            $n = [string]$rd.GetValue(0)
            $cols += $n
            if ([bool]$rd.GetValue(1)) { $identity = $n }
            if ([bool]$rd.GetValue(2)) { $computed += $n }
            if ([bool]$rd.GetValue(3)) { $nullable += $n }
            $typeName = [string]$rd.GetValue(4)
            if ($typeName -in @('timestamp', 'rowversion')) { $rowversion += $n }
        }
    } finally { $rd.Close() }

    if ($cols.Count -eq 0) {
        throw "La tabla $full no existe o no tiene columnas en el origen."
    }

    return New-TableMetaObject -Schema $Schema -Table $Table -Columns $cols `
        -IdentityCol $identity -ComputedCols $computed -NullableCols $nullable `
        -RowversionCols $rowversion
}

function Get-DuplicateKeys {
    param($Connection, $Meta, [string[]]$KeyColumns, [string]$Where)

    $qTable   = "$(Quote-Id $Meta.Schema).$(Quote-Id $Meta.Table)"
    $keyList  = ($KeyColumns | ForEach-Object { Quote-Id $_ }) -join ', '
    $whereSql = if ($Where) { "WHERE $Where" } else { '' }

    $dups = @()
    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = "SELECT TOP 10 $keyList, COUNT(*) AS Repeticiones FROM $qTable $whereSql GROUP BY $keyList HAVING COUNT(*) > 1;"

    $rd = $cmd.ExecuteReader()
    try {
        while ($rd.Read()) {
            $parts = @()
            for ($i = 0; $i -lt $KeyColumns.Count; $i++) {
                $parts += "$($KeyColumns[$i]) = $($rd.GetValue($i))"
            }
            $dups += "$($parts -join ', ') (x$($rd.GetValue($KeyColumns.Count)))"
        }
    } finally { $rd.Close() }
    return , $dups
}

function Read-TableRows {
    param($Connection, $Meta, [string[]]$SelectColumns, [string]$Where)

    $qTable   = "$(Quote-Id $Meta.Schema).$(Quote-Id $Meta.Table)"
    $colList  = ($SelectColumns | ForEach-Object { Quote-Id $_ }) -join ', '
    $whereSql = if ($Where) { "WHERE $Where" } else { '' }

    $rows = New-Object System.Collections.ArrayList
    $cmd = $Connection.CreateCommand()
    $cmd.CommandTimeout = 0
    $cmd.CommandText = "SELECT $colList FROM $qTable $whereSql;"

    $rd = $cmd.ExecuteReader()
    try {
        while ($rd.Read()) {
            $row = @{}
            foreach ($c in $SelectColumns) { $row[$c] = $rd[$c] }
            [void]$rows.Add($row)
        }
    } finally { $rd.Close() }
    # La coma unaria evita que PowerShell desenrolle el ArrayList al retornarlo: sin
    # ella, 0 filas devuelve $null (y $rows.Count crashea bajo StrictMode) y 1 fila
    # devuelve el hashtable suelto en vez de un array de 1.
    return , $rows
}

# ============================ Salida =============================================

function Resolve-OutputPath {
    param([string]$OutputFile, [string]$Table, [string]$Timestamp)
    if ($OutputFile) { return $OutputFile }
    return Join-Path (Join-Path '.' 'artifacts/db-config') "update_${Table}_$Timestamp.sql"
}

function Write-SqlFile {
    param([string]$Path, [string]$Content)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    # UTF-8 CON BOM: sin el BOM, sqlcmd aplica la code page actual y corrompe los
    # acentos de los prompts (N'Clasificacion' -> mojibake dentro del literal).
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($true))
}

# ============================ Orquestacion =======================================

function Invoke-Main {
    # Validacion manual de parametros: ver la nota de .NOTES sobre Mandatory.
    if (-not $Table) { throw "Se requiere -Table." }
    if (-not $KeyColumns -or @($KeyColumns | Where-Object { $_ }).Count -eq 0) {
        throw "Se requiere -KeyColumns con al menos una columna."
    }
    if (-not $SourceConnectionString) { throw "Se requiere -SourceConnectionString." }

    $srcToken = $SourceAccessToken
    if ($EntraAuth) {
        if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
            throw "EntraAuth requiere Azure CLI (az) autenticado ('az login')."
        }
        Write-Step 'Obteniendo token de Entra ID para Azure SQL ...'
        if (-not $srcToken) { $srcToken = Get-SqlAccessToken }
    }

    $cn = New-SqlConnection -ConnectionString $SourceConnectionString -AccessToken $srcToken
    try {
        $label = "$($cn.DataSource)/$($cn.Database)"
        Write-Step "Leyendo esquema de $Schema.$Table desde $label ..."

        $meta = Get-TableMeta -Connection $cn -Schema $Schema -Table $Table
        Assert-KeyColumns -Meta $meta -KeyColumns $KeyColumns
        $setColumns = Resolve-UpdateColumns -Meta $meta -KeyColumns $KeyColumns -Columns $Columns

        # Aviso (no aborta): un WHERE col = NULL nunca casa, esas filas se perderian
        # en el destino sin dejar rastro.
        foreach ($k in $KeyColumns) {
            if ($meta.NullableCols -contains $k) {
                Write-Warn2 "La columna clave '$k' admite NULL. Las filas con NULL en esa columna nunca casaran en el destino."
            }
        }

        Write-Step 'Comprobando unicidad de la clave en el origen ...'
        $dups = Get-DuplicateKeys -Connection $cn -Meta $meta -KeyColumns $KeyColumns -Where $Where
        if (@($dups).Count -gt 0) {
            throw ("La clave ($($KeyColumns -join ', ')) NO es unica en el origen. " +
                   "Con un UPDATE por fila, las filas duplicadas se pisarian en silencio en el destino. " +
                   "Claves duplicadas: $($dups -join ' | ')")
        }

        $selectColumns = @($KeyColumns) + @($setColumns)
        Write-Info "Columnas a actualizar: $($setColumns -join ', ')"
        $rows = Read-TableRows -Connection $cn -Meta $meta -SelectColumns $selectColumns -Where $Where
        Write-Info "Filas leidas del origen: $($rows.Count)"

        $stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
        $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        $sql  = New-UpdateScript -Meta $meta -Rows $rows -KeyColumns $KeyColumns -SetColumns $setColumns `
                    -SourceLabel $label -WhereClause $Where -Timestamp $timestamp -IncludeBackup (-not $NoBackup)
        $path = Resolve-OutputPath -OutputFile $OutputFile -Table $Table -Timestamp $stamp

        Write-SqlFile -Path $path -Content $sql
        Write-Ok "Script generado: $path"
        Write-Info 'Revisalo y ejecutalo contra el DESTINO. Solo actualiza; no inserta ni borra.'
    } finally {
        $cn.Close()
    }
}

# El guard permite dot-sourcear el script desde los tests sin ejecutar nada.
if ($MyInvocation.InvocationName -ne '.') { Invoke-Main }
