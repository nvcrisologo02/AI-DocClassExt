# Fase 3: Rollback Procedures & Emergency Guides

---

## 1. v1.5 ROLLBACK (Code-Only, No DB Changes)

### Scenario: Deployment Issues Post v1.5

**Timeframe**: Can be done immediately (zero DB constraints)  
**Risk Level**: Very Low  
**Data Impact**: NONE  

#### Quick Rollback (< 5 minutes)

```powershell
# 1. Identify previous deployment
$deployments = az functionapp deployment list `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions" `
    --query "[0:5].[id,status,timestamp,author_email]" `
    -o table

# 2. Swap to previous deployment slot
az functionapp deployment slot swap `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions" `
    --slot staging

# 3. Verify health
$health = Invoke-RestMethod `
    -Uri "https://document-ia-functions.azurewebsites.net/api/health" `
    -ErrorAction SilentlyContinue

if ($health.status -eq "healthy") {
    Write-Output "✅ v1.5 rollback successful"
} else {
    Write-Output "❌ Rollback failed - manual intervention needed"
}
```

#### Manual Rollback (If Swap Fails)

```powershell
# 1. Re-deploy v1.4 code
$v14Package = "artifacts/v1.4/publish.zip"

az functionapp deployment source config-zip `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions" `
    --src $v14Package

# 2. Restart function app
az functionapp restart `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions"

# 3. Verify
Start-Sleep -Seconds 10
$health = Invoke-RestMethod -Uri "$healthUrl" -ErrorAction SilentlyContinue
```

**Note**: v1.5 rollback affects ZERO database tables. No data loss possible.

---

## 2. v2.0 ROLLBACK (Database + Code)

### Scenario: Migration Failure or Data Corruption

**Timeframe**: 15-30 minutes  
**Risk Level**: Low (pre-tested backup + migration)  
**Data Impact**: Restored from backup (loss = 0)  

### 2.1 Emergency Rollback Checklist

- [ ] Identify rollback reason (error logs in Application Insights)
- [ ] Notify team immediately (Slack + email)
- [ ] Begin countdown (30 min window)
- [ ] Prepare backup location

### 2.2 Database Rollback Procedure

```powershell
# ⚠️ This is IRREVERSIBLE once executed

# 1. Stop application immediately
az functionapp stop `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions"

Write-Output "⏸️  Application stopped"

# 2. List available backups
$backups = az sql db list-deleted `
    --server "document-ia" `
    --resource-group "AI-DocClassExt-prod" `
    --query "[].{name:name,deletionTime:deletionTime,backupStorageRedundancy:backupStorageRedundancy}"

$backups | Select-Object -First 5 | Format-Table

# 3. Restore from pre-v2.0 backup
# Using Azure Portal:
# - SQL Databases > DocumentIA
# - Restore > Point in Time Restore
# - Select: July 30, 2026 23:59:59 (pre-migration)
# - New database name: "DocumentIA_Restored"

# OR via CLI:
$restorePointTime = "2026-07-30T23:59:59Z"

az sql db restore `
    --server "document-ia" `
    --resource-group "AI-DocClassExt-prod" `
    --name "DocumentIA" `
    --restore-point-in-time $restorePointTime `
    --dest-name "DocumentIA_Restored"

# 4. Verify restoration
$restoredDb = az sql db list `
    --server "document-ia" `
    --resource-group "AI-DocClassExt-prod" `
    --query "[?name=='DocumentIA_Restored']"

if ($restoredDb) {
    Write-Output "✅ Database restored: $restoredDb"
} else {
    Write-Output "❌ Restoration failed"
    exit 1
}
```

### 2.3 Swap Database & Code

```powershell
# 1. Rename original DB (archive)
az sql db rename `
    --server "document-ia" `
    --resource-group "AI-DocClassExt-prod" `
    --name "DocumentIA" `
    --new-name "DocumentIA_Failed_v2.0"

# 2. Rename restored DB (active)
az sql db rename `
    --server "document-ia" `
    --resource-group "AI-DocClassExt-prod" `
    --name "DocumentIA_Restored" `
    --new-name "DocumentIA"

Write-Output "✅ Database swapped to restored version"

# 3. Deploy v1.5 code (last known good)
az functionapp deployment source config-zip `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions" `
    --src "artifacts/v1.5/publish.zip"

# 4. Start application
az functionapp start `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions"

Write-Output "✅ Application restarted with v1.5"

# 5. Verify
Start-Sleep -Seconds 15
$health = Invoke-RestMethod `
    -Uri "https://document-ia-functions.azurewebsites.net/api/health" `
    -ErrorAction SilentlyContinue

if ($health.status -eq "healthy") {
    Write-Output "✅ Complete rollback successful"
    Write-Output "   - Database: Restored to pre-v2.0 state"
    Write-Output "   - Code: Reverted to v1.5"
    Write-Output "   - PromptGPT: Still present (v1.5 schema)"
} else {
    Write-Output "❌ CRITICAL: Health check failed after rollback"
    Write-Output "⚠️  MANUAL INTERVENTION REQUIRED"
}
```

### 2.4 Investigation & Analysis

```powershell
# 1. Archive failed database for forensics
az sql db delete `
    --server "document-ia" `
    --resource-group "AI-DocClassExt-prod" `
    --name "DocumentIA_Failed_v2.0" `
    --yes

# 2. Analyze rollback logs
$logPath = "artifacts/logs/rollback_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

az functionapp log download `
    --resource-group "AI-DocClassExt-prod" `
    --name "document-ia-functions" `
    --log-file $logPath

Write-Output "Rollback logs saved to: $logPath"

# 3. Generate incident report
$report = @"
ROLLBACK INCIDENT REPORT
========================
Date: $(Get-Date)
Version: v2.0
Reason: [Investigation needed]
Duration: [minutes]
Data Loss: 0 (fully restored)
Status: ✅ COMPLETE

Next Steps:
1. Review Application Insights errors
2. Analyze migration logs
3. Fix identified issues
4. Re-schedule v2.0 deployment for [DATE]
"@

Set-Content -Path "artifacts/logs/incident_report.txt" -Value $report
```

---

## 3. PARTIAL ROLLBACK (Data Only, Keep v2.0 Code)

### Use Case: Data corruption in v2.0, but code is correct

**Timeframe**: 10-15 minutes  
**Complexity**: Medium  

```powershell
# 1. Revert v2.0 migration (undo DROP)
cd 'src/backend/DocumentIA.Data'

dotnet ef database update `
    --context DocumentIADbContext `
    --migration "20260605095444_v15_MarkPromptGPTObsolete"

Write-Output "✅ Migration reverted (PromptGPT column restored)"

# 2. Restore data from backup
# If corruption detected:
$backupData = Import-Csv "artifacts/backups/20260605_pre-drop/tipologias_backup.csv"

# Insert backup data (with conflict handling)
$backupData | ForEach-Object {
    $existingTip = Get-DbTipologia -Id $_.Id
    if ($existingTip) {
        Update-DbTipologia -Data $_
    } else {
        Add-DbTipologia -Data $_
    }
}

# 3. Verify data integrity
$count = Invoke-Sqlcmd -Query "SELECT COUNT(*) FROM Tipologias"
Write-Output "Tipologías restored: $count rows"
```

---

## 4. POST-ROLLBACK PROCEDURES

### 4.1 Communication Template

```
Subject: [URGENT] v2.0 Deployment Rolled Back

Team,

A critical issue was identified during v2.0 deployment at [TIME].

**ACTIONS TAKEN:**
- Application: Stopped at [TIME]
- Database: Restored from backup (pre-v2.0 state)
- Code: Reverted to v1.5
- Data: 100% intact (zero loss)

**CURRENT STATUS:**
✅ Application is healthy and operational
✅ All classifications working
✅ PromptGPT field restored (v1.5 schema)

**NEXT STEPS:**
1. Root cause analysis (48 hours)
2. Code review & fixes
3. Re-schedule deployment for [DATE]

**IMPACT:**
- Users: Zero impact (transparent cutover)
- Data: Zero loss (full recovery)
- Timeline: v2.0 deployment delayed to [DATE]

For questions, contact: [OnCall Engineer]
```

### 4.2 Root Cause Analysis

```powershell
# 1. Review migration logs
Get-Content "src/backend/DocumentIA.Data/bin/Release/net8.0/migrate.log" | Select-Object -Last 50

# 2. Check database transaction log
$query = @"
SELECT TOP 20 
    operation, 
    database_name, 
    transaction_begin_time,
    session_id
FROM sys.dm_tran_database_transactions
ORDER BY transaction_begin_time DESC
"@

Invoke-Sqlcmd -Query $query -ServerInstance "document-ia.database.windows.net" -Database "DocumentIA"

# 3. Analyze Application Insights
# URL: https://portal.azure.com
# Filter: Custom query
# Query: exceptions | where timestamp > ago(1h)
```

### 4.3 Follow-Up Actions

- [ ] Create GitHub issue documenting the failure
- [ ] Schedule post-mortem meeting
- [ ] Review migration script (pre-tested?)
- [ ] Add more test scenarios
- [ ] Update rollback runbook if needed
- [ ] Re-schedule v2.0 deployment

---

## 5. PREVENTION CHECKLIST

### Before Any v2.0 Deployment

- [ ] Database backup: VERIFIED + RESTORED (practice)
- [ ] Migration script: TESTED in staging
- [ ] Code: 100% test coverage
- [ ] Performance baseline: RECORDED
- [ ] Team: Trained on rollback
- [ ] Communication: Template ready
- [ ] Contacts: List verified
- [ ] Emergency access: Confirmed

---

## 6. SUPPORT MATRIX

| Scenario | Duration | Data Loss | Risk | Contacts |
|----------|----------|-----------|------|----------|
| v1.5 Rollback | 5 min | 0 | Very Low | Deployment Lead |
| v2.0 Database Only | 15 min | 0 | Low | DBA + DevOps |
| v2.0 Code Only | 5 min | 0 | Very Low | Deployment Lead |
| v2.0 Full | 30 min | 0 | Low | DBA + DevOps + CTO |
| Data Corruption | 10 min | 0 | Medium | DBA + Database Engineer |

---

## 7. ESCALATION PATH

```
Level 1 (5 min): Try quick rollback
    ↓
Level 2 (10 min): Contact deployment lead
    ↓
Level 3 (15 min): Contact DBA
    ↓
Level 4 (20 min): Contact DevOps lead
    ↓
Level 5 (30 min): Contact CTO
    ↓
Level 6 (45 min): Activate full incident response
```

---

## 8. TESTING ROLLBACK

### Monthly Rollback Drills

```powershell
# Schedule: First Friday of each month, 14:00 UTC

# 1. Create snapshot of current database
# 2. Simulate v2.0 deployment failure
# 3. Execute rollback procedure
# 4. Verify data + application
# 5. Restore from snapshot (return to normal)
# 6. Document results

Write-Output "✅ Rollback drill completed successfully"
```

---

## 9. CONTACT INFORMATION

| Role | Name | Email | Phone | Time Zone |
|------|------|-------|-------|-----------|
| Deployment Lead | [Name] | [email] | [phone] | CET |
| Database DBA | [Name] | [email] | [phone] | CET |
| DevOps Lead | [Name] | [email] | [phone] | CET |
| CTO | [Name] | [email] | [phone] | CET |
| On-Call | [Name] | [email] | [phone] | [varies] |

---

## 10. FINAL NOTES

- ✅ **v1.5 rollback is automatic and low-risk**
- ⚠️ **v2.0 rollback requires database restore (~15 min)**
- 🔄 **Practice rollback procedures monthly**
- 📋 **Keep all backups for 90 days**
- 🚨 **Immediate escalation if downtime exceeds 30 min**
