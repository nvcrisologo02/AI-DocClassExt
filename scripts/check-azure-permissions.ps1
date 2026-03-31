param(
    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup = "SRBRGDOCSAIPROD",

    [Parameter(Mandatory = $false)]
    [string]$KeyVaultName = "",

    [Parameter(Mandatory = $false)]
    [string]$SqlServerName = "srbsqlprodocai",

    [Parameter(Mandatory = $false)]
    [string]$FunctionAppName = "srbappprodocai",

    [Parameter(Mandatory = $false)]
    [string]$StorageDocuments = "srbstgprodocai",

    [Parameter(Mandatory = $false)]
    [string]$StorageDurable = "srbstgproapppdocai",

    [Parameter(Mandatory = $false)]
    [switch]$TestCreateSecret,

    [Parameter(Mandatory = $false)]
    [switch]$TestCreateDb,

    [Parameter(Mandatory = $false)]
    [string]$TestDbName = "DocumentIA-TestPermisos",

    [Parameter(Mandatory = $false)]
    [switch]$CleanupTestDb
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
}

function Get-JsonPayload {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    # Azure CLI can prepend warning/info lines that break ConvertFrom-Json.
    $lines = ($Text -replace "`r", "") -split "`n" |
        Where-Object {
            -not [string]::IsNullOrWhiteSpace($_) -and
            $_ -notmatch '^WARNING:' -and
            $_ -notmatch '^INFO:'
        }

    $clean = ($lines -join "`n").Trim()
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return $null
    }

    $firstObj = $clean.IndexOf('{')
    $firstArr = $clean.IndexOf('[')
    if ($firstObj -ge 0 -and $firstArr -ge 0) {
        $start = [Math]::Min($firstObj, $firstArr)
    }
    elseif ($firstObj -ge 0) {
        $start = $firstObj
    }
    elseif ($firstArr -ge 0) {
        $start = $firstArr
    }
    else {
        $start = 0
    }

    $lastObj = $clean.LastIndexOf('}')
    $lastArr = $clean.LastIndexOf(']')
    $end = [Math]::Max($lastObj, $lastArr)

    if ($end -ge $start -and $start -ge 0) {
        return $clean.Substring($start, ($end - $start + 1)).Trim()
    }

    return $clean
}

function Invoke-AzTsv {
    param([string[]]$AzArgs)
    $result = & az @AzArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($AzArgs -join ' ')`n$result"
    }
    return ($result | Out-String).Trim()
}

function Invoke-AzJson {
    param([string[]]$AzArgs)
    $result = & az @AzArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($AzArgs -join ' ')`n$result"
    }
    $text = ($result | Out-String)
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $null
    }

    $jsonPayload = Get-JsonPayload -Text $text
    if ([string]::IsNullOrWhiteSpace($jsonPayload)) {
        return $null
    }

    try {
        return $jsonPayload | ConvertFrom-Json
    }
    catch {
        throw "No se pudo parsear JSON de Azure CLI.`nSalida limpia:`n$jsonPayload`n`nSalida completa:`n$text"
    }
}

function Try-AzJson {
    param([string[]]$AzArgs)
    try {
        return Invoke-AzJson -AzArgs $AzArgs
    }
    catch {
        Write-Warning $_.Exception.Message
        return $null
    }
}

function Try-AzText {
    param([string[]]$AzArgs)
    try {
        return Invoke-AzTsv -AzArgs $AzArgs
    }
    catch {
        Write-Warning $_.Exception.Message
        return $null
    }
}

function Get-RoleAssignments {
    param(
        [string]$Assignee,
        [string]$Scope
    )

    $args = @(
        "role", "assignment", "list",
        "--assignee", $Assignee,
        "--scope", $Scope,
        "-o", "json"
    )
    return (Try-AzJson -AzArgs $args)
}

function Has-Role {
    param(
        [Object[]]$Assignments,
        [string[]]$RoleNames
    )

    if (-not $Assignments) { return $false }

    foreach ($a in $Assignments) {
        if ($RoleNames -contains $a.roleDefinitionName) {
            return $true
        }
    }
    return $false
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) no esta instalado o no esta en PATH."
}

Write-Section "Contexto Azure"
Try-AzText -AzArgs @("account", "set", "--subscription", $SubscriptionId) | Out-Null
$account = Invoke-AzJson -AzArgs @("account", "show", "-o", "json")
$me = Invoke-AzJson -AzArgs @("ad", "signed-in-user", "show", "-o", "json")

Write-Host "Suscripcion: $($account.name) ($($account.id))"
Write-Host "Tenant: $($account.tenantId)"
Write-Host "Usuario: $($me.userPrincipalName)"
Write-Host "ObjectId: $($me.id)"

$scopeRg = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"
$scopeKv = if ($KeyVaultName) { "$scopeRg/providers/Microsoft.KeyVault/vaults/$KeyVaultName" } else { $null }
$scopeSql = "$scopeRg/providers/Microsoft.Sql/servers/$SqlServerName"
$scopeStorageDocs = "$scopeRg/providers/Microsoft.Storage/storageAccounts/$StorageDocuments"
$scopeStorageDurable = "$scopeRg/providers/Microsoft.Storage/storageAccounts/$StorageDurable"

Write-Section "RBAC del usuario en Resource Group"
$rgAssignments = Get-RoleAssignments -Assignee $me.id -Scope $scopeRg
if ($rgAssignments) {
    $rgAssignments |
        Select-Object roleDefinitionName, scope, principalName |
        Format-Table -AutoSize
}
else {
    Write-Host "Sin asignaciones visibles en scope RG."
}

$canContributorRg = Has-Role -Assignments $rgAssignments -RoleNames @("Contributor", "Owner")
$canGrantRoles = Has-Role -Assignments $rgAssignments -RoleNames @("Owner", "User Access Administrator")

Write-Host ""
Write-Host ("Puede crear recursos en RG: {0}" -f ($(if ($canContributorRg) { "SI" } else { "NO" })))
Write-Host ("Puede asignar roles (RBAC): {0}" -f ($(if ($canGrantRoles) { "SI" } else { "NO" })))

Write-Section "Key Vault"
$keyVaultInfo = $null
$kvAssignments = $null
$kvCanManageSecrets = $false

if ([string]::IsNullOrWhiteSpace($KeyVaultName)) {
    Write-Host "No se proporciono -KeyVaultName. Se omite validacion de Key Vault."
}
else {
    $keyVaultInfo = Try-AzJson -AzArgs @(
        "keyvault", "show",
        "--name", $KeyVaultName,
        "--resource-group", $ResourceGroup,
        "-o", "json"
    )

    if ($keyVaultInfo) {
        $isRbac = [bool]$keyVaultInfo.properties.enableRbacAuthorization
        Write-Host "Vault: $($keyVaultInfo.name)"
        Write-Host "Modelo permisos: $(if ($isRbac) { 'RBAC' } else { 'Access Policies' })"

        if ($isRbac) {
            $kvAssignments = Get-RoleAssignments -Assignee $me.id -Scope $scopeKv
            if ($kvAssignments) {
                $kvAssignments |
                    Select-Object roleDefinitionName, scope, principalName |
                    Format-Table -AutoSize
            }

            $kvCanManageSecrets = Has-Role -Assignments $kvAssignments -RoleNames @("Key Vault Secrets Officer", "Key Vault Administrator", "Owner")
            Write-Host ("Puede crear/actualizar secretos (RBAC): {0}" -f ($(if ($kvCanManageSecrets) { "SI" } else { "NO" })))
        }
        else {
            $policies = $keyVaultInfo.properties.accessPolicies
            if ($policies) {
                $mine = $policies | Where-Object { $_.objectId -eq $me.id }
                if ($mine) {
                    $secretPerms = $mine.permissions.secrets
                    Write-Host "Access policy encontrada para tu usuario. Permisos secretos: $($secretPerms -join ', ')"
                    $kvCanManageSecrets = $secretPerms -contains "set"
                }
                else {
                    Write-Host "No hay access policy para tu objectId en este vault."
                }
            }
            else {
                Write-Host "Vault sin access policies configuradas."
            }

            Write-Host ("Puede crear/actualizar secretos (Access Policy): {0}" -f ($(if ($kvCanManageSecrets) { "SI" } else { "NO" })))
        }

        if ($TestCreateSecret) {
            Write-Host ""
            Write-Host "Probando creacion de secreto de test..."
            $testSecretName = "prueba-permisos-$(Get-Date -Format 'yyyyMMddHHmmss')"
            $setResult = Try-AzJson -AzArgs @(
                "keyvault", "secret", "set",
                "--vault-name", $KeyVaultName,
                "--name", $testSecretName,
                "--value", "test123",
                "-o", "json"
            )

            if ($setResult) {
                Write-Host "OK: secreto creado: $testSecretName"
                Try-AzJson -AzArgs @(
                    "keyvault", "secret", "delete",
                    "--vault-name", $KeyVaultName,
                    "--name", $testSecretName,
                    "-o", "json"
                ) | Out-Null
                Write-Host "Secreto de test eliminado."
            }
            else {
                Write-Host "No se pudo crear secreto de test."
            }
        }
    }
}

Write-Section "Azure SQL"
$sqlServer = Try-AzJson -AzArgs @(
    "sql", "server", "show",
    "--name", $SqlServerName,
    "--resource-group", $ResourceGroup,
    "-o", "json"
)

$sqlAssignments = Get-RoleAssignments -Assignee $me.id -Scope $scopeSql
if ($sqlAssignments) {
    $sqlAssignments |
        Select-Object roleDefinitionName, scope, principalName |
        Format-Table -AutoSize
}

$hasSqlScopeRole = Has-Role -Assignments $sqlAssignments -RoleNames @("Contributor", "SQL Server Contributor", "SQL DB Contributor", "Owner")
$hasRgScopeRole = Has-Role -Assignments $rgAssignments -RoleNames @("Contributor", "SQL Server Contributor", "SQL DB Contributor", "Owner")
$canCreateDbByRbac = $hasSqlScopeRole -or $hasRgScopeRole

if ($sqlServer) {
    Write-Host "Servidor SQL encontrado: $($sqlServer.name) ($($sqlServer.fullyQualifiedDomainName))"

    $dbs = Try-AzJson -AzArgs @(
        "sql", "db", "list",
        "--server", $SqlServerName,
        "--resource-group", $ResourceGroup,
        "-o", "json"
    )

    if ($dbs) {
        $dbs | Select-Object name, status, currentServiceObjectiveName | Format-Table -AutoSize
    }

    $adAdmin = Try-AzJson -AzArgs @(
        "sql", "server", "ad-COMPLETAR_GDC_HTTP_BASIC_USERNAME", "show",
        "--server", $SqlServerName,
        "--resource-group", $ResourceGroup,
        "-o", "json"
    )

    if ($adAdmin) {
        Write-Host "Entra COMPLETAR_GDC_HTTP_BASIC_USERNAME SQL: $($adAdmin.login)"
    }
    else {
        Write-Host "No se pudo leer Entra COMPLETAR_GDC_HTTP_BASIC_USERNAME SQL o no esta configurado."
    }
}
else {
    Write-Host "Servidor SQL no encontrado en RG indicado."
}

Write-Host ("Puede crear DB por RBAC: {0}" -f ($(if ($canCreateDbByRbac) { "SI" } else { "NO" })))

if ($TestCreateDb) {
    Write-Host ""
    Write-Host "Probando creacion de BD de test ($TestDbName)..."
    $createdDb = Try-AzJson -AzArgs @(
        "sql", "db", "create",
        "--resource-group", $ResourceGroup,
        "--server", $SqlServerName,
        "--name", $TestDbName,
        "--service-objective", "S0",
        "-o", "json"
    )

    if ($createdDb) {
        Write-Host "OK: BD de test creada: $TestDbName"
        if ($CleanupTestDb) {
            Try-AzText -AzArgs @(
                "sql", "db", "delete",
                "--resource-group", $ResourceGroup,
                "--server", $SqlServerName,
                "--name", $TestDbName,
                "--yes"
            ) | Out-Null
            Write-Host "BD de test eliminada."
        }
        else {
            Write-Host "No se elimina BD de test (faltaba -CleanupTestDb)."
        }
    }
    else {
        Write-Host "No se pudo crear la BD de test."
    }
}

Write-Section "Managed Identity de Function App"
$miPrincipalId = Try-AzText -AzArgs @(
    "functionapp", "identity", "show",
    "--name", $FunctionAppName,
    "--resource-group", $ResourceGroup,
    "--query", "principalId",
    "-o", "tsv"
)

if ([string]::IsNullOrWhiteSpace($miPrincipalId)) {
    Write-Host "No se pudo obtener principalId de MI para Function App $FunctionAppName."
}
else {
    Write-Host "MI principalId: $miPrincipalId"

    if ($scopeKv) {
        Write-Host ""
        Write-Host "Roles MI en Key Vault"
        $miKv = Get-RoleAssignments -Assignee $miPrincipalId -Scope $scopeKv
        if ($miKv) {
            $miKv | Select-Object roleDefinitionName, scope | Format-Table -AutoSize
        }
        else {
            Write-Host "Sin asignaciones MI en Key Vault."
        }
    }

    Write-Host ""
    Write-Host "Roles MI en Storage documentos"
    $miDocs = Get-RoleAssignments -Assignee $miPrincipalId -Scope $scopeStorageDocs
    if ($miDocs) {
        $miDocs | Select-Object roleDefinitionName, scope | Format-Table -AutoSize
    }
    else {
        Write-Host "Sin asignaciones MI en storage documentos."
    }

    Write-Host ""
    Write-Host "Roles MI en Storage durable"
    $miDur = Get-RoleAssignments -Assignee $miPrincipalId -Scope $scopeStorageDurable
    if ($miDur) {
        $miDur | Select-Object roleDefinitionName, scope | Format-Table -AutoSize
    }
    else {
        Write-Host "Sin asignaciones MI en storage durable."
    }

    Write-Host ""
    Write-Host "Roles MI en SQL server"
    $miSql = Get-RoleAssignments -Assignee $miPrincipalId -Scope $scopeSql
    if ($miSql) {
        $miSql | Select-Object roleDefinitionName, scope | Format-Table -AutoSize
    }
    else {
        Write-Host "Sin asignaciones MI en SQL server."
    }
}

Write-Section "Resumen"
$summary = [PSCustomObject]@{
    SubscriptionName = $account.name
    SubscriptionId = $account.id
    ResourceGroup = $ResourceGroup
    UserUPN = $me.userPrincipalName
    UserObjectId = $me.id
    CanCreateResourcesInRG = $canContributorRg
    CanAssignRbacRoles = $canGrantRoles
    KeyVaultName = $KeyVaultName
    CanManageSecrets = $kvCanManageSecrets
    SqlServerName = $SqlServerName
    CanCreateSqlDbByRbac = $canCreateDbByRbac
    FunctionAppName = $FunctionAppName
    FunctionAppMIPrincipalId = $miPrincipalId
}

$summary | Format-List

$reportPath = Join-Path -Path (Get-Location) -ChildPath "azure-permissions-report.json"
$summary | ConvertTo-Json -Depth 5 | Out-File -FilePath $reportPath -Encoding utf8
Write-Host ""
Write-Host "Reporte resumen guardado en: $reportPath" -ForegroundColor Green

Write-Host ""
Write-Host "Sugerencia de ejecucion:" -ForegroundColor Yellow
Write-Host "  .\scripts\check-azure-permissions.ps1 -SubscriptionId <SUBSCRIPTION_ID> -KeyVaultName <KEYVAULT_NAME>"
Write-Host ""
Write-Host "Para testear creacion real de secreto y BD:" -ForegroundColor Yellow
Write-Host "  .\scripts\check-azure-permissions.ps1 -SubscriptionId <SUBSCRIPTION_ID> -KeyVaultName <KEYVAULT_NAME> -TestCreateSecret -TestCreateDb -CleanupTestDb"
