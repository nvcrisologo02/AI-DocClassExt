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
    $nombre = ($SqlServer -split '\.')[0]
    if ($nombre -ne $script:ServidorDevEsperado) {
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
