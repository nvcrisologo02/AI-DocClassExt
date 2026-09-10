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

# Lista blanca de columnas mutables. Cualquier otra se rechaza: el bloque
# "mutacion" viene de un fichero JSON y no debe poder construir SQL arbitrario.
$script:ColumnasMutables = @("MarkdownPaginas", "MarkdownCompleto", "Paginas")

function Get-Sha256DeFichero {
    param([Parameter(Mandatory = $true)][string]$Ruta)

    if (-not (Test-Path -Path $Ruta)) { throw "No existe el fichero: $Ruta" }
    $sha    = [System.Security.Cryptography.SHA256]::Create()
    $stream = $null
    try {
        $stream = [System.IO.File]::OpenRead((Resolve-Path -Path $Ruta).Path)
        return ([System.BitConverter]::ToString($sha.ComputeHash($stream)) -replace '-', '').ToUpperInvariant()
    }
    finally {
        if ($null -ne $stream) { $stream.Dispose() }
        $sha.Dispose()
    }
}

function New-MutacionSql {
    param([hashtable]$Mutacion)

    if ($null -eq $Mutacion -or $Mutacion.Keys.Count -eq 0) { return $null }

    $asignaciones = @()
    foreach ($columna in $Mutacion.Keys) {
        if ($script:ColumnasMutables -notcontains $columna) {
            throw "Columna '$columna' no admitida en mutacion. Admitidas: $($script:ColumnasMutables -join ', ')"
        }
        $asignaciones += "$columna = @$columna"
    }
    return "UPDATE Documentos SET $($asignaciones -join ', ') WHERE SHA256 = @sha"
}

function Invoke-DocumentoMutacion {
    param(
        # AllowNull sobre el tipo fuerte: la guarda de Sha256 de abajo debe poder
        # probarse sin abrir conexion real, y un parametro Mandatory sin AllowNull
        # rechaza $null en el enlazado antes de que el cuerpo de la funcion se
        # ejecute. AllowNull permite pasar $null en el test sin renunciar a que
        # cualquier otro tipo distinto de SqlConnection siga rechazandose ahi mismo.
        [Parameter(Mandatory = $true)][AllowNull()][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256,
        [hashtable]$Mutacion
    )

    if ([string]::IsNullOrWhiteSpace($Sha256)) {
        throw "Invoke-DocumentoMutacion: Sha256 vacio o en blanco no admitido"
    }

    $sql = New-MutacionSql -Mutacion $Mutacion
    if ($null -eq $sql) { return 0 }

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    foreach ($columna in $Mutacion.Keys) {
        $valor = $Mutacion[$columna]
        if ($null -eq $valor) { $valor = [System.DBNull]::Value }
        [void]$cmd.Parameters.AddWithValue("@$columna", $valor)
    }
    return $cmd.ExecuteNonQuery()
}

function Remove-DocumentoPorSha256 {
    param(
        # AllowNull sobre el tipo fuerte, mismo motivo que en Invoke-DocumentoMutacion.
        [Parameter(Mandatory = $true)][AllowNull()][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256
    )

    if ([string]::IsNullOrWhiteSpace($Sha256)) {
        throw "Remove-DocumentoPorSha256: Sha256 vacio o en blanco no admitido"
    }

    # El borrado en cascada configurado en DocumentIADbContext arrastra
    # Ejecuciones y sus postprocesos y validaciones.
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = "DELETE FROM Documentos WHERE SHA256 = @sha"
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    return $cmd.ExecuteNonQuery()
}

# Rutas del JSON del contrato de la ultima ejecucion que un caso puede mutar. Una sola:
# el backfill decide por OrigenMarkdown y no hay otro motivo para tocar ese JSON.
# Clave = ruta sin el prefijo "$.", valor = nombre del parametro SQL.
$script:RutasJsonMutables = @{ "DetalleEjecucion.OrigenMarkdown" = "OrigenMarkdown" }

function New-MutacionEjecucionSql {
    param([hashtable]$Mutacion)

    if ($null -eq $Mutacion -or $Mutacion.Keys.Count -eq 0) { return $null }

    $expresion = "e.ContratoSalidaCompletoJson"
    foreach ($ruta in $Mutacion.Keys) {
        if (-not $script:RutasJsonMutables.ContainsKey($ruta)) {
            throw "Ruta JSON '$ruta' no admitida en mutacionEjecucion. Admitidas: $($script:RutasJsonMutables.Keys -join ', ')"
        }
        $parametro = $script:RutasJsonMutables[$ruta]
        $expresion = "JSON_MODIFY($expresion, '`$.$ruta', @$parametro)"
    }

    # Misma definicion de "ultima ejecucion" que el backfill: TOP 1 por FechaEjecucion DESC.
    return @"
UPDATE e
SET e.ContratoSalidaCompletoJson = $expresion
FROM dbo.DocumentoEjecuciones e
WHERE e.Id = (
    SELECT TOP 1 e2.Id
    FROM dbo.DocumentoEjecuciones e2
    INNER JOIN dbo.Documentos d ON d.Id = e2.DocumentoId
    WHERE d.SHA256 = @sha
    ORDER BY e2.FechaEjecucion DESC
)
"@
}

function Invoke-EjecucionMutacion {
    param(
        [Parameter(Mandatory = $true)][AllowNull()][System.Data.SqlClient.SqlConnection]$Connection,
        [Parameter(Mandatory = $true)][string]$Sha256,
        [hashtable]$Mutacion
    )

    if ([string]::IsNullOrWhiteSpace($Sha256)) { throw "Invoke-EjecucionMutacion: Sha256 vacio o en blanco no admitido" }
    $sql = New-MutacionEjecucionSql -Mutacion $Mutacion
    if ($null -eq $sql) { return 0 }

    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue("@sha", $Sha256)
    foreach ($ruta in $Mutacion.Keys) {
        $valor = $Mutacion[$ruta]
        if ($null -eq $valor) { $valor = [System.DBNull]::Value }
        [void]$cmd.Parameters.AddWithValue("@$($script:RutasJsonMutables[$ruta])", $valor)
    }
    return $cmd.ExecuteNonQuery()
}

function Test-DbAssertions {
    param(
        [Parameter(Mandatory = $true)][pscustomobject]$Antes,
        [Parameter(Mandatory = $true)][pscustomobject]$Despues,
        [array]$Assertions = @()
    )

    # Acceso a claves opcionales sin dot-notation directa: con Set-StrictMode -Version
    # Latest (activo en este fichero desde la Tarea 1), $regla.claveQueNoExiste lanza
    # PropertyNotFoundException en vez de devolver null. Estas funciones evitan ese
    # lanzamiento tanto para hashtables (@{...}) como para pscustomobject.
    #
    # Se exponen presencia y valor por separado: una clave presente con valor $null
    # (por ejemplo "esperado = $null", que asevera que la columna quedo en NULL) no es
    # lo mismo que la clave ausente (que no asevera nada sobre esa columna).
    function Test-ReglaTieneClave {
        param($Regla, [string]$Nombre)
        if ($Regla -is [System.Collections.IDictionary]) {
            return $Regla.ContainsKey($Nombre)
        }
        return (@($Regla.PSObject.Properties.Name) -contains $Nombre)
    }

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

        $tieneEsperado    = Test-ReglaTieneClave -Regla $regla -Nombre "esperado"
        $tieneNoDisminuye = Test-ReglaTieneClave -Regla $regla -Nombre "noDisminuye"
        $tieneNoEncoge    = Test-ReglaTieneClave -Regla $regla -Nombre "noEncoge"
        $tieneNoEsNulo    = Test-ReglaTieneClave -Regla $regla -Nombre "noEsNulo"

        $esperado    = Get-ValorRegla -Regla $regla -Nombre "esperado"
        $noDisminuye = Get-ValorRegla -Regla $regla -Nombre "noDisminuye"
        $noEncoge    = Get-ValorRegla -Regla $regla -Nombre "noEncoge"
        $noEsNulo    = Get-ValorRegla -Regla $regla -Nombre "noEsNulo"

        if ($tieneNoEsNulo -and [bool]$noEsNulo -and $null -eq $valorDespues) {
            $errores += "$columna es nulo y no deberia serlo"
            continue
        }

        if ($tieneEsperado) {
            if ([string]$valorDespues -ne [string]$esperado) {
                $errores += "$columna='$valorDespues' esperado='$esperado'"
            }
        }

        if ($tieneNoDisminuye -and [bool]$noDisminuye) {
            if ($null -eq $valorDespues) {
                $errores += "$columna paso a nulo y la regla es noDisminuye"
            }
            elseif ($null -eq $valorAntes) {
                $errores += "no hay base de comparacion para ${columna}: el valor previo era nulo"
            }
            elseif ([int]$valorDespues -lt [int]$valorAntes) {
                $errores += "$columna bajo de $valorAntes a $valorDespues"
            }
        }

        if ($tieneNoEncoge -and [bool]$noEncoge) {
            if ($null -eq $valorDespues) {
                $errores += "$columna paso a nulo y la regla es noEncoge"
            }
            elseif ($null -eq $valorAntes) {
                $errores += "no hay base de comparacion para ${columna}: el valor previo era nulo"
            }
            elseif ([int]$valorDespues -lt [int]$valorAntes) {
                $errores += "$columna encogio de $valorAntes a $valorDespues"
            }
        }
    }

    return [pscustomobject]@{ Success = ($errores.Count -eq 0); Errors = $errores }
}
