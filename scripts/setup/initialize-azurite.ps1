# Script para inicializar Azurite con los contenedores necesarios para Durable Functions

$connectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;"

Write-Host "Inicializando Azurite con contenedores necesarios..." -ForegroundColor Cyan

# Esperar a que Azurite esté listo (Blob + Queue + Table)
Write-Host "Esperando a que Azurite responda (todos los servicios)..." -ForegroundColor Yellow
$maxRetries = 30
$retries = 0
$context = $null

while ($retries -lt $maxRetries) {
    try {
        $context = New-AzStorageContext -ConnectionString $connectionString -ErrorAction Stop
        
        # Verify Blob service is ready
        $containers = Get-AzStorageContainer -Context $context -MaxCount 1 -ErrorAction Stop | Select-Object -First 1
        
        # Verify Table service is ready
        $tables = Get-AzStorageTable -Context $context -ErrorAction Stop | Select-Object -First 1
        
        Write-Host "[OK] Azurite esta listo (Blob + Queue + Table)" -ForegroundColor Green
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

# Crear tablas (Tables) con reintentos
Write-Host "`nCreando tablas para Durable Functions..." -ForegroundColor Cyan
$tables = @(
    "DocumentIAHub",
    "DocumentIAHubInstances",
    "DocumentIAHubHistory"
)

foreach ($table in $tables) {
    $tableRetries = 0
    $maxTableRetries = 3
    $created = $false
    
    while ($tableRetries -lt $maxTableRetries -and -not $created) {
        try {
            $existingTable = Get-AzStorageTable -Name $table -Context $context -ErrorAction SilentlyContinue
            if ($existingTable) {
                Write-Host "  [OK] Tabla '$table' ya existe (verificada)"
                $created = $true
            }
            else {
                New-AzStorageTable -Name $table -Context $context -ErrorAction Stop
                Write-Host "  [OK] Tabla '$table' creada"
                $created = $true
            }
        }
        catch {
            $tableRetries++
            if ($tableRetries -lt $maxTableRetries) {
                Write-Host "  [RETRY] Tabla '$table' - intento $tableRetries/$maxTableRetries"
                Start-Sleep -Milliseconds 500
            }
            else {
                Write-Host "  [WARN] Tabla '$table' no se pudo crear/verificar: $_" -ForegroundColor Yellow
            }
        }
    }
}

# Esperar a que Azurite sincronice cambios antes de retornar
Write-Host "`nEsperando a que Azurite sincronice..." -ForegroundColor Cyan
Start-Sleep -Seconds 2

Write-Host "`n[SUCCESS] Azurite inicializado correctamente" -ForegroundColor Green
