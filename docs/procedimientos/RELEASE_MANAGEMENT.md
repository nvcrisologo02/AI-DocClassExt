# Release Management — DocumentIA

## 1. VERSIONING STRATEGY

### Semantic Versioning (Semver)

Format: `MAJOR.MINOR.PATCH` (e.g., 1.5.3)

- **MAJOR:** Breaking changes (incompatible API, data migration required)
- **MINOR:** New features (backward compatible)
- **PATCH:** Bug fixes (backward compatible)

Examples:
- v1.5.0 → v2.0.0: Changed plugin system API
- v1.5.0 → v1.6.0: Added new provider type
- v1.5.0 → v1.5.1: Fixed memory leak

---

## 2. RELEASE TIMELINE

### Pre-Release (1 week before)

**Day 1:**
1. Create release branch: `git checkout -b release/v1.x.0`
2. Bump version in:
   - Project files: `*.csproj`
   - appsettings.json
   - Package.json (if applicable)
3. Create CHANGELOG entry with all changes
4. PR for code review

**Days 2-3:**
- Code review + testing on release branch
- Integration testing in staging environment
- Security scan: `dotnet analyzer`
- Performance baseline: Run load tests

**Days 4-5:**
- Final testing & sign-off
- Merge PR to develop
- Tag commit: `git tag v1.x.0`

### Release Day

**Morning (Execution):**
1. Run pre-release checklist (see section 5)
2. Deploy to staging: Run `azure-pipelines-functions.yml` manually with stage="staging"
3. Smoke test staging (see section 6)
4. Get final approval from lead architect

**Afternoon (Go-Live):**
1. Deploy to production: Trigger `azure-pipelines-functions.yml` stage="production"
2. Monitor error rate for 1 hour (should be < 1%)
3. Monitor latency for 1 hour (P99 should be < 30 sec)
4. If issues: Rollback (see section 3)
5. If ok: Mark as release complete

**Post-Release (3 days):**
- Monitor production metrics daily
- Collect user feedback
- Document any issues
- Scheduled hotfix if needed

---

## 3. ROLLBACK PROCEDURES

### When to Rollback

- Critical bug affecting > 50% of documents
- Data corruption
- P99 latency > 60 sec (sustained)
- Error rate > 10%

### Rollback Steps (< 30 min)

**Code Rollback:**
```powershell
# Revert to previous version
git revert <commit-hash>
git push origin release/v1.x.0
# Redeploy via pipeline
```

**Database Rollback:**
1. If migrations ran: Revert last migration
   ```powershell
   dotnet ef migrations remove
   dotnet ef database update <previous-migration>
   ```
2. If data corrupted: Restore from backup
   ```
   See INFRAESTRUCTURA_DESPLIEGUE.md section 5
   ```

**Configuration Rollback:**
1. Revert Key Vault secrets to previous version
2. Restart Function App

**Communication:**
1. Notify team: "Rolled back to v1.x.y due to [reason]"
2. Post-incident review within 24 hours

---

## 4. HOTFIX PROCESS (Critical production bug)

### When to Use Hotfix

- Production is broken / high error rate
- Can't wait for next release
- Limited scope (1-3 files changed)

### Hotfix Workflow

1. Create branch: `git checkout -b hotfix/v1.5.1` (from main/production tag)
2. Fix code
3. Bump patch version (v1.5.0 → v1.5.1)
4. Test locally + staging
5. PR + rapid review (30 min)
6. Deploy to production
7. Tag: `git tag v1.5.1`
8. Merge back to develop

---

## 5. PRE-RELEASE CHECKLIST

Run before every release (automated where possible):

- [ ] All tests pass locally: `dotnet test`
- [ ] Code review completed & approved
- [ ] No security warnings: `dotnet analyzer`
- [ ] Performance baseline captured: `load-test.ps1`
- [ ] Staging deployed & smoke tested
- [ ] Database migrations tested on staging
- [ ] Configuration reviewed (no secrets exposed)
- [ ] Changelog updated with user-facing changes
- [ ] Version number bumped in all places
- [ ] Git tags prepared
- [ ] Monitoring alerts set up for new metrics
- [ ] Rollback procedure reviewed & tested
- [ ] Team notified of release window
- [ ] Load test results reviewed (no degradation)

---

## 6. SMOKE TEST SCRIPT

**Location:** `scripts/testing/smoke-test-release.ps1`

```powershell
# Minimal tests to verify release
$baseUrl = "https://documentia-staging.azurewebsites.net"

# Test 1: API health
$health = Invoke-RestMethod "$baseUrl/api/health" -TimeoutSec 10
if ($health.status -ne "healthy") { throw "Health check failed" }

# Test 2: Classify simple document
$testDoc = Get-Content "test-data/simple-note.json" | ConvertFrom-Json
$result = Invoke-RestMethod "$baseUrl/api/classify" -Method POST -Body ($testDoc | ConvertTo-Json) -TimeoutSec 30
if ($result.confidence -lt 0.5) { throw "Classification confidence too low" }

# Test 3: Extract data
if (-not $result.extracted) { throw "Data extraction failed" }

Write-Host "✅ All smoke tests passed"
```

---

## 7. CHANGELOG FORMAT

**Location:** `CHANGELOG.md` (in repo root)

```markdown
## [1.5.0] - 2026-06-10

### Added
- New plugin: RegexClasificador for high-confidence patterns
- Performance tuning guide for operators

### Fixed
- Memory leak in ExtractActivity (issue #123)
- Classification timeout with large PDFs (issue #122)

### Changed
- Increased activity timeout from 5 min to 10 min
- Updated provider retry policy

### Removed
- Legacy mock provider (use new MockProvider instead)

### Known Issues
- DirectInvoice SOAP timeout on weekends (pending fix)

### Migration Notes
- No database changes in this version
- Configuration backward compatible

### Performance Impact
- +15% throughput with new retry policy
- -5% latency with caching improvements
```

---

## 8. VERSION COMPATIBILITY MATRIX

Current version: **v1.5.0**

| Version | .NET | SQL DB | Plugins | Status |
|---------|------|--------|---------|--------|
| v1.0-1.2 | .NET 6 | ❌ Not supported | Legacy | ❌ EOL |
| v1.3-1.4 | .NET 8 | ✅ 2018+ | v1 | ⚠️ LTS until 2026-12-31 |
| v1.5+ | .NET 8/9 | ✅ 2019+ | v2 | ✅ Current |

**Breaking Changes in v1.5:**
- Plugin API: `IExtraerDataProvider.ExtractAsync()` signature changed
- Database: Migration from v1.4 required (auto-run)
- Configuration: Config JSON structure updated (migration script provided)

---

## 9. COMMUNICATION TEMPLATE

### Pre-Release (1 week before)
```
Subject: [RELEASE] DocumentIA v1.5.0 scheduled for June 10

Hi team,

We're releasing v1.5.0 on June 10:

Changes:
- Added new plugin system
- Fixed memory leak
- 15% performance improvement

Testing: June 9 in staging
Release: June 10, 14:00 UTC
Estimated downtime: < 5 min

Questions? See: docs/INDEX.md → Release Management

—Ops Team
```

### Post-Release (Day of)
```
✅ Released v1.5.0 to production

Status:
- Error rate: < 1% ✅
- P99 latency: 20 sec ✅
- No critical issues ✅

Monitoring 24/7. Report issues in #incidents channel.

—Ops Team
```
