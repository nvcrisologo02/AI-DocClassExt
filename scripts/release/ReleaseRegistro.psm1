# Registro de pasos de release aplicados (tabla dbo.ReleaseAplicada). AB#101583.
# Depende de Invoke-ReleaseSql (ReleaseCommon.psm1). Todo el SQL va parametrizado.
if (-not (Get-Module ReleaseCommon)) { Import-Module (Join-Path $PSScriptRoot 'ReleaseCommon.psm1') }

function Test-ReleaseTablaInexistente {
    <#
    .SYNOPSIS
        Indica si la excepcion (o alguna interna) es el error 208 de SQL Server (objeto inexistente).
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param([Parameter(Mandatory)]$Excepcion)
    $e = $Excepcion
    while ($null -ne $e) {
        if ($e -is [System.Data.SqlClient.SqlException] -and $e.Number -eq 208) { return $true }
        if ($e.Message -match 'Invalid object name') { return $true }
        $e = $e.InnerException
    }
    return $false
}

function Get-ReleaseEstado {
    <#
    .SYNOPSIS
        Lee el estado de una version en dbo.ReleaseAplicada: SinAplicar, Parcial o Cerrada.
    .DESCRIPTION
        Solo cuentan las filas con Resultado = 'Aplicado'. Cerrada si existe la fila Paso = '__cierre__'.
        Con la tabla inexistente (error 208) o con -IgnorarRegistro devuelve SinAplicar sin pasos.
        -IgnorarRegistro no consulta la base de datos.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Version,
        [switch]$IgnorarRegistro
    )
    $vacio = [pscustomobject]@{ Estado = 'SinAplicar'; PasosAplicados = [object[]]@() }
    if ($IgnorarRegistro) { return $vacio }
    try {
        $tabla = Invoke-ReleaseSql -Server $Server -Database $Database -Parameters @{ Version = $Version } -Query `
            'SELECT Paso, HashContenido, Resultado, FechaUtc FROM dbo.ReleaseAplicada WHERE Version = @Version'
    }
    catch {
        if (Test-ReleaseTablaInexistente -Excepcion $_.Exception) { return $vacio }
        throw
    }
    $pasos = @()
    $cerrada = $false
    foreach ($fila in $tabla) {
        if ($fila.Resultado -ne 'Aplicado') { continue }
        if ($fila.Paso -eq '__cierre__') { $cerrada = $true; continue }
        $pasos += [pscustomobject]@{ Paso = $fila.Paso; HashContenido = $fila.HashContenido; FechaUtc = $fila.FechaUtc }
    }
    $estado = if ($cerrada) { 'Cerrada' } elseif ($pasos.Count -gt 0) { 'Parcial' } else { 'SinAplicar' }
    return [pscustomobject]@{ Estado = $estado; PasosAplicados = [object[]]$pasos }
}

function Register-ReleasePaso {
    <#
    .SYNOPSIS
        Inserta una fila en dbo.ReleaseAplicada para un paso de la release (FechaUtc = SYSUTCDATETIME()).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$Paso,
        [Parameter(Mandatory)][AllowEmptyString()][string]$HashContenido,
        [Parameter(Mandatory)][string]$BuildId,
        [string]$Resultado = 'Aplicado'
    )
    $query = 'INSERT INTO dbo.ReleaseAplicada (Version, Paso, HashContenido, Resultado, BuildId, FechaUtc) ' +
    'VALUES (@Version, @Paso, @HashContenido, @Resultado, @BuildId, SYSUTCDATETIME())'
    $null = Invoke-ReleaseSql -Server $Server -Database $Database -Query $query -Parameters @{
        Version = $Version; Paso = $Paso; HashContenido = $HashContenido; Resultado = $Resultado; BuildId = $BuildId
    }
}

function Register-ReleaseCierre {
    <#
    .SYNOPSIS
        Registra la fila de cierre de la release (Paso = '__cierre__', hash vacio).
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$BuildId
    )
    Register-ReleasePaso -Server $Server -Database $Database -Version $Version -Paso '__cierre__' -HashContenido '' -BuildId $BuildId
}

function Set-ReleaseRevertida {
    <#
    .SYNOPSIS
        Marca como Revertido las filas Aplicado de un BuildId. Devuelve el numero de filas afectadas.
    #>
    [CmdletBinding()]
    [OutputType([int])]
    param(
        [Parameter(Mandatory)][string]$Server,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$BuildId
    )
    $query = "UPDATE dbo.ReleaseAplicada SET Resultado = 'Revertido' WHERE BuildId = @BuildId AND Resultado = 'Aplicado'; SELECT @@ROWCOUNT"
    return [int](Invoke-ReleaseSql -Server $Server -Database $Database -Query $query -Parameters @{ BuildId = $BuildId } -Scalar)
}

Export-ModuleMember -Function Get-ReleaseEstado, Register-ReleasePaso, Register-ReleaseCierre, Set-ReleaseRevertida
