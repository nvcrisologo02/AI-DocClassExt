# =============================================================================
# Configura los Application Settings no secretos de la Function App en Azure
# Uso: .\scripts\set-app-settings.ps1
#
# Requisitos previos:
#   - Azure CLI instalado y sesion activa: az login
#   - Key Vault srbkvprodocai poblado con todos los secretos necesarios
#   - Este script deja Azure en modo Key Vault via Program.cs
# =============================================================================

$ErrorActionPreference = "Stop"

# ─── PARAMETROS DE RECURSO ───────────────────────────────────────────────────
$resourceGroup   = "SRBRGDOCSAIPROD"
$functionAppName = "srbappprodocai"
$keyVaultName    = "srbkvprodocai"
$assetResolverBaseUrl = "https://srbwebpluginassetresolver.azurewebsites.net/"

# Obtenido via: az monitor app-insights component show --resource-group SRBRGDOCSAIPROD --app srbappiprodocai --query connectionString -o tsv
$appInsightsConnectionString = "InstrumentationKey=c57c6166-c5ac-4584-a4d6-13fbb37860c4;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.com/;ApplicationId=19eeb2fb-8544-41c5-931a-b0983a26e92e"

# =============================================================================
# NO modificar a partir de aqui salvo cambio de entorno
# =============================================================================

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  CONFIGURANDO APP SETTINGS" -ForegroundColor Cyan
Write-Host "  $functionAppName" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Validar que se han rellenado los campos obligatorios
$incompletos = @()
if ($appInsightsConnectionString  -match "COMPLETAR") { $incompletos += "APPLICATIONINSIGHTS_CONNECTION_STRING" }

if ($incompletos.Count -gt 0) {
    Write-Host "[ERROR] Hay valores sin completar en el script:" -ForegroundColor Red
    $incompletos | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host "`nEdita el script, rellena los valores marcados con COMPLETAR y vuelve a ejecutarlo." -ForegroundColor Yellow
    exit 1
}

Write-Host "[1/3] Aplicando settings de infraestructura..." -ForegroundColor Yellow

az functionapp config appsettings set `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --settings `
        "FUNCTIONS_WORKER_RUNTIME=dotnet-isolated" `
        "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString" `
        "SecretsSource=AzureVault" `
        "KeyVaultName=$keyVaultName" `
        "AzureWebJobsStorage=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=AzureWebJobsStorage)" `
        "AzureStorageConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=AzureStorageConnectionString)" `
        "SqlConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=SqlConnectionString)" `
        "AssetResolver__BaseUrl=$assetResolverBaseUrl" `
        "AssetResolver__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=AssetResolverApiKey)" `
        "RunDatabaseMigrationsOnStartup=false" `
        "BlobRetention__DefaultDays=2" `
        "BlobRetentionCleanupCron=0 0 3 * * *" `
    --output none

if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Fallo en settings de infraestructura" -ForegroundColor Red; exit 1 }
Write-Host "  [OK]" -ForegroundColor Green

Write-Host "[1.5/3] Eliminando settings legacy/conflictivos..." -ForegroundColor Yellow

az functionapp config appsettings delete `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --setting-names `
        "AzureWebJobsStorage__accountName" `
        "AzureWebJobsStorage__credential" `
    --output none

if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Fallo eliminando settings conflictivos" -ForegroundColor Red; exit 1 }
Write-Host "  [OK]" -ForegroundColor Green

Write-Host "[2/3] Aplicando settings de AI (Extraction + Classification)..." -ForegroundColor Yellow

az functionapp config appsettings set `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --settings `
        "Extraction__DefaultProvider=azure-content-understanding" `
        "Extraction__AzureContentUnderstanding__Endpoint=https://upe48-mm2avmdm-swedencentral.services.ai.azure.com/" `
        "Extraction__AzureContentUnderstanding__AuthMode=ApiKey" `
        "Extraction__AzureContentUnderstanding__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Extraction--AzureContentUnderstanding--ApiKey)" `
        "Extraction__AzureContentUnderstanding__DefaultProcessingLocation=geography" `
        "Extraction__GptFallback__Enabled=true" `
        "Extraction__GptFallback__Endpoint=https://upe48-mm2avmdm-swedencentral.openai.azure.com" `
        "Extraction__GptFallback__AuthMode=ApiKey" `
        "Extraction__GptFallback__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Extraction--GptFallback--ApiKey)" `
        "Extraction__GptFallback__DeploymentName=gpt-4o-mini" `
        "Extraction__GptFallback__MinFieldsRatio=0.9" `
        "Extraction__GptFallback__Temperature=0.0" `
        "Extraction__GptFallback__MaxTokens=2000" `
        "Extraction__GptFallback__TimeoutSeconds=60" `
        "Classification__DefaultProvider=azure-document-intelligence" `
        "Classification__DefaultModelKey=default.azure-di" `
        "Classification__AzureDocumentIntelligence__Endpoint=https://srbdiprodocai.cognitiveservices.azure.com/" `
        "Classification__AzureDocumentIntelligence__AuthMode=ApiKey" `
        "Classification__AzureDocumentIntelligence__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Classification--AzureDocumentIntelligence--ApiKey)" `
        "Classification__AzureDocumentIntelligence__ApiVersion=2024-11-30" `
        "Classification__GptFallback__Enabled=true" `
        "Classification__GptFallback__Endpoint=https://upe48-mm2avmdm-swedencentral.openai.azure.com" `
        "Classification__GptFallback__AuthMode=ApiKey" `
        "Classification__GptFallback__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Classification--GptFallback--ApiKey)" `
        "Classification__GptFallback__DeploymentName=gpt-4o-mini" `
        "Classification__GptFallback__FallbackThreshold=0.5" `
        "Classification__GptFallback__Temperature=0.0" `
        "Classification__GptFallback__MaxTokens=150" `
        "Classification__GptFallback__TimeoutSeconds=30" `
    --output none

if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Fallo en settings de AI" -ForegroundColor Red; exit 1 }
Write-Host "  [OK]" -ForegroundColor Green

Write-Host "[3/3] Aplicando settings de GDC..." -ForegroundColor Yellow

az functionapp config appsettings set `
    --resource-group $resourceGroup `
    --name $functionAppName `
    --settings `
        "GDC__Endpoint=https://srbwidd03.sareb.srb:8090/sintws/IDocService" `
        "GDC__TimeoutSeconds=60" `
        "GDC__HttpBasicUsername=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=GDC--HttpBasicUsername)" `
        "GDC__HttpBasicPassword=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=GDC--HttpBasicPassword)" `
        "GDC__ApplicationId=CKP1" `
        "GDC__NominalUser=" `
        "GDC__DocumentTypeId=document" `
        "GDC__ContentFieldName=Content" `
        "GDC__OrigenDocumento=8878" `
        "GDC__RepositoryId=" `
        "GDC__RepositoryName=" `
        "GDC__ClaseExpediente=AI04" `
        "GDC__DefaultMatricula=AI-99-SCXX-00" `
        "GDC__Servicer=9999" `
        "GDC__EntidadOrigen=9999" `
        "GDC__ProcesoCarga=PC01" `
        "GDC__TipoExpediente=AI" `
        "GDC__Publico=verdadero" `
        "GDC__BypassSslValidation=true" `
    --output none

if ($LASTEXITCODE -ne 0) { Write-Host "[ERROR] Fallo en settings de GDC" -ForegroundColor Red; exit 1 }
Write-Host "  [OK]" -ForegroundColor Green

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  COMPLETADO" -ForegroundColor Green
Write-Host "  Todos los settings aplicados en $functionAppName" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "NOTA: Este script deja los secretos fuera de App Settings." -ForegroundColor Yellow
Write-Host "  - AzureWebJobsStorage queda referenciado a Key Vault para el host de Functions" -ForegroundColor Gray
Write-Host "  - SQL, AI, GDC y AssetResolver quedan referenciados a Key Vault" -ForegroundColor Gray
