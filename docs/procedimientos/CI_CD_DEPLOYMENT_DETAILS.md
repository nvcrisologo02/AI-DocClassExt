# CI/CD & Deployment Details — DocumentIA

## 1. PIPELINE OVERVIEW

Three main pipelines (in repo root):

```
azure-pipelines.yml (General/Admin)
├─ Stages: build, test, deploy-staging, deploy-prod
└─ Artifacts: Function App code, Admin website

azure-pipelines-functions.yml (Functions service)
├─ Stages: build, test, deploy-staging, deploy-prod
└─ Artifacts: Function App binaries, host.json, function.json

azure-pipelines-assetresolver.yml (AssetResolver plugin)
├─ Stages: build, test
└─ Artifacts: Plugin binary
```

---

## 2. BUILD STAGE

### What Happens

1. Agent OS: Windows (Azure DevOps)
2. Dotnet version: 8.0 (from global.json)
3. Steps:
   ```yaml
   - Restore NuGet packages
   - Build solution (.csproj)
   - Run unit tests
   - Publish artifacts
   - Generate code metrics
   ```

### Duration: ~3-5 min

### Common Failures

| Error | Cause | Fix |
|-------|-------|-----|
| `PackageNotFound` | NuGet source offline | Check nuget.org status |
| `CompilationError` | Code syntax error | Review recent commits |
| `TestFailure` | Unit test regression | Fix code, re-run locally |

### Debugging Build Failures

```powershell
# Reproduce locally
cd src/backend/DocumentIA.Functions
dotnet clean
dotnet restore
dotnet build

# If build succeeds locally but fails in pipeline:
# → Check nuget.config, check .NET version, check dependencies
```

---

## 3. TEST STAGE

### Unit Tests
- Framework: xUnit
- Location: `tests/**/*.Tests.csproj`
- Coverage target: 70%+ of new code
- Runs: ~30-60 sec

### Integration Tests
- Location: `tests/**/*.IntegrationTests.csproj`
- Requires: Local SQL Server, Storage emulation
- Runs: ~2-3 min
- Skipped in CI (requires local setup)

### Smoke Tests
- Location: `scripts/testing/smoke-test-*.ps1`
- Runs after deploy to staging
- Verifies: API health, classification works, data extracted
- Runs: ~1 min

---

## 4. DEPLOY TO STAGING

### Prerequisites

- [ ] Build succeeded
- [ ] All tests passed
- [ ] No security warnings

### Deployment Steps (automated by pipeline)

1. **Backup Staging Database**
   ```powershell
   az sql db backup create --resource-group RG-Staging --server doc-ia-sql-staging --name DocumentIA
   ```

2. **Stop Staging Function App** (to prevent conflicts)
   ```powershell
   az functionapp stop --name documentia-functions-staging --resource-group RG-Staging
   ```

3. **Run Migrations** (if any)
   ```powershell
   dotnet ef database update --context DocumentIADbContext
   ```

4. **Deploy Function Code**
   ```powershell
   az functionapp deployment source config-zip --resource-group RG-Staging --name documentia-functions-staging --src build/output.zip
   ```

5. **Start Function App**
   ```powershell
   az functionapp start --name documentia-functions-staging --resource-group RG-Staging
   ```

6. **Run Smoke Tests**
   ```powershell
   ./scripts/testing/smoke-test-release.ps1 -Endpoint "https://documentia-functions-staging.azurewebsites.net"
   ```

### Expected Downtime: < 2 min

### Monitoring Post-Deploy

```kql
// In Application Insights
traces
| where timestamp > ago(5m)
| where severityLevel > 1
| summarize count() by severity
```

If error rate > 2%: Investigate before proceeding to prod.

---

## 5. DEPLOY TO PRODUCTION

### Manual Approval Gate

Deployment paused here, requires approval from:
- Tech Lead
- Lead Architect (for major releases)

Approval via: Azure DevOps Release Approvals (UI or email)

### Production Deployment Steps

1. **Pre-Flight Checks**
   ```powershell
   # Verify prod DB health
   az sql db show --resource-group RG-Prod --server doc-ia-sql --name DocumentIA --query "state"
   # Should show: "Online"
   ```

2. **Backup Production Database**
   ```powershell
   az sql db backup create --resource-group RG-Prod --server doc-ia-sql --name DocumentIA `
     --backup-name "pre-deploy-$(Get-Date -Format 'yyyyMMdd-HHmm')"
   ```

3. **Deploy Functions**
   - Similar steps as staging
   - BUT: Stagger deployment (not all instances at once)
   - Rolling deployment: 1 instance, verify, then remaining

4. **Verify Deployment**
   ```kql
   // Check error rate for last 5 min
   requests
   | where timestamp > ago(5m)
   | summarize success_rate=100.0 * sum(tolong(success)) / count() by bin(timestamp, 1m)
   | render timechart
   ```

5. **Monitor 1 Hour**
   - Error rate must stay < 1%
   - Latency must stay < 30 sec P99
   - No spike in exceptions

### If Production Deployment Fails

1. **Immediate Actions:**
   ```powershell
   # Rollback code
   git revert <commit-hash>
   # Redeploy (via pipeline again)
   ```

2. **Restore from Backup** (if data corrupted):
   ```powershell
   # Get backup list
   az sql db backup list --resource-group RG-Prod --server doc-ia-sql --database DocumentIA
   # Restore
   az sql db restore --resource-group RG-Prod --server doc-ia-sql --name DocumentIA --backup-name "pre-deploy-..."
   ```

3. **Post-Incident:**
   - Document what went wrong
   - Schedule retro within 24 hours
   - Add test to prevent recurrence

---

## 6. TROUBLESHOOTING PIPELINE FAILURES

### Pipeline Hangs (No progress for > 30 min)

**Cause:** Agent stuck or deadlock

**Solution:**
```powershell
# Kill stuck run from Azure DevOps UI
# Or via CLI:
az pipelines runs cancel --id <run-id> --project "AI DocClassExt"
```

### Timeout During Deployment

**Cause:** Large artifact or slow network

**Solution:**
```powershell
# Increase timeout in pipeline YAML
# Task: timeout: 1200  # seconds (20 min)
```

### Secret Not Found

**Cause:** Key Vault secret doesn't exist or permissions missing

**Solution:**
```powershell
# List secrets available to pipeline identity
az keyvault secret list --vault-name documentia-kv --query [].name -o tsv

# Add missing secret
az keyvault secret set --vault-name documentia-kv --name "MySecret" --value "value"
```

---

## 7. MANUAL DEPLOYMENT (If Pipeline Broken)

Emergency deployment without pipeline:

```powershell
# 1. Build locally
cd src/backend/DocumentIA.Functions
dotnet build -c Release
dotnet publish -c Release -o ./publish

# 2. Create ZIP
Compress-Archive -Path ./publish/* -DestinationPath ./deploy.zip -Force

# 3. Deploy to Azure
az functionapp deployment source config-zip --resource-group RG-Prod `
  --name documentia-functions --src ./deploy.zip

# 4. Monitor logs
az functionapp log tail --name documentia-functions --resource-group RG-Prod
```

---

## 8. SECRETS MANAGEMENT IN PIPELINE

### Where Secrets Stored

- **Azure DevOps:** Pipeline variables (encrypted)
- **Key Vault:** Runtime secrets (RBAC protected)
- **Never in:** Code, YAML files, Git

### Accessing Secrets in Pipeline

```yaml
steps:
- task: AzureKeyVault@2
  inputs:
    azureSubscription: 'Azure Subscription'
    KeyVaultName: 'documentia-kv'
    SecretsFilter: '*'  # Load all secrets
- script: echo $(MySecret)  # Use in step
```

### Rotating Secrets

```powershell
# 1. Update in Key Vault
az keyvault secret set --vault-name documentia-kv --name "ApiKey" --value "new-key"

# 2. Restart Function App to reload
az functionapp restart --name documentia-functions --resource-group RG-Prod

# 3. Verify new secret loaded
# (Check logs for successful connection)
```

---

## 9. DEPLOYMENT LOGS & DIAGNOSTICS

### Access Deployment Logs

Azure DevOps:
- Pipelines → Run → View logs (by task)

Azure Portal:
- Function App → Deployment → Logs

Application Insights:
- Traces & exceptions during deployment

### Common Log Messages

```
"Successfully deployed Function App" → Success
"Deployment timed out" → Check network, increase timeout
"Invalid connection string" → Check Key Vault, secrets
"Package too large" → Reduce artifacts, check .gitignore
```
