# Fase 3: v1.5 Deployment Runbook

**Date**: June 30, 2026  
**Release**: v1.5 - PromptGPT Deprecation  
**Duration**: ~15-20 minutes  
**Downtime**: 0 minutes (rolling deployment)  
**Rollback Risk**: Very Low (code-only, no DB changes)

---

## 1. PRE-DEPLOYMENT CHECKLIST

### 1.1 Code Readiness
- [ ] All tests passing (32/32)
- [ ] Build successful with expected CS0618 warnings
- [ ] Code review approved
- [ ] Security scan passed
- [ ] All [Obsolete] attributes correctly applied

### 1.2 Documentation Review
- [ ] Team notified of deprecation
- [ ] Deprecation message clear in code comments
- [ ] Client migration guide reviewed
- [ ] No breaking changes identified

### 1.3 Backup & Safety
- [ ] Database backup from 2026-06-05 verified
- [ ] Backup location documented: `artifacts/backups/20260605_114320/`
- [ ] Rollback procedure reviewed
- [ ] Emergency contacts identified

---

## 2. DEPLOYMENT PROCEDURE

### 2.1 Pre-Deployment (5 minutes)
```powershell
# 1. Verify current version
$currentVersion = (dotnet --version)
Write-Output "Current .NET version: $currentVersion"

# 2. Pull latest code
git checkout main
git pull origin main

# 3. Verify feature branch merged
git log --oneline -1
# Should show: "[FASE3] Task 5: Deprecation tests - 32/32 passing"
```

### 2.2 Build & Package (10 minutes)
```powershell
cd 'c:\temp\MVP\documento-ia-clasificacion-mvp\src\backend\DocumentIA.Functions'

# 1. Clean previous build
dotnet clean --configuration Release

# 2. Build
dotnet build --configuration Release

# 3. Verify build (should see CS0618 warnings, NOT errors)
# Expected: "Build succeeded with X warning(s)"

# 4. Publish
dotnet publish --configuration Release `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

# 5. Verify published artifacts
Get-ChildItem -Path "bin/Release/net8.0/publish" -Recurse | Measure-Object | Select-Object -ExpandProperty Count
# Should show: > 100 files
```

### 2.3 Deploy to Azure Functions (5 minutes)
```powershell
# Option A: Azure Functions Core Tools (Local Testing First)
cd "bin/Release/net8.0/publish"
func host start

# Option B: Azure App Service (Production)
# 1. Connect to Azure
az login

# 2. Get app name
$appName = "document-ia-functions"
$resourceGroup = "AI-DocClassExt-prod"

# 3. Deploy
az functionapp deployment source config-zip `
    --resource-group $resourceGroup `
    --name $appName `
    --src "bin/Release/net8.0/publish.zip"

# 4. Monitor deployment
az functionapp deployment list `
    --resource-group $resourceGroup `
    --name $appName `
    --query "[0].[status,timestamp]"
```

### 2.4 Verification (5 minutes)
```powershell
# 1. Check function is running
$functionUrl = "https://${appName}.azurewebsites.net/api/health"
$response = Invoke-RestMethod -Uri $functionUrl -ErrorAction SilentlyContinue
$response | ConvertTo-Json | Write-Output

# 2. Verify tipologías endpoint works
$tipsUrl = "https://${appName}.azurewebsites.net/api/management/tipologias"
$tips = Invoke-RestMethod -Uri $tipsUrl -Headers @{ "Authorization" = "Bearer $token" }
Write-Output "Tipologías loaded: $($tips.Count) items"

# 3. Test classification
$testDoc = @{ tipologiaId = 1; content = "test document" } | ConvertTo-Json
$classifyUrl = "https://${appName}.azurewebsites.net/api/classify"
$result = Invoke-RestMethod -Uri $classifyUrl -Method Post -Body $testDoc -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }
Write-Output "Classification result: $($result.status)"

# 4. Check Application Insights for errors
# Navigate to: https://portal.azure.com -> Application Insights -> Failures
# Verify: No new errors related to PromptGPT or ConfiguracionJson
```

---

## 3. POST-DEPLOYMENT MONITORING

### 3.1 Immediate Checks (First Hour)
```powershell
# 1. Monitor Application Insights for errors
# Expected: 0 new errors related to [Obsolete] field

# 2. Check classification pipeline
# Expected: All classifications working normally

# 3. Verify tipología API responses
# Expected: No changes to API contract (ConfigJson still in response)
```

### 3.2 Extended Monitoring (First 24 Hours)
- [ ] Application Insights: No CS0618-related errors
- [ ] Classification success rate: > 99%
- [ ] Performance metrics: No degradation
- [ ] Error logs: Clean (no "PromptGPT" mentions)

---

## 4. CLIENT COMMUNICATION

### 4.1 For API Consumers
**Message**: No action required. v1.5 is a maintenance release with deprecation warnings only.

```
v1.5 Release Notes:
- PromptGPT field marked as [Obsolete]
- Internally already using ConfiguracionJson (no change to behavior)
- ConfiguracionJson remains the primary field
- All APIs unchanged
- Recommended: Plan migration to ConfiguracionJson in your code before v2.0 (July 2026)
```

### 4.2 For Internal Teams
**Message**: Update code to suppress CS0618 warnings and use extension methods.

```csharp
// ❌ Before (generates CS0618 warning)
var prompt = tipologia.PromptGPT;

// ✅ After (correct approach)
var prompt = tipologia.GetSystemPrompt();
```

---

## 5. ROLLBACK PROCEDURE (If Needed)

### 5.1 Quick Rollback (If Deployment Fails)
```powershell
# 1. Get previous deployment slot
az functionapp deployment list `
    --resource-group $resourceGroup `
    --name $appName `
    --query "[1].[id,status,timestamp]"

# 2. Swap back to previous version
az functionapp deployment slot swap `
    --resource-group $resourceGroup `
    --name $appName `
    --slot staging

# 3. Verify
$response = Invoke-RestMethod -Uri "$functionUrl/health" -ErrorAction SilentlyContinue
if ($response.status -eq "healthy") {
    Write-Output "✅ Rollback successful"
} else {
    Write-Output "❌ Rollback failed"
}
```

### 5.2 Database Rollback (Not Needed for v1.5)
**Note**: v1.5 has NO database changes. Only code deployment.

---

## 6. SUCCESS CRITERIA

- [ ] Deployment completed without errors
- [ ] All 32 tests passing in production environment
- [ ] Application Insights shows 0 new errors
- [ ] Classification pipeline functioning normally
- [ ] No CS0618 warnings in production logs
- [ ] API contract unchanged
- [ ] Performance metrics normal

---

## 7. CONTACTS

| Role | Name | Contact |
|------|------|---------|
| Deployment Lead | [Name] | [Email/Phone] |
| On-Call Engineer | [Name] | [Email/Phone] |
| Database Admin | [Name] | [Email/Phone] |

---

## 8. TIMELINE

| Task | Duration | Start | End |
|------|----------|-------|-----|
| Pre-deployment checks | 5 min | T+0:00 | T+0:05 |
| Build & package | 10 min | T+0:05 | T+0:15 |
| Deploy to Azure | 5 min | T+0:15 | T+0:20 |
| Verification | 5 min | T+0:20 | T+0:25 |
| **Total** | **25 min** | | |

---

## 9. NOTES

- v1.5 is a **CODE-ONLY** deployment
- **ZERO** database changes
- **ZERO** downtime (rolling deployment)
- All clients continue working without any changes
- This release prepares the foundation for v2.0 (July 31, 2026)
