# Script para inicializar Azurite con los contenedores necesarios para Durable Functions

$connectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"

Write-Host "Inicializando Azurite con contenedores necesarios..." -ForegroundColor Cyan

# Esperar a que Azurite esté listo
Write-Host "Esperando a que Azurite responda..." -ForegroundColor Yellow
$maxRetries = 30
$retries = 0
while ($retries -lt $maxRetries) {
    try {
        $context = New-AzStorageContext -ConnectionString $connectionString -ErrorAction Stop
        Write-Host "[OK] Azurite esta listo" -ForegroundColor Green
        break
    }
    catch {
        $retries++
        if ($retries -eq $maxRetries) {
            Write-Host "[ERROR] No se pudo conectar a Azurite despues de $maxRetries intentos" -ForegroundColor Red
            exit 1
        }
        Write-Host "  Reintentando... ($retries/$maxRetries)"
        Start-Sleep -Seconds 1
    }
}

# Crear contexto
$context = New-AzStorageContext -ConnectionString $connectionString

# Crear contenedores de Blob Storage
Write-Host "`nCreando contenedores de Blob Storage..." -ForegroundColor Cyan
$blobContainers = @(
    "azure-webjobs-hosts",
    "scalarresults",
    "pbt2651-191919255",
    "extraccion-resultado"
)

foreach ($container in $blobContainers) {
    try {
        New-AzStorageContainer -Name $container -Context $context -ErrorAction SilentlyContinue
        Write-Host "  [OK] Contenedor blob '$container' creado/verificado"
    }
    catch {
        Write-Host "  [WARN] Contenedor blob '$container' ya existe o error: $_" -ForegroundColor Yellow
    }
}

# Crear colas (Queues)
Write-Host "`nCreando colas para Durable Functions..." -ForegroundColor Cyan
$queues = @(
    "documentiahub-control-00",
    "documentiahub-control-01",
    "documentiahub-control-02",
    "documentiahub-control-03",
    "documentiahub-workitems"
)

foreach ($queue in $queues) {
    try {
        New-AzStorageQueue -Name $queue -Context $context -ErrorAction SilentlyContinue
        Write-Host "  [OK] Cola '$queue' creada/verificada"
    }
    catch {
        Write-Host "  [WARN] Cola '$queue' ya existe o error: $_" -ForegroundColor Yellow
    }
}

# Crear tablas (Tables)
Write-Host "`nCreando tablas para Durable Functions..." -ForegroundColor Cyan
$tables = @(
    "DocumentIAHub",
    "DocumentIAHubInstances",
    "DocumentIAHubHistory"
)

foreach ($table in $tables) {
    try {
        New-AzStorageTable -Name $table -Context $context -ErrorAction SilentlyContinue
        Write-Host "  [OK] Tabla '$table' creada/verificada"
    }
    catch {
        Write-Host "  [WARN] Tabla '$table' ya existe o error: $_" -ForegroundColor Yellow
    }
}

Write-Host "`n[SUCCESS] Azurite inicializado correctamente" -ForegroundColor Green
