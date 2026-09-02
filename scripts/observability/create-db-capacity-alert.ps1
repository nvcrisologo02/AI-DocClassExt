<#
.SYNOPSIS
    Crea (o actualiza) la alerta de capacidad de la base de datos DocumentIA. AB#100171.

.DESCRIPTION
    Metric alert sobre storage_percent de la BD: avisa cuando el almacenamiento supera el
    umbral, para no repetir el episodio de agosto de 2026, en que la BD se lleno contra su
    limite de 2 GB sin aviso previo y hubo que ampliar de urgencia.

    Es distinta de las reglas de create-monitor-alerts.ps1: aquellas son scheduled query
    rules sobre Application Insights; esta es una metric alert de plataforma sobre el
    recurso SQL, que usa otro comando de az. Por eso vive en su propio script.

    Idempotente: az monitor metrics alert create con el mismo nombre actualiza la regla.

.PARAMETER ResourceGroup
    Resource group del servidor SQL. Por defecto SRBRGDOCSAIPROD.

.PARAMETER ServerName
    Nombre del servidor SQL. Por defecto srbsqlprodocai.

.PARAMETER DatabaseName
    Nombre de la base de datos. Por defecto DocumentIA.

.PARAMETER Threshold
    Umbral en porcentaje de ocupacion. Por defecto 80.

.PARAMETER SubscriptionId
    (Opcional) Suscripcion del recurso. Por defecto la activa en az.

.PARAMETER ActionGroupId
    (Opcional) Resource ID completo del action group a asociar.

.EXAMPLE
    ./create-db-capacity-alert.ps1
    ./create-db-capacity-alert.ps1 -Threshold 85 -ActionGroupId "/subscriptions/<sub>/resourceGroups/SRBRGDOCSAIPROD/providers/microsoft.insights/actionGroups/srbagoperprodocai"
#>
[CmdletBinding()]
param(
    [string]$ResourceGroup  = "SRBRGDOCSAIPROD",
    [string]$ServerName     = "srbsqlprodocai",
    [string]$DatabaseName   = "DocumentIA",
    [int]$Threshold         = 80,
    [string]$SubscriptionId = "",
    [string]$ActionGroupId  = ""
)

$ErrorActionPreference = "Stop"

if (-not $SubscriptionId) {
    $SubscriptionId = az account show --query id -o tsv
}
if (-not $SubscriptionId) { throw "No hay sesion de az. Ejecuta 'az login' primero." }

$scope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Sql/servers/$ServerName/databases/$DatabaseName"
$alertName = "srbalertstoprodocai"

Write-Host "Creando/actualizando alerta '$alertName'"
Write-Host "  Ambito : $scope"
Write-Host "  Umbral : storage_percent > $Threshold%"

$argumentos = @(
    "monitor", "metrics", "alert", "create",
    "--name", $alertName,
    "--resource-group", $ResourceGroup,
    "--subscription", $SubscriptionId,
    "--scopes", $scope,
    "--condition", "avg storage_percent > $Threshold",
    "--description", "Ocupacion de la BD DocumentIA por encima del $Threshold%. Revisar crecimiento y retencion antes de quedarse sin espacio. AB#100171.",
    "--window-size", "1h",
    "--evaluation-frequency", "30m",
    "--severity", "2"
)

if ($ActionGroupId) { $argumentos += @("--action", $ActionGroupId) }

az @argumentos | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Fallo al crear la alerta '$alertName'." }

Write-Host "[OK] Alerta '$alertName' creada o actualizada."
if (-not $ActionGroupId) {
    Write-Host "Sin -ActionGroupId la regla queda sin acciones: re-ejecutar con el action group para que avise por correo."
}
