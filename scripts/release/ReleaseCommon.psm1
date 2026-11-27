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
    $contenido = Get-Content -LiteralPath $ScriptPath -Raw
    if ($contenido -match '(?i)\[switch\]\s*\$DryRun\b') { return 'DryRun' }
    if ($contenido -match '(?i)\[switch\]\s*\$WhatIf\b') { return 'WhatIf' }
    if ($contenido -match '(?i)SupportsShouldProcess') { return 'WhatIf' }
    throw "El script '$ScriptPath' no admite -DryRun ni -WhatIf."
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
    if ($Server -notmatch '\.') { $Server = "$Server.database.windows.net" }
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $token = (& az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv 2>$null)
    $codigo = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($codigo -ne 0 -or [string]::IsNullOrWhiteSpace($token)) { throw 'No se pudo obtener el token de Azure SQL con az.' }
    $conn = [System.Data.SqlClient.SqlConnection]::new("Server=tcp:$Server,1433;Database=$Database;Encrypt=True;TrustServerCertificate=False;")
    $conn.AccessToken = ($token | Select-Object -First 1).Trim()
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
    $salida = & az @args2 2>&1
    $script:UltimoExitCodeAz = $LASTEXITCODE
    $ErrorActionPreference = $prev
    if ($script:UltimoExitCodeAz -ne 0) { return $null }
    $texto = (@($salida | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] }) -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    try { return ($texto | ConvertFrom-Json) } catch { return $texto }
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
    Invoke-ReleaseAz, Get-ReleaseAzUltimoExitCode
