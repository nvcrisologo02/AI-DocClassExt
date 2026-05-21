# Clean Azurite resources - allow Durable Functions to regenerate from scratch
param(
    [string]$BlobEndpoint = "http://127.0.0.1:10000/devstoreaccount1",
    [string]$QueueEndpoint = "http://127.0.0.1:10001/devstoreaccount1", 
    [string]$TableEndpoint = "http://127.0.0.1:10002/devstoreaccount1",
    [string]$AccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw=="
)

Write-Host "Cleaning corrupted Azurite resources..." -ForegroundColor Cyan

$blobContext = New-AzStorageContext -StorageAccountName "devstoreaccount1" -StorageAccountKey $AccountKey -BlobEndpoint $BlobEndpoint -ErrorAction SilentlyContinue
$queueContext = New-AzStorageContext -StorageAccountName "devstoreaccount1" -StorageAccountKey $AccountKey -QueueEndpoint $QueueEndpoint -ErrorAction SilentlyContinue
$tableContext = New-AzStorageContext -StorageAccountName "devstoreaccount1" -StorageAccountKey $AccountKey -TableEndpoint $TableEndpoint -ErrorAction SilentlyContinue

# Delete containers
Write-Host "Deleting blob containers..." -ForegroundColor Yellow
@("azure-webjobs-hosts", "scalarresults", "pbt2651-191919255", "extraccion-resultado") | ForEach-Object {
    $exists = Get-AzStorageContainer -Context $blobContext -Name $_ -ErrorAction SilentlyContinue
    if ($exists) {
        Remove-AzStorageContainer -Context $blobContext -Name $_ -Force -ErrorAction SilentlyContinue
        Write-Host "  [DELETED] $_"
    }
}

# Delete queues
Write-Host "Deleting queues..." -ForegroundColor Yellow
@("documentiahub-control-00", "documentiahub-control-01", "documentiahub-control-02", "documentiahub-control-03", "documentiahub-workitems") | ForEach-Object {
    $exists = Get-AzStorageQueue -Context $queueContext -Name $_ -ErrorAction SilentlyContinue
    if ($exists) {
        Remove-AzStorageQueue -Context $queueContext -Name $_ -Force -ErrorAction SilentlyContinue
        Write-Host "  [DELETED] $_"
    }
}

# Delete tables
Write-Host "Deleting tables..." -ForegroundColor Yellow
@("DocumentIAHub", "DocumentIAHubInstances", "DocumentIAHubHistory") | ForEach-Object {
    $exists = Get-AzStorageTable -Context $tableContext -Name $_ -ErrorAction SilentlyContinue
    if ($exists) {
        Remove-AzStorageTable -Context $tableContext -Name $_ -Force -ErrorAction SilentlyContinue
        Write-Host "  [DELETED] $_"
    }
}

Write-Host ""
Write-Host "Cleanup complete. Resources will auto-regenerate on next Functions startup." -ForegroundColor Green
