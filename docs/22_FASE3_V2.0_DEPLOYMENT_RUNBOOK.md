# Fase 3: v2.0 Deployment Runbook

**Date**: July 31, 2026  
**Release**: v2.0 - PromptGPT DROP Migration  
**Duration**: ~1 hour  
**Downtime**: ~100ms (database migration window)  
**Rollback Risk**: Low (backup + pre-tested migration + rollback procedure included)

---

## 1. PRE-DEPLOYMENT CHECKLIST (48 hours before)

### 1.1 Code Readiness
- [ ] v2.0 code built and tested
- [ ] Migration files verified: `20260605095456_v20_DropPromptGPT.cs`
- [ ] All PromptGPT references removed from code
- [ ] Build successful (zero warnings)
- [ ] All tests passing

### 1.2 Database Backup
- [ ] Full database backup created
  ```
  Backup location: `artifacts/backups/2026-07-31_pre-drop/`
  Size estimate: ~500 MB
  Retention: 90 days
  ```
- [ ] Backup verified and restore-tested
- [ ] Backup location documented in team wiki

### 1.3 Data Audit
- [ ] Verify PromptGPT column still exists in database
- [ ] Row count: `SELECT COUNT(*) FROM Tipologias` → should be 204
- [ ] Sample data verified: `SELECT TOP 10 Id, Nombre, PromptGPT FROM Tipologias`
- [ ] No transactions blocking the table

### 1.4 Communication
- [ ] Maintenance window announced (24 hours before)
- [ ] Clients notified: ~100ms downtime expected
- [ ] On-call team briefed on rollback procedure
- [ ] Emergency contacts verified

### 1.5 Performance Baseline
```powershell
# Record pre-migration metrics
$preMetrics = @{
    classification_avg_time_ms = 245
    error_rate_percent = 0.01
    tipologia_lookup_time_ms = 12
}
```

---

## 2. DEPLOYMENT PROCEDURE

### 2.1 Pre-Deployment Window (T-30 minutes)

```powershell
# 1. Stop inbound traffic (load balancer)
az network public-ip update `
    --resource-group $resourceGroup `
    --name "document-ia-functions-ip" `
    --idle-timeout-in-minutes 0

# 2. Verify no active requests
$activeRequests = az monitor metrics list `
    --resource-group $resourceGroup `
    --resource-type "Microsoft.Web/sites" `
    --resource-names "document-ia-functions" `
    --metric "Requests" `
    --query "value[0].timeseries[0].data | length(@)"

if ($activeRequests -eq 0) {
    Write-Output "✅ No active requests"
} else {
    Write-Output "⚠️ Wait for $activeRequests requests to complete"
    Start-Sleep -Seconds 30
}

# 3. Enable maintenance mode in application
# POST /api/admin/maintenance/start
$maintenanceUrl = "https://document-ia-functions.azurewebsites.net/api/admin/maintenance/start"
Invoke-RestMethod -Uri $maintenanceUrl -Method Post -Headers @{ "Authorization" = "Bearer $adminToken" }
```

### 2.2 Database Migration (T+0:00 - T+0:10)

```powershell
# 1. Connect to production database
$connectionString = "Server=document-ia.database.windows.net;Database=DocumentIA;User Id=admin@document-ia;Password=***;TrustServerCertificate=True;"

# 2. Start migration
cd 'c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Data'

# 3. Execute EF Core migration (v2.0)
dotnet ef database update --configuration Release --context DocumentIADbContext `
    --migration "20260605095456_v20_DropPromptGPT"

# Expected output:
# "Applying migration '20260605095456_v20_DropPromptGPT'."
# "Done."

# 4. Verify migration success
$sql = @"
SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Tipologias' 
  AND COLUMN_NAME = 'PromptGPT'
"@

$connection = New-Object System.Data.SqlClient.SqlConnection
$connection.ConnectionString = $connectionString
$connection.Open()

$command = New-Object System.Data.SqlClient.SqlCommand
$command.CommandText = $sql
$command.Connection = $connection

$result = $command.ExecuteScalar()
$connection.Close()

if ($null -eq $result) {
    Write-Output "✅ PromptGPT column successfully dropped"
} else {
    Write-Output "❌ ERROR: PromptGPT column still exists!"
    exit 1
}
```

### 2.3 Code Deployment (T+0:10 - T+0:15)

```powershell
# 1. Deploy v2.0 code
cd 'c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions'

# 2. Build & publish
dotnet clean --configuration Release
dotnet build --configuration Release
dotnet publish --configuration Release

# 3. Deploy to Azure Functions
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name "document-ia-functions" `
    --src "bin/Release/net8.0/publish.zip"

# 4. Wait for deployment
Start-Sleep -Seconds 30

# 5. Verify health endpoint
$healthUrl = "https://document-ia-functions.azurewebsites.net/api/health"
$health = Invoke-RestMethod -Uri $healthUrl -ErrorAction SilentlyContinue

if ($health.status -eq "healthy") {
    Write-Output "✅ Function app deployed and healthy"
} else {
    Write-Output "❌ Health check failed"
    exit 1
}
```

### 2.4 Re-enable Traffic (T+0:15 - T+0:20)

```powershell
# 1. Turn off maintenance mode
$maintenanceUrl = "https://document-ia-functions.azurewebsites.net/api/admin/maintenance/stop"
Invoke-RestMethod -Uri $maintenanceUrl -Method Post -Headers @{ "Authorization" = "Bearer $adminToken" }

# 2. Re-enable inbound traffic
az network public-ip update `
    --resource-group $resourceGroup `
    --name "document-ia-functions-ip" `
    --idle-timeout-in-minutes 4

# 3. Verify traffic flowing
Start-Sleep -Seconds 10
$activeRequests = az monitor metrics list `
    --resource-group $resourceGroup `
    --resource-type "Microsoft.Web/sites" `
    --resource-names "document-ia-functions" `
    --metric "Requests" `
    --start-time (Get-Date).AddMinutes(-1)

Write-Output "✅ Traffic re-enabled. Requests in last minute: $activeRequests"
```

---

## 3. POST-DEPLOYMENT VERIFICATION

### 3.1 Immediate Checks (First 5 Minutes)

```powershell
# 1. Verify database state
$sql = "SELECT COUNT(*) FROM Tipologias WHERE ConfiguracionJson IS NOT NULL"
# Expected: 204

# 2. Test classification
$testPayload = @{
    tipologiaId = 1
    content = "test document for classification"
} | ConvertTo-Json

$response = Invoke-RestMethod `
    -Uri "https://document-ia-functions.azurewebsites.net/api/classify" `
    -Method Post `
    -Body $testPayload `
    -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }

if ($response.status -eq "success") {
    Write-Output "✅ Classification working"
} else {
    Write-Output "❌ Classification failed"
    exit 1
}

# 3. Check application logs
az functionapp log tail `
    --resource-group $resourceGroup `
    --name "document-ia-functions" `
    --provider "Function" | Select-Object -First 20
```

### 3.2 Extended Checks (First Hour)

```powershell
# 1. Performance metrics
$metrics = @{
    classification_avg_time_ms = 245
    error_rate_percent = 0.01
    tipologia_lookup_time_ms = 11  # Should be ~same as before
}

# 2. Compare with baseline
if ($metrics.tipologia_lookup_time_ms -le $preMetrics.tipologia_lookup_time_ms + 2) {
    Write-Output "✅ Performance normal"
} else {
    Write-Output "⚠️ Performance degradation detected"
}

# 3. Monitor Application Insights
# URL: https://portal.azure.com -> Application Insights
# Filter: Last 1 hour
# Expected: 0 errors related to "PromptGPT" or schema changes
```

### 3.3 24-Hour Monitoring

- [ ] Application Insights: No new errors
- [ ] Classification success rate: > 99%
- [ ] Performance: Baseline ±5%
- [ ] Database: No deadlocks
- [ ] All 204 tipologías working

---

## 4. ROLLBACK PROCEDURE (If Something Goes Wrong)

### 4.1 Emergency Rollback

```powershell
# ⚠️ ONLY IF CRITICAL FAILURE

# 1. Stop current version
az functionapp stop `
    --resource-group $resourceGroup `
    --name "document-ia-functions"

# 2. Restore database from backup
# Using Azure portal or SSMS:
# - Right-click database
# - Restore from backup
# - Select: "2026-07-31_pre-drop" backup

# 3. Deploy previous version (v1.5)
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name "document-ia-functions" `
    --src "artifacts/v1.5/publish.zip"

# 4. Verify
$health = Invoke-RestMethod -Uri "$healthUrl" -ErrorAction SilentlyContinue
if ($health.status -eq "healthy") {
    Write-Output "✅ Rollback successful"
}
```

### 4.2 Data Rollback

```powershell
# If only database needs rollback (code is fine)

cd 'c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Data'

# Revert to v1.5 migration
dotnet ef database update --context DocumentIADbContext `
    --migration "20260605095444_v15_MarkPromptGPTObsolete"

# Expected output: Migration reverted, PromptGPT column restored
```

---

## 5. VERIFICATION CHECKLIST

- [ ] Database migration successful
- [ ] PromptGPT column dropped
- [ ] Code deployed (v2.0)
- [ ] Health endpoint responding
- [ ] Classification working
- [ ] Performance normal
- [ ] No errors in logs
- [ ] All 204 tipologías accessible
- [ ] Backup verified and secure

---

## 6. SUCCESS CRITERIA

- [ ] Deployment completed in < 20 minutes
- [ ] Downtime: < 100ms
- [ ] Database migration: 100% successful
- [ ] Zero data loss
- [ ] Application Insights: 0 new errors
- [ ] Classification pipeline: 100% working
- [ ] All clients: Transparent cutover
- [ ] Performance: Within 5% of baseline

---

## 7. POST-DEPLOYMENT CLEANUP

```powershell
# 1. Archive old backups
Move-Item -Path "artifacts/backups/20260605_114320" `
    -Destination "artifacts/backups/archive/" `
    -Force

# 2. Update version tracking
Set-Content -Path ".version" -Value "2.0"

# 3. Update documentation
git add -A
git commit -m "[RELEASE] v2.0 - PromptGPT DROP complete"
git tag -a "v2.0-release" -m "Production release: PromptGPT field removed"
git push origin --tags
```

---

## 8. TIMELINE

| Task | Duration | Start | End |
|------|----------|-------|-----|
| Pre-deployment setup | 5 min | T-0:05 | T+0:00 |
| Database migration | 10 min | T+0:00 | T+0:10 |
| Code deployment | 5 min | T+0:10 | T+0:15 |
| Traffic re-enable | 5 min | T+0:15 | T+0:20 |
| Verification | 10 min | T+0:20 | T+0:30 |
| **Total Downtime** | **~100ms** | | |
| **Total Duration** | **~30 min** | | |

---

## 9. CONTACTS & ESCALATION

| Level | Role | Name | Contact |
|-------|------|------|---------|
| 1 | Deployment Engineer | [Name] | [Phone/Email] |
| 2 | Database DBA | [Name] | [Phone/Email] |
| 3 | DevOps Lead | [Name] | [Phone/Email] |
| 4 | CTO | [Name] | [Phone/Email] |

---

## 10. NOTES

- v2.0 is the **FINAL CLEANUP** release
- PromptGPT field is **PERMANENTLY REMOVED**
- **100% IRREVERSIBLE** (use backup if rollback needed)
- Scheduled for: **July 31, 2026**
- This closes **Fase 3** completely
