<#
.SYNOPSIS
    Modulo comun de la pipeline Release de DocumentIA (hito 2).

.DESCRIPTION
    Funciones compartidas por los scripts de scripts/release/: tokenizado de argumentos sin
    evaluar, hash de contenido, plantilla SQL de embeddings, rama esperada por entorno, alias de
    entorno, deteccion del switch de simulacion, SQL con token de Entra y envoltura de az.
    Al importar solo se definen funciones; no hay efectos laterales.
#>

$script:UltimoExitCodeAz = 0

function ConvertTo-ReleaseArgumentList {
    <#
    .SYNOPSIS
        Tokeniza una cadena de argumentos por espacios respetando comillas simples y dobles.
    .DESCRIPTION
        Automata de tres estados (fuera, comilla simple, comilla doble). Las comillas se quitan
        del token. Tras tokenizar se sustituye <env> literalmente en cada token. No evalua nada.
        Los consumidores envuelven el resultado en @(...) para tratar siempre una lista.
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [AllowEmptyString()][AllowNull()][string]$Argumentos,
        [Parameter(Mandatory)][string]$Environment
    )
    $tokens = [System.Collections.Generic.List[string]]::new()
    if ([string]::IsNullOrWhiteSpace($Argumentos)) { return @() }
    $estado = 'fuera'
    $actual = [System.Text.StringBuilder]::new()
    $enToken = $false
    foreach ($c in $Argumentos.ToCharArray()) {
        switch ($estado) {
            'fuera' {
                if ($c -eq "'") { $estado = 'simple'; $enToken = $true }
                elseif ($c -eq '"') { $estado = 'doble'; $enToken = $true }
                elseif ([char]::IsWhiteSpace($c)) {
                    if ($enToken) { $tokens.Add($actual.ToString()); [void]$actual.Clear(); $enToken = $false }
                }
                else { [void]$actual.Append($c); $enToken = $true }
            }
            'simple' {
                if ($c -eq "'") { $estado = 'fuera' } else { [void]$actual.Append($c) }
            }
            'doble' {
                if ($c -eq '"') { $estado = 'fuera' } else { [void]$actual.Append($c) }
            }
        }
    }
    if ($estado -ne 'fuera') { throw "Argumentos con comillas sin cerrar: $Argumentos" }
    if ($enToken) { $tokens.Add($actual.ToString()) }
    $resultado = foreach ($t in $tokens) { $t.Replace('<env>', $Environment) }
    return @($resultado)
}

function Get-ReleaseContentHash {
    <#
    .SYNOPSIS
        SHA-256 en hexadecimal minuscula del texto en UTF-8 sin BOM.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Content)
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($Content)
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
}

function New-EmbeddingsSql {
    <#
    .SYNOPSIS
        Devuelve el SQL del 07 (embeddings) con @Modo y @Version sustituidos.
    .DESCRIPTION
        El 07 gobierna $.Modo y $.Restringido.Modo con el mismo @Modo; si se indica
        -RestringidoModo y difiere de -Modo, lanza. Lanza si falta alguno de los DECLARE.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][string]$TemplatePath,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$Modo,
        [string]$RestringidoModo
    )
    if ($PSBoundParameters.ContainsKey('RestringidoModo') -and $RestringidoModo -cne $Modo) {
        throw "restringidoModo '$RestringidoModo' debe coincidir con modo '$Modo': el 07 usa un solo @Modo."
    }
    if ($Modo -cnotmatch '^(off|sombra|hibrido)$') { throw "Modo no valido: '$Modo'" }
    if ($Version -cnotmatch '^v\d+$') { throw "Version no valida: '$Version'" }
    $sql = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $TemplatePath).Path)
    $reModo = "DECLARE @Modo NVARCHAR\(10\) = N'[^']*';"
    $reVersion = "DECLARE @Version NVARCHAR\(10\) = N'[^']*';"
    if ($sql -cnotmatch $reModo) { throw "La plantilla no contiene DECLARE @Modo: $TemplatePath" }
    if ($sql -cnotmatch $reVersion) { throw "La plantilla no contiene DECLARE @Version: $TemplatePath" }
    $sql = [regex]::Replace($sql, $reModo, { param($m) "DECLARE @Modo NVARCHAR(10) = N'$Modo';" })
    $sql = [regex]::Replace($sql, $reVersion, { param($m) "DECLARE @Version NVARCHAR(10) = N'$Version';" })
    return $sql
}

function Get-ReleaseBranchEsperada {
    <#
    .SYNOPSIS
        Rama que debe desplegarse en el entorno: pre -> develop, prod -> master, dev -> cualquiera ($null).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment)
    switch ($Environment) {
        'pre' { 'refs/heads/develop' }
        'prod' { 'refs/heads/master' }
        default { $null }
    }
}

function ConvertTo-EnvironmentAlias {
    <#
    .SYNOPSIS
        Alias de entorno para nombres de recursos: dev -> dev, pre -> pre, prod -> pro.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][ValidateSet('dev', 'pre', 'prod')][string]$Environment)
    if ($Environment -eq 'prod') { 'pro' } else { $Environment }
}

function Get-DryRunSwitchName {
    <#
    .SYNOPSIS
        Devuelve 'DryRun' o 'WhatIf' segun lo que admita el script; lanza si ninguno.
    .DESCRIPTION
        La deteccion replica la de Test-ReleaseManifest.ps1 (patron de [switch]$DryRun|$WhatIf
        o SupportsShouldProcess sobre el texto del script).
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param([Parameter(Mandatory)][string]$ScriptPath)
    # Replica de la deteccion de Test-ReleaseManifest.ps1 (linea del param DryRun/WhatIf); no se refactoriza ese script a proposito
    $contenido = Get-Content -LiteralPath $ScriptPath -Raw
    if ($contenido -match '(?i)\[switch\]\s*\$DryRun\b') { return 'DryRun' }
    if ($contenido -match '(?i)\[switch\]\s*\$WhatIf\b') { return 'WhatIf' }
    if ($contenido -match '(?i)SupportsShouldProcess') { return 'WhatIf' }
    throw "El script '$ScriptPath' no admite -DryRun ni -WhatIf."
}

function Get-ReleaseSqlConnection {
    <#
    .SYNOPSIS
        Crea (sin abrir) una conexion a Azure SQL autenticada con token de Entra obtenido con az.
    #>
    [CmdletBinding()]
    [OutputType([System.Data.SqlClient.SqlConnection])]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database
    )
    if ($Server -notmatch '\.') { $Server = "$Server.database.windows.net" }
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $token = (& az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null)
    $codigo = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($codigo -ne 0 -or [string]::IsNullOrWhiteSpace($token)) { throw 'No se pudo obtener el token de Azure SQL con az.' }
    $conn = [System.Data.SqlClient.SqlConnection]::new("Server=tcp:$Server,1433;Database=$Database;Encrypt=True;TrustServerCertificate=False;")
    $conn.AccessToken = ($token | Select-Object -First 1).Trim()
    return $conn
}

function Invoke-ReleaseSql {
    <#
    .SYNOPSIS
        Ejecuta SQL en Azure SQL con token de Entra (az). Devuelve un DataTable o, con -Scalar, un escalar.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Query,
        [hashtable]$Parameters,
        [switch]$Scalar
    )
    $conn = Get-ReleaseSqlConnection -Server $Server -Database $Database
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Query
        $cmd.CommandTimeout = 600
        if ($Parameters) {
            foreach ($k in $Parameters.Keys) {
                $v = $Parameters[$k]
                if ($null -eq $v) { $v = [System.DBNull]::Value }
                [void]$cmd.Parameters.AddWithValue("@$k", $v)
            }
        }
        if ($Scalar) { return $cmd.ExecuteScalar() }
        $tabla = [System.Data.DataTable]::new()
        $reader = $cmd.ExecuteReader()
        try { $tabla.Load($reader) } finally { $reader.Dispose() }
        return , $tabla
    }
    finally { $conn.Dispose() }
}

function Invoke-ReleaseSqlTransaccion {
    <#
    .SYNOPSIS
        Ejecuta una lista de sentencias en orden, en una sola conexion y una sola transaccion.
    .DESCRIPTION
        Devuelve [int[]] con las filas afectadas de cada sentencia, en el mismo orden. Ante
        cualquier error hace ROLLBACK y relanza. CommandTimeout 600.
    #>
    [CmdletBinding()]
    [OutputType([int[]])]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string[]]$Queries
    )
    $conn = Get-ReleaseSqlConnection -Server $Server -Database $Database
    try {
        $conn.Open()
        $tx = $conn.BeginTransaction()
        $filas = [System.Collections.Generic.List[int]]::new()
        try {
            foreach ($q in $Queries) {
                $cmd = $conn.CreateCommand()
                $cmd.Transaction = $tx
                $cmd.CommandText = $q
                $cmd.CommandTimeout = 600
                $filas.Add([int]$cmd.ExecuteNonQuery())
            }
            $tx.Commit()
        }
        catch {
            try { $tx.Rollback() } catch { Write-Verbose "Rollback fallido: $($_.Exception.Message)" }
            throw
        }
        return , ([int[]]$filas.ToArray())
    }
    finally { $conn.Dispose() }
}

function New-ReleaseRestoreSql {
    <#
    .SYNOPSIS
        Genera el SQL que devuelve una tabla al estado de su copia (foto) en dos pasadas.
    .DESCRIPTION
        Pasada1Update: UPDATE de las filas con pareja por clave primaria cuyos valores difieren (EXCEPT
        trata NULL como igual); vacio si no hay columnas fuera de la clave. Pasada1Insert: INSERT de las
        filas de la copia sin pareja (con SET IDENTITY_INSERT ON/OFF si TieneIdentity). Pasada2Delete:
        DELETE de las filas de la tabla sin pareja en la copia. Pasada1 = Update + Insert; Pasada2 = Delete.
        Los identificadores van entre corchetes y los nombres se validan con ^[A-Za-z0-9_]+$. No usa MERGE.
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)][string]$Tabla,
        [Parameter(Mandatory)][string]$Copia,
        [Parameter(Mandatory)][string[]]$ClavePrimaria,
        [Parameter(Mandatory)][string[]]$Columnas,
        [Parameter(Mandatory)][bool]$TieneIdentity
    )
    foreach ($n in @($Tabla, $Copia) + @($ClavePrimaria) + @($Columnas)) {
        if ($n -cnotmatch '^[A-Za-z0-9_]+$') { throw "Nombre de objeto no valido: '$n'" }
    }
    $t = "[dbo].[$Tabla]"
    $f = "[dbo].[$Copia]"
    $cond = ($ClavePrimaria | ForEach-Object { "t.[$_] = f.[$_]" }) -join ' AND '
    $condInv = ($ClavePrimaria | ForEach-Object { "f.[$_] = t.[$_]" }) -join ' AND '
    $noClave = @($Columnas | Where-Object { $ClavePrimaria -cnotcontains $_ })
    $nl = [Environment]::NewLine

    $update = ''
    if ($noClave.Count -gt 0) {
        $set = ($noClave | ForEach-Object { "[$_] = f.[$_]" }) -join ', '
        $colsT = ($noClave | ForEach-Object { "t.[$_]" }) -join ', '
        $colsF = ($noClave | ForEach-Object { "f.[$_]" }) -join ', '
        $update = "-- Pasada 1: $Tabla (UPDATE)$nl" +
            "UPDATE t SET $set FROM $t t JOIN $f f ON $cond WHERE EXISTS (SELECT $colsT EXCEPT SELECT $colsF);"
    }

    $lista = ($Columnas | ForEach-Object { "[$_]" }) -join ', '
    $insert = "-- Pasada 1: $Tabla (INSERT)$nl"
    if ($TieneIdentity) { $insert += "SET IDENTITY_INSERT $t ON;$nl" }
    $insert += "INSERT INTO $t ($lista) SELECT $lista FROM $f f WHERE NOT EXISTS (SELECT 1 FROM $t t WHERE $cond);"
    if ($TieneIdentity) { $insert += "$($nl)SET IDENTITY_INSERT $t OFF;" }

    $delete = "-- Pasada 2: $Tabla$nl" +
        "DELETE t FROM $t t WHERE NOT EXISTS (SELECT 1 FROM $f f WHERE $condInv);"

    $pasada1 = if ($update) { "$update$nl$insert" } else { $insert }
    return @{
        Pasada1Update = $update
        Pasada1Insert = $insert
        Pasada2Delete = $delete
        Pasada1       = $pasada1
        Pasada2       = $delete
    }
}

function ConvertTo-ReleaseAzArgumento {
    <#
    .SYNOPSIS
        Prepara un argumento para az en Windows: az.cmd rompe con ( ) & | < > ^ si el argumento no lleva espacios.
    .DESCRIPTION
        PowerShell solo entrecomilla los argumentos con espacios. Los que llevan alguno de ( ) & | < > ^
        y no llevan espacios ni empiezan por comilla doble llegan sin comillas al cmd.exe de az.cmd y lo
        rompen ("-o was unexpected at this time", codigo 255). Se envuelven en comillas dobles literales.
        EsWindows permite probar la rama sin depender del sistema; por defecto es $IsWindows.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Argumento,
        [bool]$EsWindows = $IsWindows
    )
    if (-not $EsWindows) { return $Argumento }
    if ($Argumento.StartsWith('"') -or $Argumento -match '\s') { return $Argumento }
    if ($Argumento -match '[()&|<>^]') { return ('"' + $Argumento + '"') }
    return $Argumento
}

function Invoke-ReleaseAz {
    <#
    .SYNOPSIS
        Unica envoltura de az: ejecuta, parsea la salida JSON y devuelve el objeto o $null.
    .DESCRIPTION
        Anade -o json si el llamador no paso -o/--output. Devuelve $null con salida vacia o fallo.
        Los consumidores comprueban $LASTEXITCODE tras la llamada (los mocks de Pester lo fijan
        con $global:LASTEXITCODE); ademas queda en $script:UltimoExitCodeAz (Get-ReleaseAzUltimoExitCode).
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string[]]$Arguments)
    $args2 = @($Arguments)
    if (-not ($args2 | Where-Object { $_ -ceq '-o' -or $_ -ceq '--output' })) { $args2 += @('-o', 'json') }
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $args3 = @($args2 | ForEach-Object { ConvertTo-ReleaseAzArgumento -Argumento ([string]$_) })
    $salida = & az @args3 2>&1
    $script:UltimoExitCodeAz = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($script:UltimoExitCodeAz -ne 0) {
        # Diagnostico: solo grupo y subcomando (tres primeros argumentos), nunca valores.
        $prefijo = (@($Arguments | Select-Object -First 3) -join ' ')
        $lineas = @($salida | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { ("$_" -split "`r?`n")[0] })
        $msg = @($lineas | Where-Object { $_ -match 'ERROR' } | Select-Object -First 1)[0]
        if ([string]::IsNullOrWhiteSpace($msg)) { $msg = @($lineas | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1)[0] }
        $msg = if ($null -eq $msg) { '' } else { ([string]$msg).Trim() }
        if ($msg.Length -gt 300) { $msg = $msg.Substring(0, 300) }
        Write-Host "az $prefijo fallo (codigo $($script:UltimoExitCodeAz)): $msg"
        return $null
    }
    $texto = (@($salida | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    try { return ($texto | ConvertFrom-Json) } catch { return $texto }
}

function Get-ReleaseKuduToken {
    <#
    .SYNOPSIS
        Token de ARM de la cuenta activa de az, para autenticar contra Kudu (scm) con Bearer.
    .DESCRIPTION
        La autenticacion basica de SCM esta deshabilitada y no se reactiva. Con TF_BUILD='True'
        marca el token como secreto (##vso[task.setsecret]) antes de devolverlo. Devuelve $null
        si az falla o la salida esta vacia. Nunca escribe el token salvo en el comando setsecret.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $salida = & az account get-access-token --resource https://management.azure.com --query accessToken -o tsv 2>$null
    $codigo = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($codigo -ne 0) { return $null }
    $token = (@($salida) -join '').Trim()
    if ([string]::IsNullOrWhiteSpace($token)) { return $null }
    if ($env:TF_BUILD -eq 'True') { Write-Host "##vso[task.setsecret]$token" }
    return $token
}

function Get-ReleaseAzUltimoExitCode {
    <#
    .SYNOPSIS
        Codigo de salida de la ultima llamada a Invoke-ReleaseAz.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param()
    return $script:UltimoExitCodeAz
}

Export-ModuleMember -Function ConvertTo-ReleaseArgumentList, Get-ReleaseContentHash, New-EmbeddingsSql,
    Get-ReleaseBranchEsperada, ConvertTo-EnvironmentAlias, Get-DryRunSwitchName, Invoke-ReleaseSql,
    Invoke-ReleaseSqlTransaccion, New-ReleaseRestoreSql, Invoke-ReleaseAz, ConvertTo-ReleaseAzArgumento, Get-ReleaseAzUltimoExitCode, Get-ReleaseKuduToken
