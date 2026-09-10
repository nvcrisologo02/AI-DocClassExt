Set-StrictMode -Version Latest

# Unico servidor SQL admitido. El ValidateSet del runner protege el parametro;
# esta comprobacion protege la configuracion, que vive en un environments.json
# gitignored y editado a mano. Este modulo emite DELETE.
$script:ServidorDevEsperado = "srbsqldevdocai"

function Assert-DbServidorEsDev {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$SqlServer)

    if ([string]::IsNullOrWhiteSpace($SqlServer)) {
        throw "sqlServer vacio en environments.json. Este juego de pruebas solo opera contra DEV."
    }
    $valor = $SqlServer.Trim()
    $fqdnEsperado = "$script:ServidorDevEsperado.database.windows.net"
    if ($valor -ne $script:ServidorDevEsperado -and $valor -ne $fqdnEsperado) {
        throw "Servidor '$SqlServer' no es el de DEV ($script:ServidorDevEsperado). Abortado: este script emite DELETE."
    }
}

function Connect-DocumentIADb {
    param(
        [Parameter(Mandatory = $true)][string]$SqlServer,
        [Parameter(Mandatory = $true)][string]$SqlDatabase
    )

    Assert-DbServidorEsDev -SqlServer $SqlServer

    $token = (az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv)
    if ([string]::IsNullOrWhiteSpace($token)) {
        throw "No se obtuvo token de az. Ejecuta 'az login' desde red corporativa."
    }

    $fqdn = if ($SqlServer -like "*.database.windows.net") { $SqlServer } else { "$SqlServer.database.windows.net" }
    $cn = New-Object System.Data.SqlClient.SqlConnection
    $cn.ConnectionString = "Server=tcp:$fqdn,1433;Database=$SqlDatabase;Encrypt=True;TrustServerCertificate=False;Connect Timeout=30;"
    $cn.AccessToken = $token
    $cn.Open()
    return $cn
}

function Get-DocumentoSnapshot {
    param(
        [Parameter(Mandatory = $true)][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256
    )

    $sql = @"
SELECT TOP 1
    MarkdownPaginas,
    CAST(MarkdownCompleto AS int)                          AS MarkdownCompleto,
    ISNULL(DATALENGTH(NormalizacionMarkdownGzip), 0)       AS LongitudGzip,
    ISNULL(DATALENGTH(NormalizacionMarkdownCompressed), 0) AS LongitudCompressed
FROM Documentos
WHERE SHA256 = @sha
"@

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    $lector = $cmd.ExecuteReader()
    try {
        if (-not $lector.Read()) {
            return [pscustomobject]@{
                Existe = $false; MarkdownPaginas = $null; MarkdownCompleto = $false
                LongitudGzip = 0; LongitudCompressed = 0
            }
        }
        $paginas = if ($lector["MarkdownPaginas"] -is [System.DBNull]) { $null } else { [int]$lector["MarkdownPaginas"] }
        return [pscustomobject]@{
            Existe             = $true
            MarkdownPaginas    = $paginas
            MarkdownCompleto   = ([int]$lector["MarkdownCompleto"] -eq 1)
            LongitudGzip       = [int]$lector["LongitudGzip"]
            LongitudCompressed = [int]$lector["LongitudCompressed"]
        }
    }
    finally { $lector.Close() }
}

function Test-DbAssertions {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Antes,
        [Parameter(Mandatory = $true)][pscustomobject]$Despues,
        [array]$Assertions = @()
    )

    # Acceso a claves opcionales sin dot-notation directa: con Set-StrictMode -Version
    # Latest (activo en este fichero desde la Tarea 1), $regla.claveQueNoExiste lanza
    # PropertyNotFoundException en vez de devolver null. Esta funcion evita ese lanzamiento
    # tanto para hashtables (@{...}) como para pscustomobject.
    function Get-ValorRegla {
        param($Regla, [string]$Nombre)
        if ($Regla -is [System.Collections.IDictionary]) {
            if ($Regla.ContainsKey($Nombre)) { return $Regla[$Nombre] }
            return $null
        }
        if (@($Regla.PSObject.Properties.Name) -contains $Nombre) { return $Regla.$Nombre }
        return $null
    }

    $errores = @()

    if (-not $Despues.Existe) {
        return [pscustomobject]@{
            Success = $false
            Errors  = @("la fila no existe en Documentos al terminar las pasadas")
        }
    }

    foreach ($regla in @($Assertions)) {
        if ($null -eq $regla) { continue }
        $columna = [string](Get-ValorRegla -Regla $regla -Nombre "columna")
        if ([string]::IsNullOrWhiteSpace($columna)) { continue }

        $valorDespues = $Despues.$columna
        $valorAntes   = $Antes.$columna

        $esperado    = Get-ValorRegla -Regla $regla -Nombre "esperado"
        $noDisminuye = Get-ValorRegla -Regla $regla -Nombre "noDisminuye"
        $noEncoge    = Get-ValorRegla -Regla $regla -Nombre "noEncoge"
        $noEsNulo    = Get-ValorRegla -Regla $regla -Nombre "noEsNulo"

        if ($null -ne $noEsNulo -and [bool]$noEsNulo -and $null -eq $valorDespues) {
            $errores += "$columna es nulo y no deberia serlo"
            continue
        }

        if ($null -ne $esperado) {
            if ([string]$valorDespues -ne [string]$esperado) {
                $errores += "$columna='$valorDespues' esperado='$esperado'"
            }
        }

        if ($null -ne $noDisminuye -and [bool]$noDisminuye) {
            if ($null -eq $valorDespues) {
                $errores += "$columna paso a nulo y la regla es noDisminuye"
            }
            elseif ($null -ne $valorAntes -and [int]$valorDespues -lt [int]$valorAntes) {
                $errores += "$columna bajo de $valorAntes a $valorDespues"
            }
        }

        if ($null -ne $noEncoge -and [bool]$noEncoge) {
            if ($null -eq $valorDespues) {
                $errores += "$columna paso a nulo y la regla es noEncoge"
            }
            elseif ($null -ne $valorAntes -and [int]$valorDespues -lt [int]$valorAntes) {
                $errores += "$columna encogio de $valorAntes a $valorDespues"
            }
        }
    }

    return [pscustomobject]@{ Success = ($errores.Count -eq 0); Errors = $errores }
}
